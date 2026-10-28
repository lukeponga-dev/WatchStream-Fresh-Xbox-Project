using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WatchStream.Xbox.Services
{
    /// <summary>Pure, offline-testable parsers for Stremio wire responses.</summary>
    internal static class StremioProtocolParser
    {
        internal static bool TryReadString(JsonElement element, string property, out string value)
        {
            value = string.Empty;
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var candidate) ||
                candidate.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(candidate.GetString())) return false;
            value = candidate.GetString()!;
            return true;
        }

        internal static string? ReadAuthKey(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (HasError(root) || !root.TryGetProperty("result", out var result) || !TryReadString(result, "authKey", out var authKey)) return null;
            return authKey;
        }

        internal static IReadOnlyList<ParsedStream> ParseStreams(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var result = new List<ParsedStream>();
            if (HasError(root) || !root.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array) return result;
            foreach (var stream in streams.EnumerateArray())
            {
                if (!TryReadString(stream, "url", out var url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) continue;
                var name = ReadOptional(stream, "name") ?? ReadOptional(stream, "title") ?? "Stream";
                var description = ReadOptional(stream, "description") ?? string.Empty;
                var quality = ReadOptional(stream, "quality");
                if (quality == null && stream.TryGetProperty("behaviorHints", out var hints)) quality = ReadOptional(hints, "quality");
                result.Add(new ParsedStream(name, uri.AbsoluteUri, description, quality ?? string.Empty));
            }
            return result;
        }

        internal static IReadOnlyList<ParsedLibraryItem> ParseLibraryItems(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var result = new List<ParsedLibraryItem>();
            if (HasError(root) || !root.TryGetProperty("result", out var items) || items.ValueKind != JsonValueKind.Array) return result;
            foreach (var item in items.EnumerateArray())
            {
                if (!TryReadString(item, "_id", out var id) || !TryReadString(item, "type", out var type) ||
                    !TryReadString(item, "name", out var name)) continue;
                var state = item.TryGetProperty("state", out var stateValue) && stateValue.ValueKind == JsonValueKind.Object ? stateValue : default;
                var timeOffset = ReadInt64(state, "timeOffset");
                var duration = ReadInt64(state, "duration");
                var watched = ReadInt64(state, "timesWatched") > 0;
                result.Add(new ParsedLibraryItem(id, type, name, ReadOptional(item, "poster") ?? string.Empty,
                    ReadOptional(item, "releaseInfo") ?? string.Empty, watched, timeOffset, duration,
                    ReadBoolean(item, "removed"), ReadBoolean(item, "temp")));
            }
            return result;
        }

        private static long ReadInt64(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? Math.Max(0, number) : 0;

        private static bool ReadBoolean(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.True;

        internal static IReadOnlyList<ParsedMeta> ParseCatalogMetas(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var result = new List<ParsedMeta>();
            if (HasError(root) || !root.TryGetProperty("metas", out var metas) || metas.ValueKind != JsonValueKind.Array) return result;
            foreach (var meta in metas.EnumerateArray())
            {
                if (!TryReadString(meta, "id", out var id) || !TryReadString(meta, "type", out var type)) continue;
                var name = ReadOptional(meta, "name") ?? ReadOptional(meta, "title");
                if (string.IsNullOrWhiteSpace(name)) continue;
                result.Add(new ParsedMeta(id, type, name!, ReadOptional(meta, "poster") ?? string.Empty,
                    ReadOptional(meta, "releaseInfo") ?? ReadOptional(meta, "year") ?? string.Empty,
                    ReadOptional(meta, "description") ?? string.Empty));
            }
            return result;
        }

        private static string? ReadOptional(JsonElement element, string property) =>
            TryReadString(element, property, out var value) ? value : null;

        private static bool HasError(JsonElement root) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error) &&
            error.ValueKind != JsonValueKind.Null && error.ValueKind != JsonValueKind.False &&
            !(error.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(error.GetString()));
    }

    internal sealed record ParsedStream(string Name, string Url, string Description, string Quality);
    internal sealed record ParsedLibraryItem(string Id, string Type, string Name, string Poster, string Year, bool Watched, long TimeOffset, long Duration, bool Removed, bool Temporary);
    internal sealed record ParsedMeta(string Id, string Type, string Name, string Poster, string Year, string Description);
}
