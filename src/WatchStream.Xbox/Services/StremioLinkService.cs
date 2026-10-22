using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using WatchStream.Core.Contracts;

namespace WatchStream.Xbox.Services
{
    /// <summary>
    /// Implements the link API contract used by Stremio Core:
    /// GET https://link.stremio.com/api/v2/create?type=Create
    /// GET https://link.stremio.com/api/v2/read?type=Read&code={code}
    /// </summary>
    public sealed class StremioLinkService
    {
        private const string LinkApiBase = "https://link.stremio.com/api/v2/";
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private readonly ISessionStore _sessionStore;

        public StremioLinkService(ISessionStore sessionStore)
        {
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
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
                    if (!root.TryGetProperty("result", out var result) ||
                        !result.TryGetProperty("code", out var code) ||
                        !result.TryGetProperty("link", out var link) ||
                        !result.TryGetProperty("qrcode", out var qr) ||
                        code.ValueKind != JsonValueKind.String ||
                        link.ValueKind != JsonValueKind.String ||
                        qr.ValueKind != JsonValueKind.String)
                    {
                        throw new InvalidOperationException("Stremio returned an unexpected link response.");
                    }

                    return new StremioLinkCode(code.GetString()!, link.GetString()!, qr.GetString()!);
                }
            }
        }

        /// <summary>
        /// Returns true and persists the auth key only after Stremio reports an approved link.
        /// A pending/empty response returns false. API errors are surfaced for retry by the caller.
        /// </summary>
        public async Task<bool> TryCompleteLinkAsync(string code, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("A link code is required.", nameof(code));

            var url = LinkApiBase + "read?type=Read&code=" + Uri.EscapeDataString(code);
            using (var response = await _httpClient.GetAsync(url, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var text = await response.Content.ReadAsStringAsync();
                using (var document = JsonDocument.Parse(text))
                {
                    var root = document.RootElement;
                    if (!root.TryGetProperty("result", out var result) ||
                        !result.TryGetProperty("authKey", out var authKey) ||
                        authKey.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(authKey.GetString()))
                        return false;

                    await _sessionStore.WriteSessionAsync(authKey.GetString(), cancellationToken);
                    return true;
                }
            }
        }
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
