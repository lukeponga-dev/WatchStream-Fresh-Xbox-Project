namespace WatchStream.Domain.Models;

public record MediaItem(
    string Id,
    string Name,
    string Type, // "movie" or "series"
    string? Description,
    string? PosterUrl,
    DateTime? ReleaseDate
);

public record StreamSource(
    string Id,
    string Title,
    string Url,
    string Resolution,
    string Codec
);

public record StremioAccount(
    string UserId,
    string Username,
    string Email,
    List<string> AddonIds
);
