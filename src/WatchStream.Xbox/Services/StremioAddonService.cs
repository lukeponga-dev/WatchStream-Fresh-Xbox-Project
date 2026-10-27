using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace WatchStream.Xbox.Services
{
    public sealed class InstalledAddon
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ManifestUrl { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool SupportsStreams { get; set; }
        public bool SupportsCatalogs { get; set; }
        public string Capabilities => (SupportsCatalogs ? "Catalogs" : "No catalogs") + "  •  " + (SupportsStreams ? "Streams" : "No streams");
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
                return JsonSerializer.Deserialize<List<InstalledAddon>>(raw) ?? new List<InstalledAddon>();
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
                    var resources = new List<string>();
                    if (root.TryGetProperty("resources", out var resourceArray) && resourceArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var resource in resourceArray.EnumerateArray())
                        {
                            if (resource.ValueKind == JsonValueKind.String) resources.Add(resource.GetString() ?? string.Empty);
                            else if (resource.ValueKind == JsonValueKind.Object && resource.TryGetProperty("name", out var resourceName))
                                resources.Add(resourceName.GetString() ?? string.Empty);
                        }
                    }
                    var addon = new InstalledAddon
                    {
                        Id = id,
                        Name = name,
                        Description = GetString(root, "description") ?? string.Empty,
                        ManifestUrl = uri.AbsoluteUri,
                        Version = GetString(root, "version") ?? string.Empty,
                        SupportsStreams = resources.Contains("stream", StringComparer.OrdinalIgnoreCase),
                        SupportsCatalogs = resources.Contains("catalog", StringComparer.OrdinalIgnoreCase)
                    };
                    var installed = GetInstalled().Where(x => !string.Equals(x.ManifestUrl, addon.ManifestUrl, StringComparison.OrdinalIgnoreCase)).ToList();
                    installed.Add(addon);
                    ApplicationData.Current.LocalSettings.Values[SettingsKey] = JsonSerializer.Serialize(installed);
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
            var body = JsonSerializer.Serialize(new { type = "AddonCollectionGet", authKey, update = true, addFromURL = Array.Empty<string>() });
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
            ApplicationData.Current.LocalSettings.Values[SettingsKey] = JsonSerializer.Serialize(installed);
        }

        public async Task<IReadOnlyList<StreamSource>> ResolveStreamsAsync(string type, string itemId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("A content type and item ID are required.");

            var streamTasks = GetInstalled()
                .Where(x => x.SupportsStreams)
                .Select(async addon =>
                {
                    try
                    {
                        var manifest = new Uri(addon.ManifestUrl);
                        var baseBuilder = new UriBuilder(manifest)
                        {
                            Path = manifest.AbsolutePath.Substring(0, manifest.AbsolutePath.Length - "manifest.json".Length),
                            Query = string.Empty,
                            Fragment = string.Empty
                        };
                        var baseUri = baseBuilder.Uri;
                        var streamUrl = new Uri(baseUri, "stream/" + Uri.EscapeDataString(type) + "/" + Uri.EscapeDataString(itemId) + ".json");

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

        private static string? GetString(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object &&
                   element.TryGetProperty(property, out var value) &&
                   value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }
}