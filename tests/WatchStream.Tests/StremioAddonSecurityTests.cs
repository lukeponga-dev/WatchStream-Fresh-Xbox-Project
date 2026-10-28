using System;
using System.Text.Json;
using WatchStream.Xbox.Services;
using Xunit;

namespace WatchStream.Tests;

public sealed class StremioAddonSecurityTests
{
    [Theory]
    [InlineData("https://example.com/manifest.json")]
    [InlineData("https://addons.example.org/v1/manifest.json")]
    public void AcceptsPublicHttpsManifestUrls(string value)
    {
        Assert.True(StremioAddonSecurity.IsValidManifestUri(value));
    }

    [Theory]
    [InlineData("http://example.com/manifest.json")]
    [InlineData("https://localhost/manifest.json")]
    [InlineData("https://127.0.0.1/manifest.json")]
    [InlineData("https://10.0.0.4/manifest.json")]
    [InlineData("https://192.168.1.4/manifest.json")]
    [InlineData("https://172.16.0.5/manifest.json")]
    [InlineData("https://169.254.169.254/manifest.json")]
    [InlineData("https://service.local/manifest.json")]
    [InlineData("https://service.internal/manifest.json")]
    [InlineData("https://user:pass@example.com/manifest.json")]
    [InlineData("https://example.com/not-a-manifest")]
    [InlineData("not a URL")]
    public void RejectsUnsafeOrInvalidManifestUrls(string value)
    {
        Assert.False(StremioAddonSecurity.IsValidManifestUri(value));
    }

    [Fact]
    public void AllowsLoopbackHttpOnlyWhenExplicitlyEnabled()
    {
        var uri = new Uri("http://localhost:1234/manifest.json");

        Assert.Throws<InvalidOperationException>(() => StremioAddonSecurity.ValidateRequestUri(uri));
        Assert.Equal(uri, StremioAddonSecurity.ValidateRequestUri(uri, allowLocalhostHttp: true));
    }

    [Theory]
    [InlineData("https://10.0.0.2/path")]
    [InlineData("https://[fd00::1]/path")]
    [InlineData("https://[fe80::1]/path")]
    [InlineData("https://192.0.2.1/path")]
    [InlineData("https://198.18.0.1/path")]
    [InlineData("https://198.51.100.1/path")]
    [InlineData("https://203.0.113.1/path")]
    [InlineData("https://0.0.0.0/path")]
    [InlineData("https://224.0.0.1/path")]
    public void RejectsPrivateAddressRedirectTargets(string value)
    {
        Assert.Throws<InvalidOperationException>(() => StremioAddonSecurity.ValidateRequestUri(new Uri(value)));
    }

    [Fact]
    public void ResolvesRelativeRedirectAndKeepsItOnHttps()
    {
        var current = new Uri("https://addons.example.org/v1/manifest.json");
        var target = StremioAddonSecurity.ValidateRedirectTarget(current, new Uri("../catalog.json", UriKind.Relative), 0);
        Assert.Equal("https://addons.example.org/catalog.json", target.AbsoluteUri);
    }

    [Fact]
    public void RejectsUnsafeOrExcessiveRedirects()
    {
        var current = new Uri("https://addons.example.org/manifest.json");
        Assert.Throws<InvalidOperationException>(() => StremioAddonSecurity.ValidateRedirectTarget(current, new Uri("http://example.org/manifest.json"), 0));
        Assert.Throws<InvalidOperationException>(() => StremioAddonSecurity.ValidateRedirectTarget(current, new Uri("https://127.0.0.1/manifest.json"), 0));
        Assert.Throws<InvalidOperationException>(() => StremioAddonSecurity.ValidateRedirectTarget(current, new Uri("/next", UriKind.Relative), 3));
        Assert.Throws<InvalidOperationException>(() => StremioAddonSecurity.ValidateRedirectTarget(current, null, 0));
    }

    [Fact]
    public async Task ReadsBoundedUtf8Response()
    {
        using var content = new StringContent("{\"ok\":true}");
        var result = await StremioAddonSecurity.ReadBoundedResponseTextAsync(content, CancellationToken.None, 32);
        Assert.Equal("{\"ok\":true}", result);
    }

    [Fact]
    public async Task RejectsResponseOverConfiguredLimit()
    {
        using var content = new StringContent("12345");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StremioAddonSecurity.ReadBoundedResponseTextAsync(content, CancellationToken.None, 4));
    }

    [Fact]
    public void BuildsResourceUrlWithEscapedParametersAndPreservesBaseQuery()
    {
        var extras = new Dictionary<string, string> { ["genre"] = "Drama & Mystery" };
        var uri = StremioAddonSecurity.BuildResourceUri(
            "https://addons.example.org/base/manifest.json?key=secret",
            "catalog", "movie", "tt123", extras);

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("addons.example.org", uri.Host);
        Assert.Equal("?key=secret", uri.Query);
        Assert.Contains("catalog/movie/tt123/genre=Drama%20%26%20Mystery.json", uri.AbsolutePath);
    }

    [Fact]
    public void RejectsResourceUrlFromUnsafeManifest()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StremioAddonSecurity.BuildResourceUri("http://example.org/manifest.json", "stream", "movie", "tt123", null));
    }

    [Theory]
    [InlineData("https://cdn.example.org/movie.mp4", true)]
    [InlineData("http://cdn.example.org/movie.mp4", false)]
    [InlineData("https://127.0.0.1/movie.mp4", false)]
    [InlineData("https://192.168.1.1/movie.mp4", false)]
    [InlineData("https://router.local/movie.mp4", false)]
    [InlineData("https://user:pass@cdn.example.org/movie.mp4", false)]
    [InlineData("magnet:?xt=urn:btih:abc", false)]
    public void StreamUriPolicyRejectsUnsupportedOrUnsafeTargets(string value, bool expected)
    {
        Assert.Equal(expected, StremioAddonSecurity.IsValidStreamUri(value));
    }

    [Fact]
    public void AcceptsBoundedWellFormedManifest()
    {
        using var document = JsonDocument.Parse("""
            {
              "id":"org.example.addon",
              "name":"Example Add-on",
              "description":"A safe test manifest",
              "types":["movie","series"],
              "resources":["catalog",{"name":"stream","types":["movie"]}],
              "catalogs":[{"id":"popular","type":"movie","name":"Popular","extra":[{"name":"genre","options":["Drama","Comedy"]}]}]
            }
            """);
        StremioAddonSecurity.ValidateManifest(document.RootElement);
    }

    [Theory]
    [InlineData("{\"name\":\"Missing ID\"}")]
    [InlineData("{\"id\":\"id\",\"name\":\"\"}")]
    [InlineData("{\"id\":\"id\",\"name\":\"Name\",\"types\":\"movie\"}")]
    [InlineData("{\"id\":\"id\",\"name\":\"Name\",\"resources\":[null]}")]
    [InlineData("{\"id\":\"id\",\"name\":\"Name\",\"catalogs\":[{\"id\":\"x\"}]}")]
    public void RejectsMalformedManifestFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<InvalidOperationException>(() => StremioAddonSecurity.ValidateManifest(document.RootElement));
    }

    [Fact]
    public void RejectsOversizedManifestArrays()
    {
        var resources = string.Join(",", Enumerable.Repeat("\"catalog\"", 33));
        using var document = JsonDocument.Parse("{\"id\":\"id\",\"name\":\"Name\",\"resources\":[" + resources + "]}");
        Assert.Throws<InvalidOperationException>(() => StremioAddonSecurity.ValidateManifest(document.RootElement));
    }
}
