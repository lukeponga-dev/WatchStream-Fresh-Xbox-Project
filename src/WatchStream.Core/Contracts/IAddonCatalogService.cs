using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WatchStream.Core.Models;

namespace WatchStream.Core.Contracts
{
    public interface IAddonCatalogService
    {
        Task<IReadOnlyList<MediaItem>> GetCatalogAsync(
            string catalogId,
            CancellationToken cancellationToken);
    }
}
