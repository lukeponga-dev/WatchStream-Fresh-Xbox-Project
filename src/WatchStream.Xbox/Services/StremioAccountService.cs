using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WatchStream.Core.Contracts;

namespace WatchStream.Xbox.Services
{
    /// <summary>Minimal adapter based on the public Stremio API client contract.</summary>
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

            var body = JsonSerializer.Serialize(new { authKey = (string?)null, email = email.Trim(), password });
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
                        if (root.TryGetProperty("error", out _))
                            throw new InvalidOperationException("Stremio sign-in failed. Check your email and password.");
                        if (!root.TryGetProperty("result", out var result) ||
                            !result.TryGetProperty("authKey", out var key) ||
                            key.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(key.GetString()))
                            throw new InvalidOperationException("Stremio returned an unexpected sign-in response.");
                        await _sessionStore.WriteSessionAsync(key.GetString(), cancellationToken);
                    }
                }
            }
        }

        public async Task<AccountSessionState> GetSessionStateAsync(CancellationToken cancellationToken)
        {
            var session = await _sessionStore.ReadSessionAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(session) ? AccountSessionState.SignedOut : AccountSessionState.SignedIn;
        }

        public async Task SignOutAsync(CancellationToken cancellationToken)
        {
            var authKey = await _sessionStore.ReadSessionAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrWhiteSpace(authKey))
                {
                    var body = JsonSerializer.Serialize(new { authKey });
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
    }
}
