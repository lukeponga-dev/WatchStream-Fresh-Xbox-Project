using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using WatchStream.Core.Contracts;

namespace WatchStream.Xbox.Services
{
    /// <summary>Stores only the Stremio session key in the Windows credential vault.</summary>
    public sealed class SecureSessionStore : ISessionStore
    {
        private const string ResourceName = "WatchStream.Stremio";
        private const string UserName = "session-auth-key";

        public Task<string?> ReadSessionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var credential = new PasswordVault().Retrieve(ResourceName, UserName);
                credential.RetrievePassword();
                return Task.FromResult<string?>(credential.Password);
            }
            catch (Exception) { return Task.FromResult<string?>(null); }
        }

        public Task WriteSessionAsync(string? sessionMaterial, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vault = new PasswordVault();
            RemoveExisting(vault);
            if (!string.IsNullOrWhiteSpace(sessionMaterial))
                vault.Add(new PasswordCredential(ResourceName, UserName, sessionMaterial));
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoveExisting(new PasswordVault());
            return Task.CompletedTask;
        }

        private static void RemoveExisting(PasswordVault vault)
        {
            try { vault.Remove(vault.Retrieve(ResourceName, UserName)); }
            catch (Exception) { }
        }
    }
}
