using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace WatchStream.Xbox.Services
{
    public sealed class AddonResource
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Types { get; set; } = new List<string>();
        public List<string> IdPrefixes { get; set; } = new List<string>();
    }
    public sealed class CatalogExtra
    {
        public string Name { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public List<string> Options { get; set; } = new List<string>();
        public int OptionsLimit { get; set; } = 1;
    }
    public sealed class AddonCatalog
    {
        public string Type { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<CatalogExtra> Extra { get; set; } = new List<CatalogExtra>();
    }
    public sealed class InstalledAddon
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ManifestUrl { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<string> Types { get; set; } = new List<string>();
        public List<string> IdPrefixes { get; set; } = new List<string>();
        public List<AddonResource> Resources { get; set; } = new List<AddonResource>();
        public List<AddonCatalog> Catalogs { get; set; } = new List<AddonCatalog>();
        public bool SupportsStreams => Resources.Any(x => string.Equals(x.Name, "stream", StringComparison.OrdinalIgnoreCase));
        public bool SupportsCatalogs => Resources.Any(x => string.Equals(x.Name, "catalog", StringComparison.OrdinalIgnoreCase)) && Catalogs.Count > 0;
        public string Capabilities => (SupportsCatalogs ? "Catalogs" : "No catalogs") + " · " + (SupportsStreams ? "Streams" : "No streams");
        public bool SupportsResource(string name, string type, string? id)
        {
            return Resources.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
                (x.Types.Count == 0 ? Types.Count == 0 || Types.Contains(type, StringComparer.OrdinalIgnoreCase) : x.Types.Contains(type, StringComparer.OrdinalIgnoreCase)) &&
                (x.IdPrefixes.Count == 0 ? IdPrefixes.Count == 0 || id == null || IdPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal)) : id != null && x.IdPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))));
        }
    }
    public sealed class StreamSource
    {
        public string Name { get; set; } = "Stream";
        public string Url { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AddonName { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(Quality) ? Name : Name + "  •  " + Quality;
    }

    /// <summary>Installs public HTTPS Stremio add-ons from their manifest and resolves documented stream resources.</summary>
    public sealed class StremioAddonService
    {
        private const string SettingsKey = "WatchStream.InstalledAddons.v1";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public IReadOnlyList<InstalledAddon> GetInstalled()
        {
            var raw = ApplicationData.Current.LocalSettings.Values[SettingsKey] as string;
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<InstalledAddon>();
            try
            {
                return JsonSerializer.Deserialize<List<InstalledAddon>>(raw, WatchStreamJson.Options) ?? new List<InstalledAddon>();
            }
            catch { return Array.Empty<InstalledAddon>(); }
        }

        public async Task<InstalledAddon> InstallAsync(string manifestUrl, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(manifestUrl?.Trim(), UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Enter a valid add-on manifest URL ending in /manifest.json.");
            if (uri.Scheme != Uri.UriSchemeHttps &&
                !(uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException("Add-ons must use HTTPS. HTTP is allowed only for localhost development.");
            if (!uri.AbsolutePath.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase) &&
                !uri.AbsolutePath.Equals("/manifest.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The URL must point to the add-on's /manifest.json endpoint.");

            using (var response = await Http.GetAsync(uri, cancellationToken))
            {
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("The add-on manifest could not be downloaded.");
                using (var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
                {
                    var root = doc.RootElement;
                    var id = GetString(root, "id");
                    var name = GetString(root, "name");
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                        throw new InvalidOperationException("This manifest is missing the required id or name fields.");
                    var addonTypes = ReadStringArray(root, "types");
                    var addonPrefixes = ReadStringArray(root, "idPrefixes");
                    var resources = new List<AddonResource>();
                    if (root.TryGetProperty("resources", out var resourceArray) && resourceArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var resource in resourceArray.EnumerateArray())
                        {
                            var resourceName = resource.ValueKind == JsonValueKind.String ? resource.GetString() : GetString(resource, "name");
                            if (string.IsNullOrWhiteSpace(resourceName)) continue;
                            resources.Add(new AddonResource { Name = resourceName,
                                Types = resource.ValueKind == JsonValueKind.Object ? ReadStringArray(resource, "types") : new List<string>(),
                                IdPrefixes = resource.ValueKind == JsonValueKind.Object ? ReadStringArray(resource, "idPrefixes") : new List<string>() });
                        }
                    }
                    var catalogs = new List<AddonCatalog>();
                    if (root.TryGetProperty("catalogs", out var catalogArray) && catalogArray.ValueKind == JsonValueKind.Array)
                    foreach (var catalog in catalogArray.EnumerateArray())
                    {
                        var catalogId = GetString(catalog, "id"); var catalogType = GetString(catalog, "type");
                        if (string.IsNullOrWhiteSpace(catalogId) || string.IsNullOrWhiteSpace(catalogType)) continue;
                        var extras = new List<CatalogExtra>();
                        if (catalog.TryGetProperty("extra", out var extraArray) && extraArray.ValueKind == JsonValueKind.Array)
                        foreach (var extra in extraArray.EnumerateArray())
                        {
                            var extraName = GetString(extra, "name"); if (string.IsNullOrWhiteSpace(extraName)) continue;
                            extras.Add(new CatalogExtra { Name = extraName, IsRequired = extra.TryGetProperty("isRequired", out var required) && required.ValueKind == JsonValueKind.True,
                                Options = ReadStringArray(extra, "options"), OptionsLimit = extra.TryGetProperty("optionsLimit", out var limit) && limit.TryGetInt32(out var count) ? count : 1 });
                        }
                        catalogs.Add(new AddonCatalog { Id = catalogId, Type = catalogType, Name = GetString(catalog, "name") ?? catalogId, Extra = extras });
                    }
                    var addon = new InstalledAddon { Id = id, Name = name, Description = GetString(root, "description") ?? string.Empty,
                        ManifestUrl = uri.AbsoluteUri, Version = GetString(root, "version") ?? string.Empty, Types = addonTypes,
                        IdPrefixes = addonPrefixes, Resources = resources, Catalogs = catalogs };
                    var installed = GetInstalled().Where(x => !string.Equals(x.ManifestUrl, addon.ManifestUrl, StringComparison.OrdinalIgnoreCase)).ToList();
                    installed.Add(addon);
                    ApplicationData.Current.LocalSettings.Values[SettingsKey] = JsonSerializer.Serialize(installed, WatchStreamJson.Options);
                    return addon;
                }
            }
        }

        /// <summary>
        /// Pulls the signed-in user's Stremio add-on collection. The official API's
        /// addonCollectionGet method returns add-on descriptors; this client imports only
        /// HTTPS descriptors that point at a modern /manifest.json endpoint.
        /// Legacy /stremio/v1 endpoints are left untouched rather than guessed at.
        /// </summary>
        public async Task<int> SyncFromAccountAsync(WatchStream.Core.Contracts.ISessionStore sessionStore, CancellationToken cancellationToken)
        {
            if (sessionStore == null) throw new ArgumentNullException(nameof(sessionStore));
            var authKey = await sessionStore.ReadSessionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(authKey))
                throw new InvalidOperationException("Connect your Stremio account before syncing its add-ons.");

            // APIRequest is internally tagged in stremio-core; the method name is part of the JSON contract.
            var body = JsonSerializer.Serialize(new { type = "AddonCollectionGet", authKey, update = true, addFromURL = Array.Empty<string>() }, WatchStreamJson.Options);
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.strem.io/api/addonCollectionGet"))
            {
                request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                using (var response = await Http.SendAsync(request, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Stremio could not return your add-on collection.");
                    using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
                    {
                        var root = document.RootElement;
                        if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null &&
                            !(error.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(error.GetString())))
                            throw new InvalidOperationException("Stremio rejected the add-on sync request.");
                        if (!root.TryGetProperty("result", out var result) ||
                            !result.TryGetProperty("addons", out var addons) || addons.ValueKind != JsonValueKind.Array)
                            throw new InvalidOperationException("Stremio returned an unexpected add-on collection response.");

                        var imported = 0;
                        foreach (var descriptor in addons.EnumerateArray())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var transportUrl = GetString(descriptor, "transportUrl") ?? GetString(descriptor, "transport_url");
                            if (!Uri.TryCreate(transportUrl, UriKind.Absolute, out var addonUri) ||
                                addonUri.Scheme != Uri.UriSchemeHttps ||
                                !addonUri.AbsolutePath.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase))
                                continue;

                            try
                            {
                                await InstallAsync(addonUri.AbsoluteUri, cancellationToken);
                                imported++;
                            }
                            catch (OperationCanceledException) { throw; }
                            catch { /* Keep syncing other valid descriptors if one add-on is offline. */ }
                        }

                        return imported;
                    }
                }
            }
        }

        public void Remove(string manifestUrl)
        {
            var installed = GetInstalled().Where(x => !string.Equals(x.ManifestUrl, manifestUrl, StringComparison.OrdinalIgnoreCase)).ToList();
            ApplicationData.Current.LocalSettings.Values[SettingsKey] = JsonSerializer.Serialize(installed, WatchStreamJson.Options);
        }

        public async Task<IReadOnlyList<StreamSource>> ResolveStreamsAsync(string type, string itemId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("A content type and item ID are required.");

            var streamTasks = GetInstalled()
                .Where(x => x.SupportsResource("stream", type, itemId))
                .Select(async addon =>
                {
                    try
                    {
                        var streamUrl = BuildResourceUri(addon.ManifestUrl, "stream", type, itemId, null);
                        using var response = await Http.GetAsync(streamUrl, cancellationToken);
                        if (!response.IsSuccessStatusCode) return Enumerable.Empty<StreamSource>();

                        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        if (!doc.RootElement.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
                            return Enumerable.Empty<StreamSource>();

                        var foundStreams = new List<StreamSource>();
                        foreach (var stream in streams.EnumerateArray())
                        {
                            var url = GetString(stream, "url");
                            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                                (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)) continue;

                            var title = GetString(stream, "name") ?? GetString(stream, "title") ?? "Stream";
                            var description = GetString(stream, "description") ?? string.Empty;
                            var behavior = GetString(stream, "quality");
                            if (string.IsNullOrWhiteSpace(behavior) &&
                                stream.TryGetProperty("behaviorHints", out var hints))
                                behavior = GetString(hints, "quality");

                            foundStreams.Add(new StreamSource
                            {
                                Name = title,
                                Url = parsed.AbsoluteUri,
                                Description = description,
                                AddonName = addon.Name,
                                Quality = behavior ?? string.Empty
                            });
                        }
                        return foundStreams;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { return Enumerable.Empty<StreamSource>(); }
                });

            var results = await Task.WhenAll(streamTasks);
            return results.SelectMany(x => x).ToList();
        }

        public async Task<string> RequestCatalogAsync(InstalledAddon addon, AddonCatalog catalog, IDictionary<string, string>? extra, CancellationToken cancellationToken)
        {
            if (addon == null || catalog == null) throw new ArgumentNullException(addon == null ? nameof(addon) : nameof(catalog));
            if (!addon.SupportsResource("catalog", catalog.Type, null)) throw new InvalidOperationException("This add-on does not declare the requested catalog resource.");
            var supplied = extra ?? new Dictionary<string, string>();
            foreach (var required in catalog.Extra.Where(x => x.IsRequired))
                if (!supplied.TryGetValue(required.Name, out var value) || string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Catalog requires the extra parameter: " + required.Name);
            using var response = await Http.GetAsync(BuildResourceUri(addon.ManifestUrl, "catalog", catalog.Type, catalog.Id, supplied), cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string?> RequestMetadataAsync(string type, string itemId, CancellationToken cancellationToken)
        {
            foreach (var addon in GetInstalled().Where(x => x.SupportsResource("meta", type, itemId)))
            {
                try
                {
                    using var response = await Http.GetAsync(BuildResourceUri(addon.ManifestUrl, "meta", type, itemId, null), cancellationToken);
                    if (!response.IsSuccessStatusCode) continue;
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object) return meta.GetRawText();
                }
                catch (OperationCanceledException) { throw; }
                catch { /* Try the next compatible metadata provider. */ }
            }
            return null;
        }

        public static Uri BuildResourceUri(string manifestUrl, string resource, string type, string id, IDictionary<string, string>? extra)
        {
            var manifest = new Uri(manifestUrl);
            var basePath = manifest.AbsolutePath.Substring(0, manifest.AbsolutePath.Length - "manifest.json".Length);
            var route = resource.TrimEnd('/') + "/" + Uri.EscapeDataString(type) + "/" + Uri.EscapeDataString(id);
            if (extra != null && extra.Count > 0)
                route += "/" + string.Join("&", extra.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
            var builder = new UriBuilder(manifest) { Path = basePath + route + ".json", Query = manifest.Query.TrimStart('?') , Fragment = string.Empty };
            return builder.Uri;
        }
        private static List<string> ReadStringArray(JsonElement element, string property)
        {
            var values = new List<string>();
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
                foreach (var value in array.EnumerateArray()) if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())) values.Add(value.GetString()!);
            return values;
        }
        private static string? GetString(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object &&
                   element.TryGetProperty(property, out var value) &&
                   value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }
}