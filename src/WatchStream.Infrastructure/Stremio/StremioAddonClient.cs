using System.Net.Http.Json;
using WatchStream.Application.Interfaces;
using WatchStream.Domain.Models;

namespace WatchStream.Infrastructure.Stremio;

public class StremioAddonClient : IStremioAddonService
{
    private readonly HttpClient _httpClient;

    public StremioAddonClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<MediaItem>> GetCatalogAsync(string addonId, string catalogId)
    {
        try
        {
            // Stremio Addon Protocol: /catalog/{catalogId}/streams.json
            var url = $"https://{addonId}/catalog/{catalogId}/streams.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) return Enumerable.Empty<MediaItem>();

            var content = await response.Content.ReadFromJsonAsync<AddonCatalogResponse>();
            if (content?.Catalogs == null) return Enumerable.Empty<MediaItem>();

            return content.Catalogs.Select(c => new MediaItem(
                Id: c.Id,
                Name: c.Name,
                Type: "movie", // Simplified for initial implementation
                Description: null,
                PosterUrl: null,
                ReleaseDate: null
            ));
        }
        catch
        {
            return Enumerable.Empty<MediaItem>();
        }
    }

    public async Task<IEnumerable<StreamSource>> GetStreamsAsync(string mediaId)
    {
        // Implementation for fetching streams for a specific media ID
        // Usually involves querying multiple installed addons
        return Enumerable.Empty<StreamSource>();
    }

    private record AddonCatalogResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("catalogs")] List<CatalogEntry>? Catalogs
    );

    private record CatalogEntry(
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id,
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name
    );
}
