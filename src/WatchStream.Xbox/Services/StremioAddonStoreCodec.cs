using System;
using System.Text.Json.Nodes;

namespace WatchStream.Xbox.Services
{
    /// <summary>Separates sensitive manifest URLs from the JSON add-on metadata persisted on disk.</summary>
    public static class StremioAddonStoreCodec
    {
        public static string ProtectSerializedUrls(string serializedAddons, Func<string, string> storeUrl)
        {
            if (storeUrl == null) throw new ArgumentNullException(nameof(storeUrl));
            var root = JsonNode.Parse(serializedAddons) as JsonArray
                ?? throw new InvalidOperationException("The installed add-on store must be a JSON array.");
            foreach (var node in root)
            {
                if (node is not JsonObject item) continue;
                var url = (string?)item["ManifestUrl"];
                if (string.IsNullOrWhiteSpace(url))
                    throw new InvalidOperationException("An installed add-on is missing its manifest URL.");
                var key = storeUrl(url);
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("The URL secret store did not return a key.");
                item["ManifestUrl"] = string.Empty;
                item["ManifestUrlVaultKey"] = key;
            }
            return root.ToJsonString();
        }

        public static string RestoreSerializedUrls(string serializedAddons, Func<string, string?> loadUrl, out bool needsMigration)
        {
            if (loadUrl == null) throw new ArgumentNullException(nameof(loadUrl));
            var root = JsonNode.Parse(serializedAddons) as JsonArray
                ?? throw new InvalidOperationException("The installed add-on store must be a JSON array.");
            needsMigration = false;
            foreach (var node in root)
            {
                if (node is not JsonObject item) continue;
                var key = (string?)item["ManifestUrlVaultKey"];
                if (!string.IsNullOrWhiteSpace(key))
                {
                    var url = loadUrl(key);
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        // A previous build may have exhausted the Windows credential quota.
                        // Preserve recoverable add-ons and skip only records whose URL is irretrievable.
                        item["ManifestUrl"] = string.Empty;
                    }
                    else
                    {
                        item["ManifestUrl"] = url;
                    }
                    item.Remove("ManifestUrlVaultKey");
                    needsMigration = true;
                }
                else if (!string.IsNullOrWhiteSpace((string?)item["ManifestUrl"]))
                {
                    needsMigration = true;
                }
            }
            return root.ToJsonString();
        }
    }
}
