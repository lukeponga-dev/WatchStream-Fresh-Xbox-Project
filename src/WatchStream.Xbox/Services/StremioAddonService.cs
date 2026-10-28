using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Windows.Security.Credentials;
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
        public string InstallSource { get; set; } = "Manual";
        public string SyncStatus { get; set; } = "Available";
        public List<string> Types { get; set; } = new List<string>();
        public List<string> IdPrefixes { get; set; } = new List<string>();
        public List<AddonResource> Resources { get; set; } = new List<AddonResource>();
        public List<AddonCatalog> Catalogs { get; set; } = new List<AddonCatalog>();
        public bool SupportsStreams => Resources.Any(x => string.Equals(x.Name, "stream", StringComparison.OrdinalIgnoreCase));
        public bool SupportsCatalogs => Resources.Any(x => string.Equals(x.Name, "catalog", StringComparison.OrdinalIgnoreCase)) && Catalogs.Count > 0;
        public string Capabilities => (SupportsCatalogs ? "Catalogs" : "No catalogs") + " | " + (SupportsStreams ? "Streams" : "No streams");
        public bool SupportsResource(string name, string type, string? id)
        {
            return Resources.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
                (x.Types.Count == 0 ? Types.Contains(type, StringComparer.OrdinalIgnoreCase) : x.Types.Contains(type, StringComparer.OrdinalIgnoreCase)) &&
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
        public string DisplayName => string.IsNullOrWhiteSpace(Quality) ? Name : Name + " - " + Quality;
    }

    /// <summary>Installs public HTTPS Stremio add-ons from their manifest and resolves documented stream resources.</summary>
    public sealed class StremioAddonService
    {
        private const string SettingsKey = "WatchStream.InstalledAddons.v1";
        private const string AddonUrlVaultResource = "WatchStream.AddonManifestUrl.v1";
        private static readonly HttpClient Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly object DirectoryCacheLock = new object();
        private static readonly Dictionary<string, (DateTimeOffset ExpiresAt, IReadOnlyList<AddonDirectoryEntry> Results)> DirectoryCache = new Dictionary<string, (DateTimeOffset, IReadOnlyList<AddonDirectoryEntry>)>(StringComparer.Ordinal);

        private static string InstalledAddonsPath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "installed-addons.json");

        public IReadOnlyList<InstalledAddon> GetInstalled()
        {
            try
            {
                if (File.Exists(InstalledAddonsPath))
                {
                    var fileJson = File.ReadAllText(InstalledAddonsPath);
                    var vault = new PasswordVault();
                    var restoredJson = StremioAddonStoreCodec.RestoreSerializedUrls(fileJson, key =>
                    {
                        try
                        {
                            var credential = vault.FindAllByResource(AddonUrlVaultResource)
                                .FirstOrDefault(x => string.Equals(x.UserName, key, StringComparison.Ordinal));
                            if (credential == null) return null;
                            credential.RetrievePassword();
                            return credential.Password;
                        }
                        catch { return null; }
                    }, out var needsMigration);

                    var addons = JsonSerializer.Deserialize<List<InstalledAddon>>(restoredJson, WatchStreamJson.Options) ?? new List<InstalledAddon>();
                    // Manifest URLs identify public add-on endpoints; they are not account credentials.
                    // Migrate legacy vault-backed records to the app-local JSON store and free vault slots.
                    if (needsMigration) SaveInstalled(addons);
                    return addons;
                }

                // Migrate older installs from LocalSettings, which has a small per-value size limit.
                var legacyJson = ApplicationData.Current.LocalSettings.Values[SettingsKey] as string;
                if (string.IsNullOrWhiteSpace(legacyJson)) return Array.Empty<InstalledAddon>();
                var legacyAddons = JsonSerializer.Deserialize<List<InstalledAddon>>(legacyJson, WatchStreamJson.Options) ?? new List<InstalledAddon>();
                SaveInstalled(legacyAddons);
                ApplicationData.Current.LocalSettings.Values.Remove(SettingsKey);
                return legacyAddons;
            }
            catch { return Array.Empty<InstalledAddon>(); }
        }

        private static void SaveInstalled(IReadOnlyList<InstalledAddon> addons)
        {
            var safeAddons = addons.Where(x => x != null && !string.IsNullOrWhiteSpace(x.ManifestUrl)).ToList();
            // Add-on manifest URLs are public endpoint identifiers, not authentication secrets.
            // Keeping one PasswordVault credential per URL exhausts the per-app credential quota.
            File.WriteAllText(InstalledAddonsPath, JsonSerializer.Serialize(safeAddons, WatchStreamJson.Options));
            ApplicationData.Current.LocalSettings.Values.Remove(SettingsKey);

            // Clean up credentials created by older WatchStream builds. Do not touch unrelated vault entries.
            try
            {
                var vault = new PasswordVault();
                foreach (var credential in vault.FindAllByResource(AddonUrlVaultResource).ToList())
                    vault.Remove(credential);
            }
            catch { /* No legacy credentials is a normal first-run state. */ }
        }

        public sealed class AddonDirectoryEntry
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string ManifestUrl { get; set; } = string.Empty;
            public string PageUrl { get; set; } = string.Empty;
            public string ConfigureUrl { get; set; } = string.Empty;
            public int Stars { get; set; }
            public string Categories { get; set; } = string.Empty;
            public string PopularityLabel => Stars.ToString("N0") + " stars";
        }

        /// <summary>Searches the public Stremio Addons community directory. API is experimental and read-only.</summary>
        public async Task<IReadOnlyList<AddonDirectoryEntry>> SearchDirectoryAsync(string? search, CancellationToken cancellationToken)
        {
#if !WATCHSTREAM_ADDON_DIRECTORY_PERMISSION_GRANTED
            await Task.CompletedTask;
            throw new InvalidOperationException("The Stremio Addons directory is disabled until written permission for this integration is confirmed.");
#else
            var cacheKey = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim().ToLowerInvariant();
            lock (DirectoryCacheLock)
            {
                if (DirectoryCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
                    return cached.Results;
                DirectoryCache.Remove(cacheKey);
            }

            var query = "https://stremio-addons.net/api/v0/addons?sort_by=stars&order=desc&limit=25&nsfw=exclude";
            if (!string.IsNullOrWhiteSpace(search))
                query += "&search=" + Uri.EscapeDataString(search.Trim());

            using (var response = await GetSafeAsync(new Uri(query), cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("The Stremio Addons directory is temporarily unavailable.");
                using (var document = JsonDocument.Parse(await ReadResponseTextAsync(response, cancellationToken)))
                {
                    var root = document.RootElement;
                    if (!root.TryGetProperty("addons", out var addons) || addons.ValueKind != JsonValueKind.Array)
                        throw new InvalidOperationException("The directory returned an unexpected response.");
                    var results = new List<AddonDirectoryEntry>();
                    foreach (var item in addons.EnumerateArray())
                    {
                        var manifest = item.TryGetProperty("manifest", out var value) && value.ValueKind == JsonValueKind.Object
                            ? value : default;
                        var name = GetString(manifest, "name");
                        var manifestUrl = GetString(item, "manifestUrl");
                        if (string.IsNullOrWhiteSpace(name) || !StremioAddonSecurity.IsValidManifestUri(manifestUrl))
                            continue;
                        var categories = new List<string>();
                        if (item.TryGetProperty("categories", out var categoryArray) && categoryArray.ValueKind == JsonValueKind.Array)
                            foreach (var category in categoryArray.EnumerateArray())
                            {
                                var categoryName = GetString(category, "name");
                                if (!string.IsNullOrWhiteSpace(categoryName)) categories.Add(categoryName);
                            }
                        var stars = item.TryGetProperty("stars", out var starsElement) && starsElement.TryGetInt32(out var starCount)
                            ? starCount : 0;
                        results.Add(new AddonDirectoryEntry
                        {
                            Name = name,
                            Description = GetString(manifest, "description") ?? string.Empty,
                            ManifestUrl = manifestUrl!,
                            PageUrl = GetString(item, "url") ?? "https://stremio-addons.net/",
                            ConfigureUrl = GetString(item, "configureUrl") ?? string.Empty,
                            Stars = stars,
                            Categories = string.Join(" · ", categories)
                        });
                    }
                    var snapshot = results.AsReadOnly();
                    lock (DirectoryCacheLock)
                    {
                        if (DirectoryCache.Count >= 32) DirectoryCache.Clear();
                        DirectoryCache[cacheKey] = (DateTimeOffset.UtcNow.AddHours(12), snapshot);
                    }
                    return snapshot;
                }
            }
#endif
        }

        public async Task<InstalledAddon> InstallAsync(string manifestUrl, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(manifestUrl?.Trim(), UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Enter a valid add-on manifest URL ending in /manifest.json.");
            StremioAddonSecurity.ValidateRequestUri(uri, allowLocalhostHttp: uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp);
            if (!uri.AbsolutePath.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase) &&
                !uri.AbsolutePath.Equals("/manifest.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The URL must point to the add-on's /manifest.json endpoint.");

            using (var response = await GetSafeAsync(uri, cancellationToken, allowLocalhostHttp: uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp))
            {
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("The add-on manifest could not be downloaded.");
                using (var doc = JsonDocument.Parse(await ReadResponseTextAsync(response, cancellationToken)))
                {
                    var root = doc.RootElement;
                    StremioAddonSecurity.ValidateManifest(root);
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
                    SaveInstalled(installed);
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
                    using (var document = JsonDocument.Parse(await ReadResponseTextAsync(response, cancellationToken)))
                    {
                        var root = document.RootElement;
                        if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null &&
                            !(error.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(error.GetString())))
                            throw new InvalidOperationException("Stremio rejected the add-on sync request.");
                        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object ||
                            !result.TryGetProperty("addons", out var addons) || addons.ValueKind != JsonValueKind.Array)
                            throw new InvalidOperationException("Stremio returned an unexpected add-on collection response.");

                        // The account collection is authoritative for account-managed add-ons. Keep manual
                        // installs, but replace the previous account-managed set with this response.
                        var beforeSync = GetInstalled().ToList();
                        var accountAddons = new List<InstalledAddon>();
                        var imported = 0;

                        foreach (var descriptor in addons.EnumerateArray())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var transportUrl = GetString(descriptor, "transportUrl")
                                ?? GetString(descriptor, "transport_url")
                                ?? GetString(descriptor, "url")
                                ?? string.Empty;
                            var descriptorId = GetString(descriptor, "_id") ?? GetString(descriptor, "id");
                            var descriptorName = GetString(descriptor, "name");
                            var existing = beforeSync.FirstOrDefault(x => SameAddonUrl(x.ManifestUrl, transportUrl));
                            var isManifestUrl = Uri.TryCreate(transportUrl, UriKind.Absolute, out var addonUri)
                                && addonUri.Scheme == Uri.UriSchemeHttps
                                && (addonUri.AbsolutePath.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase)
                                    || addonUri.AbsolutePath.Equals("/manifest.json", StringComparison.OrdinalIgnoreCase));

                            if (!isManifestUrl)
                            {
                                var legacy = transportUrl.Contains("/stremio/v1", StringComparison.OrdinalIgnoreCase)
                                    || transportUrl.Contains("/stremio/v2", StringComparison.OrdinalIgnoreCase);
                                accountAddons.Add(existing ?? new InstalledAddon
                                {
                                    Id = descriptorId ?? transportUrl,
                                    Name = descriptorName ?? (legacy ? "Legacy Stremio add-on" : "Unsupported add-on"),
                                    ManifestUrl = transportUrl,
                                    InstallSource = "Account",
                                    SyncStatus = legacy ? "Legacy" : "Unsupported",
                                    Description = legacy
                                        ? "This account add-on uses a legacy transport that WatchStream cannot currently load."
                                        : "This account entry does not provide a supported HTTPS /manifest.json URL."
                                });
                                if (existing != null && !string.Equals(existing.InstallSource, "Manual", StringComparison.OrdinalIgnoreCase))
                                {
                                    existing.InstallSource = "Account";
                                    existing.SyncStatus = legacy ? "Legacy" : "Unsupported";
                                    existing.Description = legacy
                                        ? "This account add-on uses a legacy transport that WatchStream cannot currently load."
                                        : "This account entry does not provide a supported HTTPS /manifest.json URL.";
                                    accountAddons[accountAddons.Count - 1] = existing;
                                }
                                continue;
                            }

                            try
                            {
                                var installedAddon = await InstallAsync(addonUri!.AbsoluteUri, cancellationToken);
                                var wasManual = existing != null && string.Equals(existing.InstallSource, "Manual", StringComparison.OrdinalIgnoreCase);
                                installedAddon.InstallSource = wasManual ? "Manual" : "Account";
                                installedAddon.SyncStatus = "Available";
                                accountAddons.Add(installedAddon);
                                imported++;
                            }
                            catch (OperationCanceledException) { throw; }
                            catch (Exception)
                            {
                                var unavailable = existing ?? new InstalledAddon
                                {
                                    Id = descriptorId ?? addonUri!.AbsoluteUri,
                                    Name = descriptorName ?? "Unavailable add-on",
                                    ManifestUrl = addonUri!.AbsoluteUri,
                                    InstallSource = "Account"
                                };
                                if (!string.Equals(unavailable.InstallSource, "Manual", StringComparison.OrdinalIgnoreCase))
                                    unavailable.InstallSource = "Account";
                                unavailable.SyncStatus = "Unavailable";
                                unavailable.Description = "The add-on is in your Stremio account but its manifest could not be loaded. Check the connection and try again.";
                                accountAddons.Add(unavailable);
                            }
                        }

                        // Deduplicate by normalized URL. Existing manual installs survive even when
                        // removed from the account collection; stale account-only entries do not.
                        var manualAddons = beforeSync
                            .Where(x => string.Equals(x.InstallSource, "Manual", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        var reconciled = new List<InstalledAddon>(manualAddons);
                        foreach (var accountAddon in accountAddons)
                        {
                            var duplicate = reconciled.FindIndex(x => SameAddonUrl(x.ManifestUrl, accountAddon.ManifestUrl));
                            if (duplicate >= 0)
                            {
                                if (string.Equals(reconciled[duplicate].InstallSource, "Manual", StringComparison.OrdinalIgnoreCase))
                                    continue;
                                reconciled[duplicate] = accountAddon;
                            }
                            else
                            {
                                reconciled.Add(accountAddon);
                            }
                        }

                        SaveInstalled(reconciled);
                        return imported;
                    }
                }
            }
        }

        public void Remove(string manifestUrl)
        {
            var installed = GetInstalled().Where(x => !string.Equals(x.ManifestUrl, manifestUrl, StringComparison.OrdinalIgnoreCase)).ToList();
            SaveInstalled(installed);
        }

        public async Task<IReadOnlyList<StreamSource>> ResolveStreamsAsync(string type, string itemId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("A content type and item ID are required.");

            // Stremio series metadata may contain distinct episode/video IDs. Stream resources
            // must be requested with each video ID, not only the parent series ID.
            var videoIds = new List<string> { itemId };
            if (string.Equals(type, "series", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var metadataJson = await RequestMetadataAsync(type, itemId, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(metadataJson))
                    {
                        using var metadata = JsonDocument.Parse(metadataJson);
                        if (metadata.RootElement.TryGetProperty("videos", out var videos) && videos.ValueKind == JsonValueKind.Array)
                        {
                            var episodeIds = videos.EnumerateArray()
                                .Select(video => GetString(video, "id"))
                                .Where(id => !string.IsNullOrWhiteSpace(id))
                                .Select(id => id!)
                                .Distinct(StringComparer.Ordinal)
                                .ToList();
                            if (episodeIds.Count > 0) videoIds = episodeIds;
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { /* Fall back to the parent ID if episode metadata is unavailable. */ }
            }

            var streamTasks = GetInstalled()
                .SelectMany(addon => videoIds
                    .Where(videoId => addon.SupportsResource("stream", type, videoId))
                    .Select(videoId => new { Addon = addon, VideoId = videoId }))
                .Select(async target =>
                {
                    var addon = target.Addon;
                    var videoId = target.VideoId;
                    try
                    {
                        var streamUrl = BuildResourceUri(addon.ManifestUrl, "stream", type, videoId, null);
                        using var response = await GetSafeAsync(streamUrl, cancellationToken, allowLocalhostHttp: streamUrl.IsLoopback && streamUrl.Scheme == Uri.UriSchemeHttp);
                        if (!response.IsSuccessStatusCode) return Enumerable.Empty<StreamSource>();

                        var parsedStreams = StremioProtocolParser.ParseStreams(await ReadResponseTextAsync(response, cancellationToken));
                        var foundStreams = new List<StreamSource>();
                        foreach (var stream in parsedStreams)
                        {
                            if (!StremioAddonSecurity.IsValidStreamUri(stream.Url)) continue;
                            try
                            {
                                await ValidateResolvedHostAsync(new Uri(stream.Url), cancellationToken);
                            }
                            catch (OperationCanceledException) { throw; }
                            catch { continue; }

                            foundStreams.Add(new StreamSource
                            {
                                Name = stream.Name, Url = stream.Url, Description = stream.Description,
                                AddonName = addon.Name, Quality = stream.Quality
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
            var catalogUri = BuildResourceUri(addon.ManifestUrl, "catalog", catalog.Type, catalog.Id, supplied);
            using var response = await GetSafeAsync(catalogUri, cancellationToken, allowLocalhostHttp: catalogUri.IsLoopback && catalogUri.Scheme == Uri.UriSchemeHttp);
            response.EnsureSuccessStatusCode();
            return await ReadResponseTextAsync(response, cancellationToken);
        }

        public async Task<string?> RequestMetadataAsync(string type, string itemId, CancellationToken cancellationToken)
        {
            foreach (var addon in GetInstalled().Where(x => x.SupportsResource("meta", type, itemId)))
            {
                try
                {
                    var metadataUri = BuildResourceUri(addon.ManifestUrl, "meta", type, itemId, null);
                    using var response = await GetSafeAsync(metadataUri, cancellationToken, allowLocalhostHttp: metadataUri.IsLoopback && metadataUri.Scheme == Uri.UriSchemeHttp);
                    if (!response.IsSuccessStatusCode) continue;
                    var json = await ReadResponseTextAsync(response, cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object) return meta.GetRawText();
                }
                catch (OperationCanceledException) { throw; }
                catch { /* Try the next compatible metadata provider. */ }
            }
            return null;
        }

        private static async Task<HttpResponseMessage> GetSafeAsync(Uri uri, CancellationToken cancellationToken, bool allowLocalhostHttp = false)
        {
            var current = StremioAddonSecurity.ValidateRequestUri(uri, allowLocalhostHttp);
            for (var redirects = 0; ; redirects++)
            {
                if (!(allowLocalhostHttp && current.IsLoopback && current.Scheme == Uri.UriSchemeHttp))
                    await ValidateResolvedHostAsync(current, cancellationToken);
                var response = await Http.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if ((int)response.StatusCode < 300 || (int)response.StatusCode >= 400)
                    return response;

                var location = response.Headers.Location;
                response.Dispose();
                current = StremioAddonSecurity.ValidateRedirectTarget(current, location, redirects);
            }
        }

        private static async Task ValidateResolvedHostAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(uri.DnsSafeHost, out var literalAddress))
            {
                if (StremioAddonSecurity.IsNonPublicAddress(literalAddress))
                    throw new InvalidOperationException("Add-on URLs must not target private or reserved network addresses.");
                return;
            }

            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            if (addresses.Length == 0 || addresses.Any(StremioAddonSecurity.IsNonPublicAddress))
                throw new InvalidOperationException("The add-on hostname resolves to a private or reserved network address.");
        }

        private static Task<string> ReadResponseTextAsync(HttpResponseMessage response, CancellationToken cancellationToken, int maxBytes = 2 * 1024 * 1024)
        {
            return StremioAddonSecurity.ReadBoundedResponseTextAsync(response.Content, cancellationToken, maxBytes);
        }

        public static Uri BuildResourceUri(string manifestUrl, string resource, string type, string id, IDictionary<string, string>? extra)
        {
            return StremioAddonSecurity.BuildResourceUri(manifestUrl, resource, type, id, extra);
        }
        private static bool SameAddonUrl(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            if (Uri.TryCreate(left.Trim(), UriKind.Absolute, out var leftUri) &&
                Uri.TryCreate(right.Trim(), UriKind.Absolute, out var rightUri))
            {
                var leftKey = leftUri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped).TrimEnd('/');
                var rightKey = rightUri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped).TrimEnd('/');
                return string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
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
