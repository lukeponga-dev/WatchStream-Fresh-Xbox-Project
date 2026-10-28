using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

namespace WatchStream.Xbox.Services
{
    /// <summary>Validation helpers for untrusted add-on URLs and bounded HTTP responses.</summary>
    public static class StremioAddonSecurity
    {
        public static Uri ValidateRequestUri(Uri uri, bool allowLocalhostHttp = false)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (!uri.IsAbsoluteUri || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException("The add-on URL must be an absolute URL without embedded credentials.");

            var localHttp = allowLocalhostHttp &&
                uri.Scheme == Uri.UriSchemeHttp &&
                (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));

            if (uri.Scheme != Uri.UriSchemeHttps && !localHttp)
                throw new InvalidOperationException("Add-on requests must use HTTPS. HTTP is allowed only for localhost development.");

            if (uri.IsLoopback && !localHttp)
                throw new InvalidOperationException("Loopback add-on URLs are not allowed.");

            var host = uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant();
            if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) ||
                host.EndsWith(".local", StringComparison.Ordinal) || host.EndsWith(".internal", StringComparison.Ordinal))
            {
                if (!localHttp) throw new InvalidOperationException("Local or internal network add-on URLs are not allowed.");
            }

            if (IPAddress.TryParse(host, out var address) && IsNonPublicAddress(address) && !localHttp)
                throw new InvalidOperationException("Add-on URLs must not target private, loopback, or reserved IP addresses.");

            return uri;
        }

        public static bool IsValidManifestUri(string? value, bool allowLocalhostHttp = false)
        {
            if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)) return false;
            try
            {
                ValidateRequestUri(uri, allowLocalhostHttp);
                return uri.AbsolutePath.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void ValidateManifest(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("The add-on manifest must be a JSON object.");
            RequireManifestString(root, "id", 256);
            RequireManifestString(root, "name", 256);
            ValidateOptionalManifestString(root, "description", 4096);
            ValidateOptionalManifestString(root, "version", 64);
            ValidateOptionalManifestString(root, "logo", 2048);
            ValidateOptionalManifestString(root, "background", 2048);
            ValidateManifestStringArray(root, "types", 64, 64);
            ValidateManifestStringArray(root, "idPrefixes", 128, 256);

            if (root.TryGetProperty("resources", out var resources))
            {
                if (resources.ValueKind != JsonValueKind.Array || resources.GetArrayLength() > 32)
                    throw new InvalidOperationException("The add-on manifest has an invalid or oversized resources array.");
                foreach (var resource in resources.EnumerateArray())
                {
                    if (resource.ValueKind == JsonValueKind.String)
                    {
                        if (string.IsNullOrWhiteSpace(resource.GetString()) || resource.GetString()!.Length > 64)
                            throw new InvalidOperationException("The add-on manifest contains an invalid resource name.");
                    }
                    else if (resource.ValueKind == JsonValueKind.Object)
                    {
                        RequireManifestString(resource, "name", 64);
                        ValidateManifestStringArray(resource, "types", 64, 64);
                        ValidateManifestStringArray(resource, "idPrefixes", 128, 256);
                    }
                    else throw new InvalidOperationException("The add-on manifest contains an invalid resource entry.");
                }
            }

            if (root.TryGetProperty("catalogs", out var catalogs))
            {
                if (catalogs.ValueKind != JsonValueKind.Array || catalogs.GetArrayLength() > 100)
                    throw new InvalidOperationException("The add-on manifest has an invalid or oversized catalogs array.");
                foreach (var catalog in catalogs.EnumerateArray())
                {
                    if (catalog.ValueKind != JsonValueKind.Object)
                        throw new InvalidOperationException("The add-on manifest contains an invalid catalog entry.");
                    RequireManifestString(catalog, "id", 256);
                    RequireManifestString(catalog, "type", 64);
                    ValidateOptionalManifestString(catalog, "name", 256);
                    if (!catalog.TryGetProperty("extra", out var extras)) continue;
                    if (extras.ValueKind != JsonValueKind.Array || extras.GetArrayLength() > 32)
                        throw new InvalidOperationException("The add-on manifest has an invalid or oversized catalog extras array.");
                    foreach (var extra in extras.EnumerateArray())
                    {
                        if (extra.ValueKind != JsonValueKind.Object)
                            throw new InvalidOperationException("The add-on manifest contains an invalid catalog extra.");
                        RequireManifestString(extra, "name", 64);
                        ValidateManifestStringArray(extra, "options", 100, 256);
                    }
                }
            }
        }

        private static void RequireManifestString(JsonElement element, string property, int maxLength)
        {
            if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > maxLength)
                throw new InvalidOperationException("The add-on manifest contains an invalid '" + property + "' field.");
        }

        private static void ValidateOptionalManifestString(JsonElement element, string property, int maxLength)
        {
            if (!element.TryGetProperty(property, out var value)) return;
            if (value.ValueKind != JsonValueKind.String || (value.GetString()?.Length ?? 0) > maxLength)
                throw new InvalidOperationException("The add-on manifest contains an invalid '" + property + "' field.");
        }

        private static void ValidateManifestStringArray(JsonElement element, string property, int maxItems, int maxLength)
        {
            if (!element.TryGetProperty(property, out var array)) return;
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > maxItems)
                throw new InvalidOperationException("The add-on manifest contains an invalid or oversized '" + property + "' array.");
            foreach (var item in array.EnumerateArray())
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()) || item.GetString()!.Length > maxLength)
                    throw new InvalidOperationException("The add-on manifest contains an invalid value in '" + property + "'.");
        }

        public static Uri ValidateRedirectTarget(Uri current, Uri? location, int redirectsAlreadyFollowed)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (location == null || redirectsAlreadyFollowed >= 3)
                throw new InvalidOperationException("The add-on endpoint returned an invalid or excessive redirect.");
            var target = location.IsAbsoluteUri ? location : new Uri(current, location);
            return ValidateRequestUri(target);
        }

        public static async Task<string> ReadBoundedResponseTextAsync(HttpContent content, CancellationToken cancellationToken, int maxBytes = 2 * 1024 * 1024)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            var declaredLength = content.Headers.ContentLength;
            if (declaredLength.HasValue && declaredLength.Value > maxBytes)
                throw new InvalidOperationException("The add-on response exceeded the allowed size limit.");

            using (var input = await content.ReadAsStreamAsync(cancellationToken))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                while (true)
                {
                    var count = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (count == 0) break;
                    if (output.Length + count > maxBytes)
                        throw new InvalidOperationException("The add-on response exceeded the allowed size limit.");
                    output.Write(buffer, 0, count);
                }
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        public static Uri BuildResourceUri(string manifestUrl, string resource, string type, string id, System.Collections.Generic.IDictionary<string, string>? extra)
        {
            if (!IsValidManifestUri(manifestUrl, allowLocalhostHttp: true))
                throw new InvalidOperationException("The installed add-on manifest URL is not a valid HTTPS endpoint.");
            var manifestUri = new Uri(manifestUrl);
            var manifest = ValidateRequestUri(manifestUri, allowLocalhostHttp: manifestUri.IsLoopback && manifestUri.Scheme == Uri.UriSchemeHttp);
            var basePath = manifest.AbsolutePath.Substring(0, manifest.AbsolutePath.Length - "manifest.json".Length);
            var route = resource.TrimEnd('/') + "/" + Uri.EscapeDataString(type) + "/" + Uri.EscapeDataString(id);
            if (extra != null && extra.Count > 0)
                route += "/" + string.Join("&", extra.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
            var builder = new UriBuilder(manifest) { Path = basePath + route + ".json", Query = manifest.Query.TrimStart('?'), Fragment = string.Empty };
            return builder.Uri;
        }

        public static bool IsValidStreamUri(string? value)
        {
            if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
                return false;
            var host = uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant();
            if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) ||
                host.EndsWith(".local", StringComparison.Ordinal) || host.EndsWith(".internal", StringComparison.Ordinal))
                return false;
            return !IPAddress.TryParse(host, out var address) || !IsNonPublicAddress(address);
        }

        public static bool IsNonPublicAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6) return IsNonPublicAddress(address.MapToIPv4());
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6Loopback) ||
                address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal)
                return true;

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var bytes = address.GetAddressBytes();
                return (bytes[0] & 0xFE) == 0xFC; // IPv6 unique-local fc00::/7
            }

            if (address.AddressFamily != AddressFamily.InterNetwork) return true;
            var octets = address.GetAddressBytes();
            return octets[0] == 0 || octets[0] == 10 || octets[0] == 127 ||
                   (octets[0] == 169 && octets[1] == 254) ||
                   (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31) ||
                   (octets[0] == 192 && octets[1] == 168) ||
                   (octets[0] == 192 && octets[1] == 0 && octets[2] == 0) ||
                   (octets[0] == 192 && octets[1] == 0 && octets[2] == 2) ||
                   (octets[0] == 198 && (octets[1] == 18 || octets[1] == 19)) ||
                   (octets[0] == 198 && octets[1] == 51 && octets[2] == 100) ||
                   (octets[0] == 203 && octets[1] == 0 && octets[2] == 113) ||
                   (octets[0] == 100 && octets[1] >= 64 && octets[1] <= 127) ||
                   octets[0] >= 224;
        }
    }
}
