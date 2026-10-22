using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WatchStream.Core.Contracts;

namespace WatchStream.Xbox.Services
{
    /// <summary>
    /// Stremio Core-compatible account API adapter. Passwords are sent only to Stremio
    /// and are never stored locally.
    /// </summary>
    public sealed class StremioAccountService : IStremioAccountService
    {
        private const string ApiBase = "https://api.strem.io/api/";
        private readonly ISessionStore _sessionStore;
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public StremioAccountService(ISessionStore sessionStore)
        {
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        }

        public async Task SignInAsync(string email, string password, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Enter your Stremio email and password.");

            // Matches stremio-core's serialized APIRequest::Auth(AuthRequest::Login).
            // The duplicate type keys reflect the current Rust serialization contract.
            var safeEmail = JsonSerializer.Serialize(email.Trim());
            var safePassword = JsonSerializer.Serialize(password);
            var body = "{\"type\":\"Auth\",\"type\":\"Login\",\"email\":" + safeEmail +
                       ",\"password\":" + safePassword + ",\"facebook\":false}";
            using (var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "login"))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                using (var response = await _httpClient.SendAsync(request, cancellationToken))
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Stremio sign-in failed. Check your details and internet connection.");

                    using (var document = JsonDocument.Parse(responseText))
                    {
                        var root = document.RootElement;
                        if (HasApiError(root) || !TryGetResult(root, out var result))
                            throw new InvalidOperationException("Stremio sign-in failed. Check your email and password.");

                        var key = ReadString(result, "key") ?? ReadString(result, "authKey");
                        if (string.IsNullOrWhiteSpace(key))
                            throw new InvalidOperationException("Stremio returned an unexpected sign-in response.");
                        await _sessionStore.WriteSessionAsync(key, cancellationToken);
                    }
                }
            }
        }

        public async Task<AccountSessionState> GetSessionStateAsync(CancellationToken cancellationToken)
        {
            var authKey = await _sessionStore.ReadSessionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(authKey)) return AccountSessionState.SignedOut;

            var body = "{\"type\":\"GetUser\",\"authKey\":" + JsonSerializer.Serialize(authKey) + "}";
            using (var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "getUser"))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                using (var response = await _httpClient.SendAsync(request, cancellationToken))
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Couldn't validate the saved Stremio session. Check your connection and retry.");

                    using (var document = JsonDocument.Parse(responseText))
                    {
                        var root = document.RootElement;
                        if (HasApiError(root) || !TryGetResult(root, out var result) ||
                            result.ValueKind == JsonValueKind.Null ||
                            (result.ValueKind == JsonValueKind.Object && !result.EnumerateObject().MoveNext()))
                        {
                            await _sessionStore.ClearAsync(cancellationToken);
                            return AccountSessionState.ReauthenticationRequired;
                        }
                        return AccountSessionState.SignedIn;
                    }
                }
            }
        }

        public async Task SignOutAsync(CancellationToken cancellationToken)
        {
            var authKey = await _sessionStore.ReadSessionAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrWhiteSpace(authKey))
                {
                    var body = "{\"type\":\"Logout\",\"authKey\":" + JsonSerializer.Serialize(authKey) + "}";
                    using (var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "logout"))
                    {
                        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                        using (var response = await _httpClient.SendAsync(request, cancellationToken))
                            _ = response.IsSuccessStatusCode;
                    }
                }
            }
            finally { await _sessionStore.ClearAsync(CancellationToken.None); }
        }

        private static bool TryGetResult(JsonElement root, out JsonElement result)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("result", out result))
                return true;
            result = default;
            return false;
        }

        private static bool HasApiError(JsonElement root) =>
            root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error) &&
            error.ValueKind != JsonValueKind.Null && error.ValueKind != JsonValueKind.False &&
            !(error.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(error.GetString()));

        private static string? ReadString(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}