using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WatchStream.Core.Contracts;
using WatchStream.Core.Models;

namespace WatchStream.Xbox.Services
{
    /// <summary>
    /// Reads the signed-in user's library from the Stremio account API and public catalogs
    /// through the documented Stremio addon protocol.
    /// </summary>
    public sealed class StremioCatalogService
    {
        private const string ApiUrl = "https://api.strem.io/api/getUser";
        private const string CinemetaManifestUrl = "https://v3-cinemeta.strem.io/manifest.json";
        private readonly ISessionStore _sessionStore;
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public StremioCatalogService(ISessionStore sessionStore)
        {
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        }

        public async Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken)
        {
            var authKey = await _sessionStore.ReadSessionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(authKey))
                throw new InvalidOperationException("Connect your Stremio account to view your library.");

            using (var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl))
            {
                request.Content = new StringContent(JsonSerializer.Serialize(new { authKey }), System.Text.Encoding.UTF8, "application/json");
                using (var response = await _http.SendAsync(request, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Stremio couldn't load your account library. Try reconnecting your account.");
                    using (var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("error", out _))
                            throw new InvalidOperationException("Stremio rejected the saved session. Reconnect your account and try again.");
                        if (!root.TryGetProperty("result", out var result))
                            throw new InvalidOperationException("Stremio returned an unexpected library response.");

                        // The account API's getUser response is the canonical user record.
                        // Support the common array form and wrapped library item records.
                        if (!result.TryGetProperty("library", out var library) || library.ValueKind == JsonValueKind.Null)
                            return Array.Empty<MediaItem>();
                        var entries = new List<MediaItem>();
                        if (library.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var entry in library.EnumerateArray())
                            {
                                var media = ParseMedia(entry);
                                if (media != null) entries.Add(media);
                            }
                        }
                        else if (library.ValueKind == JsonValueKind.Object &&
                                 library.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var entry in items.EnumerateArray())
                            {
                                var media = ParseMedia(entry);
                                if (media != null) entries.Add(media);
                            }
                        }
                        return entries;
                    }
                }
            }
        }

        public async Task<IReadOnlyList<MediaItem>> GetDiscoverAsync(string mediaType, CancellationToken cancellationToken)
        {
            if (mediaType != "movie" && mediaType != "series")
                throw new ArgumentException("Discover supports movie or series catalogs.", nameof(mediaType));

            using (var manifestResponse = await _http.GetAsync(CinemetaManifestUrl, cancellationToken))
            {
                if (!manifestResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException("Discover catalogs are temporarily unavailable.");
                using (var manifestDoc = JsonDocument.Parse(await manifestResponse.Content.ReadAsStringAsync()))
                {
                    if (!manifestDoc.RootElement.TryGetProperty("catalogs", out var catalogs) || catalogs.ValueKind != JsonValueKind.Array)
                        throw new InvalidOperationException("The catalog provider returned an unexpected manifest.");
                    string? catalogId = null;
                    foreach (var catalog in catalogs.EnumerateArray())
                    {
                        if (catalog.TryGetProperty("type", out var type) && type.GetString() == mediaType &&
                            catalog.TryGetProperty("id", out var id))
                        {
                            catalogId = id.GetString();
                            if (!string.IsNullOrWhiteSpace(catalogId)) break;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(catalogId))
                        throw new InvalidOperationException("No compatible catalog was advertised for this content type.");

                    var catalogUrl = "https://v3-cinemeta.strem.io/catalog/" + Uri.EscapeDataString(mediaType) + "/" + Uri.EscapeDataString(catalogId) + ".json";
                    using (var response = await _http.GetAsync(catalogUrl, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new InvalidOperationException("Discover couldn't load this catalog. Try again.");
                        using (var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
                        {
                            if (!doc.RootElement.TryGetProperty("metas", out var metas) || metas.ValueKind != JsonValueKind.Array)
                                return Array.Empty<MediaItem>();
                            var result = new List<MediaItem>();
                            foreach (var meta in metas.EnumerateArray())
                            {
                                var media = ParseMedia(meta);
                                if (media != null) result.Add(media);
                            }
                            return result;
                        }
                    }
                }
            }
        }

        private static MediaItem? ParseMedia(JsonElement entry)
        {
            if (entry.ValueKind != JsonValueKind.Object) return null;
            var item = entry;
            if (entry.TryGetProperty("item", out var nested) && nested.ValueKind == JsonValueKind.Object) item = nested;

            var id = ReadString(item, "id") ?? ReadString(item, "_id");
            var type = ReadString(item, "type") ?? "movie";
            var name = ReadString(item, "name") ?? ReadString(item, "title");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return null;

            var poster = ReadString(item, "poster");
            var year = ReadString(item, "releaseInfo") ?? ReadString(item, "year");
            var description = ReadString(item, "description") ?? string.Empty;
            return new MediaItem(id, type, name, poster, year, description);
        }

        private static string? ReadString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : element.TryGetProperty(property, out value) && value.ValueKind == JsonValueKind.Number
                    ? value.ToString()
                    : null;
        }
    }
}