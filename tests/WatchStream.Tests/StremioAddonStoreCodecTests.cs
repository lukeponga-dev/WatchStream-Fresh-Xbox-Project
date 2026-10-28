using System;
using System.Text.Json;
using WatchStream.Xbox.Services;
using Xunit;

namespace WatchStream.Tests;

public sealed class StremioAddonStoreCodecTests
{
    [Fact]
    public void ProtectsManifestUrlAndRestoresItFromOpaqueKey()
    {
        const string secretUrl = "https://addons.example.org/manifest.json?token=do-not-write-to-disk";
        var original = "[{\"Id\":\"example\",\"Name\":\"Example\",\"ManifestUrl\":\"" + secretUrl + "\"}]";
        var protectedJson = StremioAddonStoreCodec.ProtectSerializedUrls(original, url =>
        {
            Assert.Equal(secretUrl, url);
            return "opaque-key-123";
        });

        Assert.DoesNotContain(secretUrl, protectedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-write-to-disk", protectedJson, StringComparison.Ordinal);
        Assert.Contains("opaque-key-123", protectedJson, StringComparison.Ordinal);

        var restored = StremioAddonStoreCodec.RestoreSerializedUrls(protectedJson, key =>
        {
            Assert.Equal("opaque-key-123", key);
            return secretUrl;
        }, out var needsMigration);

        Assert.True(needsMigration); // Migrate URLs out of PasswordVault to avoid its credential quota.
        using var document = JsonDocument.Parse(restored);
        Assert.Equal(secretUrl, document.RootElement[0].GetProperty("ManifestUrl").GetString());
    }

    [Fact]
    public void FlagsLegacyClearTextRecordsForMigration()
    {
        const string legacyUrl = "https://addons.example.org/manifest.json?token=legacy";
        var legacyJson = "[{\"Id\":\"example\",\"Name\":\"Example\",\"ManifestUrl\":\"" + legacyUrl + "\"}]";
        var restored = StremioAddonStoreCodec.RestoreSerializedUrls(legacyJson, _ => null, out var needsMigration);

        Assert.True(needsMigration);
        Assert.Contains(legacyUrl, restored, StringComparison.Ordinal);
    }

    [Fact]
    public void MarksMissingVaultSecretForMigrationSoBrokenRecordsCanBeSkipped()
    {
        const string protectedJson = "[{\"Id\":\"example\",\"Name\":\"Example\",\"ManifestUrl\":\"\",\"ManifestUrlVaultKey\":\"missing\"}]";
        var restored = StremioAddonStoreCodec.RestoreSerializedUrls(protectedJson, _ => null, out var needsMigration);
        Assert.True(needsMigration);
        using var document = JsonDocument.Parse(restored);
        Assert.Equal(string.Empty, document.RootElement[0].GetProperty("ManifestUrl").GetString());
        Assert.False(document.RootElement[0].TryGetProperty("ManifestUrlVaultKey", out _));
    }
}
