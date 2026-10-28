using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WatchStream.Core.Models;

namespace WatchStream.Core.Contracts
{
    /// <summary>Account-synced library boundary; transport and Stremio wire types stay outside the UI.</summary>
    public interface IStremioLibraryService
    {
        Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken);
    }
}
