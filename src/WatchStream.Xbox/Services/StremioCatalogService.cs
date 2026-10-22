using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WatchStream.Core.Contracts;
using WatchStream.Core.Models;

namespace WatchStream.Xbox.Services
{
    /// <summary>Stremio datastore library plus manifest-driven add-on catalogs.</summary>
    public sealed class StremioCatalogService
    {
        private const string ApiBase = "https://api.strem.io/api/";
        private const string CinemetaManifestUrl = "https://v3-cinemeta.strem.io/manifest.json";
        private readonly ISessionStore _sessionStore;
        private readonly StremioAddonService _addons = new StremioAddonService();
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public StremioCatalogService(ISessionStore sessionStore) =>
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));

        public async Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken)
        {
            var key = await _sessionStore.ReadSessionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Connect your Stremio account to view your library.");

            // Matches stremio-core DatastoreRequest for the libraryItem collection.
            var body = "{\"authKey\":" + JsonSerializer.Serialize(key) +
                       ",\"collection\":\"libraryItem\",\"ids\":[],\"all\":true}";
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "datastoreGet");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Stremio couldn't load your library. Check your connection and try again.");
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (HasError(root) || !root.TryGetProperty("result", out var result))
                throw new InvalidOperationException("Stremio rejected the library request. Reconnect your account and try again.");

            var entries = new List<MediaItem>();
            CollectItems(result, entries);
            return entries.GroupBy(x => x.Type + "|" + x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
        }

        public async Task<IReadOnlyList<MediaItem>> GetDiscoverAsync(string mediaType, CancellationToken cancellationToken, string? searchQuery = null)
        {
            if (mediaType != "movie" && mediaType != "series")
                throw new ArgumentException("Discover supports movie or series catalogs.", nameof(mediaType));

            var sources = _addons.GetInstalled().Select(x => (x.Name, x.ManifestUrl)).ToList();
            if (!sources.Any(x => string.Equals(x.ManifestUrl, CinemetaManifestUrl, StringComparison.OrdinalIgnoreCase)))
                sources.Add(("Cinemeta", CinemetaManifestUrl));

            var results = new List<MediaItem>();
            var failures = 0;
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var manifestResponse = await _http.GetAsync(source.ManifestUrl, cancellationToken);
                    if (!manifestResponse.IsSuccessStatusCode) { failures++; continue; }
                    using var manifest = JsonDocument.Parse(await manifestResponse.Content.ReadAsStringAsync());
                    if (!manifest.RootElement.TryGetProperty("catalogs", out var catalogs) || catalogs.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var catalog in catalogs.EnumerateArray())
                    {
                        if (!string.Equals(ReadString(catalog, "type"), mediaType, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var id = ReadString(catalog, "id");
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        var supportsSearch = false;
                        if (catalog.TryGetProperty("extra", out var extras) && extras.ValueKind == JsonValueKind.Array)
                            supportsSearch = extras.EnumerateArray().Any(x => string.Equals(ReadString(x, "name"), "search", StringComparison.OrdinalIgnoreCase));

                        var manifestUri = new Uri(source.ManifestUrl);
                        var baseUri = new UriBuilder(manifestUri)
                        {
                            Path = manifestUri.AbsolutePath.Substring(0, manifestUri.AbsolutePath.Length - "manifest.json".Length),
                            Query = "", Fragment = ""
                        }.Uri;
                        var catalogUri = new Uri(baseUri, "catalog/" + Uri.EscapeDataString(mediaType) + "/" + Uri.EscapeDataString(id) + ".json");
                        if (!string.IsNullOrWhiteSpace(searchQuery) && supportsSearch)
                            catalogUri = new Uri(catalogUri.AbsoluteUri + "?search=" + Uri.EscapeDataString(searchQuery.Trim()));

                        using var response = await _http.GetAsync(catalogUri, cancellationToken);
                        if (!response.IsSuccessStatusCode) { failures++; continue; }
                        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        if (!doc.RootElement.TryGetProperty("metas", out var metas) || metas.ValueKind != JsonValueKind.Array)
                            continue;
                        foreach (var meta in metas.EnumerateArray())
                        {
                            var item = ParseMedia(meta);
                            if (item != null) results.Add(item);
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { failures++; } // One unavailable add-on must not block other catalogs.
            }

            var unique = results.GroupBy(x => x.Type + "|" + x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
            if (unique.Count == 0 && failures > 0)
                throw new InvalidOperationException("No add-on catalogs responded. Check your connection or installed add-ons.");
            return unique;
        }

        private static void CollectItems(JsonElement value, List<MediaItem> output)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in value.EnumerateArray()) CollectItems(child, output);
                return;
            }
            if (value.ValueKind != JsonValueKind.Object) return;
            if (value.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in items.EnumerateArray()) CollectItems(child, output);
                return;
            }
            var item = ParseMedia(value);
            if (item != null) output.Add(item);
        }

        private static MediaItem? ParseMedia(JsonElement entry)
        {
            if (entry.ValueKind != JsonValueKind.Object) return null;
            var item = entry;
            if (entry.TryGetProperty("item", out var nested) && nested.ValueKind == JsonValueKind.Object) item = nested;
            var id = ReadString(item, "id") ?? ReadString(item, "_id");
            var type = ReadString(item, "type");
            var name = ReadString(item, "name") ?? ReadString(item, "title");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name)) return null;
            return new MediaItem(id, type, name, ReadString(item, "poster") ?? "",
                ReadString(item, "releaseInfo") ?? ReadString(item, "year") ?? "",
                ReadString(item, "description") ?? "");
        }

        private static bool HasError(JsonElement root) =>
            root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error) &&
            error.ValueKind != JsonValueKind.Null && error.ValueKind != JsonValueKind.False &&
            !(error.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(error.GetString()));

        private static string? ReadString(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString()
                : value.ValueKind == JsonValueKind.Number ? value.ToString() : null
                : null;
    }
}