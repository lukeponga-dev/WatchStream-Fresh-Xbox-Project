using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace WatchStream.Xbox.Services
{
    /// <summary>Connects WatchStream to a Stremio Streaming Server running on another device.</summary>
    public sealed class StremioServerService
    {
        private const string SettingsKey = "WatchStream.StremioServer.BaseUrl.v1";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        public string GetBaseUrl()
        {
            var saved = ApplicationData.Current.LocalSettings.Values[SettingsKey] as string;
            return string.IsNullOrWhiteSpace(saved) ? string.Empty : NormalizeBaseUrl(saved);
        }

        public void SaveBaseUrl(string baseUrl)
        {
            var normalized = NormalizeBaseUrl(baseUrl);
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("Enter a valid HTTP or HTTPS Stremio Server URL, for example http://192.168.1.20:11470.");

            ApplicationData.Current.LocalSettings.Values[SettingsKey] = normalized;
        }

        public async Task<string> CheckConnectionAsync(string baseUrl, CancellationToken cancellationToken)
        {
            var normalized = NormalizeBaseUrl(baseUrl);
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("Enter a valid HTTP or HTTPS Stremio Server URL.");

            var heartbeat = new Uri(uri, "heartbeat");
            using (var response = await Http.GetAsync(heartbeat, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("The server responded with HTTP " + (int)response.StatusCode + ". Check the address and that Stremio Server is running.");
                return "Connected to Stremio Server at " + normalized;
            }
        }

        private static string NormalizeBaseUrl(string value) => (value ?? string.Empty).Trim().TrimEnd('/') + "/";
    }
}
