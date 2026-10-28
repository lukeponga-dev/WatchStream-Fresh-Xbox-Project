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
        // Older builds incorrectly stored every public add-on manifest URL as a credential.
        // Remove only entries from that obsolete resource before writing the actual session key.
        private const string LegacyAddonUrlResource = "WatchStream.AddonManifestUrl.v1";

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
            CleanupLegacyAddonUrlCredentials(vault);
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

        private static void CleanupLegacyAddonUrlCredentials(PasswordVault vault)
        {
            try
            {
                foreach (var credential in vault.FindAllByResource(LegacyAddonUrlResource))
                    vault.Remove(credential);
            }
            catch (Exception)
            {
                // FindAllByResource may itself fail when the system vault is exhausted or empty.
                // Do not mask the original session-store operation; add-on migration also cleans these entries.
            }
        }
    }
}
