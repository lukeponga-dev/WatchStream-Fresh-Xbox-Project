using System;

namespace WatchStream.Xbox.Services
{
    /// <summary>Safe display-only identity data returned by Stremio's existing GetUser contract.</summary>
    public sealed class StremioUserProfile
    {
        public string DisplayName { get; }
        public string Email { get; }
        public string? AvatarUrl { get; }

        public StremioUserProfile(string displayName, string email, string? avatarUrl = null)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Stremio account" : displayName;
            Email = email ?? string.Empty;
            AvatarUrl = avatarUrl;
        }
    }
}
