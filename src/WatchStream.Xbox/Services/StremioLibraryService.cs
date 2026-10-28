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
    /// <summary>
    /// Adapter for stremio-core's datastoreGet request on the libraryItem collection.
    /// Wire DTOs and response parsing are deliberately isolated from XAML and UI models.
    /// </summary>
    public sealed class StremioLibraryService : IStremioLibraryService
    {
        private const string Endpoint = "https://api.strem.io/api/datastoreGet";
        private readonly ISessionStore _sessionStore;
        private readonly HttpClient _http;

        public StremioLibraryService(ISessionStore sessionStore)
            : this(sessionStore, new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
        {
        }

        internal StremioLibraryService(ISessionStore sessionStore, HttpClient http)
        {
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken)
        {
            var authKey = await _sessionStore.ReadSessionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(authKey))
                throw new InvalidOperationException("Connect your Stremio account to view your library.");

            // DatastoreRequest uses camelCase fields and flattens DatastoreCommand::Get.
            var body = "{\"authKey\":" + JsonSerializer.Serialize(authKey, WatchStreamJson.Options) +
                       ",\"collection\":\"libraryItem\",\"ids\":[],\"all\":true}";
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            using var response = await _http.SendAsync(request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Stremio couldn't load your library. Check your connection and try again.");

            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (HasApiError(root) || !root.TryGetProperty("result", out var result))
                throw new InvalidOperationException("Stremio rejected the library request. Reconnect your account and try again.");

            var parsedItems = StremioProtocolParser.ParseLibraryItems(document.RootElement.GetRawText());
            return parsedItems.Select(item => new MediaItem(item.Id, item.Type, item.Name, item.Poster, item.Year, string.Empty,
                    item.Watched, item.TimeOffset, item.Duration, item.Removed, item.Temporary))
                .GroupBy(item => item.Type + "|" + item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).ToList();
        }

        private static void CollectLibraryItems(JsonElement value, ICollection<MediaItem> output)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in value.EnumerateArray())
                    CollectLibraryItems(entry, output);
                return;
            }

            if (value.ValueKind != JsonValueKind.Object)
                return;

            // Some API envelopes expose the collection under items; LibraryItem itself
            // carries the catalog metadata under item. Handle both without leaking either shape.
            if (value.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in items.EnumerateArray())
                    CollectLibraryItems(entry, output);
                return;
            }

            var metadata = value;
            if (value.TryGetProperty("item", out var nested) && nested.ValueKind == JsonValueKind.Object)
                metadata = nested;

            var id = ReadString(metadata, "id") ?? ReadString(metadata, "_id");
            var type = ReadString(metadata, "type");
            var name = ReadString(metadata, "name") ?? ReadString(metadata, "title");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
                return;

            output.Add(new MediaItem(
                id,
                type,
                name,
                ReadString(metadata, "poster") ?? string.Empty,
                ReadString(metadata, "releaseInfo") ?? ReadString(metadata, "year") ?? string.Empty,
                ReadString(metadata, "description") ?? string.Empty));
        }

        private static bool ReadBoolean(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.True;

        private static bool HasApiError(JsonElement root) =>
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


