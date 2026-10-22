using System.Threading;
using System.Threading.Tasks;

namespace WatchStream.Core.Contracts
{
    /// <summary>Stores session material only; must never store the account password.</summary>
    public interface ISessionStore
    {
        Task<string?> ReadSessionAsync(CancellationToken cancellationToken);
        Task WriteSessionAsync(string? sessionMaterial, CancellationToken cancellationToken);
        Task ClearAsync(CancellationToken cancellationToken);
    }
}
