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
                        if (!string.Equals(ReadString(catalog, "type"), mediaType, StringComparison.OrdinalIgnoreCase) || !SupportsManifestResource(manifest.RootElement, "catalog", mediaType, null))
                            continue;
                        var id = ReadString(catalog, "id");
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var extraDefinitions = catalog.TryGetProperty("extra", out var definitions) && definitions.ValueKind == JsonValueKind.Array
                            ? definitions.EnumerateArray().ToList() : new List<JsonElement>();
                        if (!string.IsNullOrWhiteSpace(searchQuery) && extraDefinitions.Any(x => string.Equals(ReadString(x, "name"), "search", StringComparison.OrdinalIgnoreCase)))
                            extras["search"] = searchQuery.Trim();
                        if (extraDefinitions.Any(x => x.TryGetProperty("isRequired", out var required) && required.ValueKind == JsonValueKind.True &&
                            (!extras.TryGetValue(ReadString(x, "name") ?? string.Empty, out var supplied) || string.IsNullOrWhiteSpace(supplied)))) continue;
                        var catalogUri = StremioAddonService.BuildResourceUri(source.ManifestUrl, "catalog", mediaType, id, extras);                        using var response = await _http.GetAsync(catalogUri, cancellationToken);
                        if (!response.IsSuccessStatusCode) { failures++; continue; }
                        var parsedMetas = StremioProtocolParser.ParseCatalogMetas(await response.Content.ReadAsStringAsync());
                        foreach (var meta in parsedMetas)
                            results.Add(new MediaItem(meta.Id, meta.Type, meta.Name, meta.Poster, meta.Year, meta.Description));
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

        private static bool SupportsManifestResource(JsonElement manifest, string name, string type, string? id)
        {
            if (!manifest.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array) return false;
            var manifestTypes = manifest.TryGetProperty("types", out var types) && types.ValueKind == JsonValueKind.Array
                ? types.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : new List<string>();
            var prefixes = manifest.TryGetProperty("idPrefixes", out var ids) && ids.ValueKind == JsonValueKind.Array
                ? ids.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : new List<string>();
            foreach (var resource in resources.EnumerateArray())
            {
                var resourceName = resource.ValueKind == JsonValueKind.String ? resource.GetString() : ReadString(resource, "name");
                if (!string.Equals(resourceName, name, StringComparison.OrdinalIgnoreCase)) continue;
                var resourceTypes = resource.ValueKind == JsonValueKind.Object && resource.TryGetProperty("types", out var rt) && rt.ValueKind == JsonValueKind.Array
                    ? rt.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : manifestTypes;
                var resourcePrefixes = resource.ValueKind == JsonValueKind.Object && resource.TryGetProperty("idPrefixes", out var rp) && rp.ValueKind == JsonValueKind.Array
                    ? rp.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : prefixes;
                if (resourceTypes.Count > 0 && !resourceTypes.Contains(type, StringComparer.OrdinalIgnoreCase)) continue;
                if (name != "catalog" && resourcePrefixes.Count > 0 && (id == null || !resourcePrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal)))) continue;
                return true;
            }
            return false;
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
