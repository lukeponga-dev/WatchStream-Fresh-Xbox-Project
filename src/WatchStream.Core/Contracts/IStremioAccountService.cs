using System.Threading;
using System.Threading.Tasks;

namespace WatchStream.Core.Contracts
{
    /// <summary>
    /// Account API boundary. Concrete request/response DTOs are intentionally deferred
    /// until upstream Stremio contracts are verified against source and tests.
    /// </summary>
    public interface IStremioAccountService
    {
        Task<AccountSessionState> GetSessionStateAsync(CancellationToken cancellationToken);
        Task SignOutAsync(CancellationToken cancellationToken);
    }

    public enum AccountSessionState
    {
        SignedOut = 0,
        SignedIn = 1,
        ReauthenticationRequired = 2
    }
}
