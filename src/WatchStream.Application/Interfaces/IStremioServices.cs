using WatchStream.Domain.Models;

namespace WatchStream.Application.Interfaces;

public interface IStremioAccountService
{
    Task<StremioAccount?> GetAccountAsync();
    Task<DeviceAuthRequest> InitiateDeviceAuthAsync();
    Task<bool> PollForTokenAsync(string deviceCode);
    Task LogoutAsync();
}

public interface IStremioAddonService
{
    Task<IEnumerable<MediaItem>> GetCatalogAsync(string addonId, string catalogId);
    Task<IEnumerable<StreamSource>> GetStreamsAsync(string mediaId);
}
