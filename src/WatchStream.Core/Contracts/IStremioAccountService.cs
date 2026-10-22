using System.Threading;
using System.Threading.Tasks;

namespace WatchStream.Core.Contracts
{
    /// <summary>Account boundary. Passwords are transient and must never be persisted.</summary>
    public interface IStremioAccountService
    {
        Task SignInAsync(string email, string password, CancellationToken cancellationToken);
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
