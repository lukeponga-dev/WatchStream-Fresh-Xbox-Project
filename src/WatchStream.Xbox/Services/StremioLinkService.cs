using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using WatchStream.Core.Contracts;

namespace WatchStream.Xbox.Services
{
    internal static class WatchStreamJson
    {
        internal static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
    }

    /// <summary>
    /// Implements Stremio Core's account-link contract. The link Read response returns
    /// the account auth key directly once the user approves the pairing; that key is
    /// persisted as the session and no further token exchange is performed.
    /// </summary>
    public sealed class StremioLinkService
    {
        private const string LinkApiBase = "https://link.stremio.com/api/v2/";
        private const string AccountApiBase = "https://api.strem.io/api/";
        private readonly HttpClient _httpClient;
        private readonly ISessionStore _sessionStore;

        public StremioLinkService(ISessionStore sessionStore)
            : this(sessionStore, new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
        {
        }

        public StremioLinkService(ISessionStore sessionStore, HttpClient httpClient)
        {
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<StremioLinkCode> CreateLinkAsync(CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(LinkApiBase + "create?type=Create", cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var text = await response.Content.ReadAsStringAsync();
                using (var document = JsonDocument.Parse(text))
                {
                    var root = document.RootElement;
                    if (!TryGetResult(root, out var result) ||
                        !TryReadString(result, "code", out var code) ||
                        !TryReadString(result, "link", out var link) ||
                        !TryReadString(result, "qrcode", out var qr))
                        throw new InvalidOperationException("Stremio returned an unexpected link response.");

                    return new StremioLinkCode(code, link, qr);
                }
            }
        }

        /// <summary>
        /// Checks whether a device link has been approved. Once the user approves the
        /// pairing, the link Read response returns the account auth key directly
        /// (stremio-core's LinkDataResponse::AuthKey); that key is persisted as the
        /// session and no further token exchange is required. A pending link (no authKey
        /// yet) returns false; failures are surfaced as exceptions.
        /// </summary>
        public async Task<bool> TryCompleteLinkAsync(string code, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("A link code is required.", nameof(code));

            var url = LinkApiBase + "read?type=Read&code=" + Uri.EscapeDataString(code);
            using (var response = await _httpClient.GetAsync(url, cancellationToken))
            {
                var text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new StremioLinkException("Stremio's pairing service returned HTTP " +
                        (int)response.StatusCode + ". Generate a new code and try again.");

                using (var document = JsonDocument.Parse(text))
                {
                    var root = document.RootElement;
                    if (HasApiError(root))
                        throw new StremioLinkException("Stremio rejected this pairing code. It may have expired; generate a new code and try again.");

                    // The link Read response carries the account auth key once approved.
                    // Until then the authKey field is absent and the link is still pending.
                    if (!TryGetResult(root, out var result) ||
                        !TryReadString(result, "authKey", out var authKey))
                        return false;

                    await _sessionStore.WriteSessionAsync(authKey, cancellationToken);
                    return true;
                }
            }
        }

        /// <summary>
        /// Completes the official Stremio Web Facebook flow using its returned account
        /// email and fbLoginToken, then persists the returned account auth key.
        /// </summary>
        public async Task<bool> CompleteFacebookSignInAsync(string email, string facebookLoginToken, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("A Facebook account email is required.", nameof(email));
            if (string.IsNullOrWhiteSpace(facebookLoginToken))
                throw new ArgumentException("Stremio did not return the Facebook login credential.", nameof(facebookLoginToken));

            // Stremio Web's official flow obtains email + fbLoginToken from
            // /login-fb-get-acc/{state}, then authenticates via AuthRequest::Login
            // with facebook=true. Do not send the Facebook access token to this API.
            var body = JsonSerializer.Serialize(new { type = "Login", email, password = facebookLoginToken, facebook = true }, WatchStreamJson.Options);
            using (var request = new HttpRequestMessage(HttpMethod.Post, AccountApiBase + "login"))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                using (var response = await _httpClient.SendAsync(request, cancellationToken))
                {
                    var text = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Stremio couldn't finish Facebook sign-in. Check your connection and try again.");

                    using (var document = JsonDocument.Parse(text))
                    {
                        var root = document.RootElement;
                        if (HasApiError(root) || !TryGetResult(root, out var result) ||
                            !TryReadString(result, "authKey", out var authKey))
                            throw new InvalidOperationException("Stremio rejected the Facebook sign-in. Please try again.");

                        await _sessionStore.WriteSessionAsync(authKey, cancellationToken);
                        return true;
                    }
                }
            }
        }

        private static bool TryGetResult(JsonElement root, out JsonElement result)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("result", out result))
                return true;
            result = default;
            return false;
        }

        private static bool TryReadString(JsonElement element, string property, out string value)
        {
            value = string.Empty;
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(property, out var candidate) ||
                candidate.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(candidate.GetString()))
                return false;
            value = candidate.GetString()!;
            return true;
        }

        private static bool HasApiError(JsonElement root) =>
            root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error) &&
            error.ValueKind != JsonValueKind.Null && error.ValueKind != JsonValueKind.False &&
            !(error.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(error.GetString()));
    }

    public sealed class StremioLinkException : InvalidOperationException
    {
        public StremioLinkException(string message) : base(message) { }
    }

    public sealed class StremioLinkCode
    {
        public string Code { get; }
        public string Link { get; }
        public string QrCode { get; }

        public StremioLinkCode(string code, string link, string qrCode)
        {
            Code = code;
            Link = link;
            QrCode = qrCode;
        }
    }
}