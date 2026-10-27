using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using WatchStream.Core.Contracts;
using WatchStream.Xbox.Services;

namespace WatchStream.Tests;

public sealed class StremioLinkServiceTests
{
    [Fact]
    public async Task CreateLinkAsync_UsesOfficialCreateEndpointAndParsesPayload()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            "{\"result\":{\"code\":\"A1B2\",\"link\":\"https://link.stremio.com/A1B2\",\"qrcode\":\"data:image/png;base64,AA==\"}}"));
        using var client = new HttpClient(handler);
        var service = new StremioLinkService(new FakeSessionStore(), client);

        var link = await service.CreateLinkAsync(CancellationToken.None);

        Assert.Equal("A1B2", link.Code);
        Assert.Equal("https://link.stremio.com/A1B2", link.Link);
        Assert.Equal("data:image/png;base64,AA==", link.QrCode);
        Assert.Equal("https://link.stremio.com/api/v2/create?type=Create", handler.Requests.Single().RequestUri!.AbsoluteUri);
        Assert.Equal(HttpMethod.Get, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task TryCompleteLinkAsync_PendingErrorDoesNotPersistSession()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            "{\"error\":{\"code\":101,\"message\":\"Invalid or expired token\"}}"));
        using var client = new HttpClient(handler);
        var store = new FakeSessionStore();
        var service = new StremioLinkService(store, client);

        var completed = await service.TryCompleteLinkAsync("A1B2", CancellationToken.None);

        Assert.False(completed);
        Assert.Null(store.Session);
        Assert.Single(handler.Requests);
        Assert.Equal("https://link.stremio.com/api/v2/read?type=Read&code=A1B2", handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task TryCompleteLinkAsync_ExchangesLinkTokenAndPersistsAccountKeyOnly()
    {
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.Host == "link.stremio.com")
                return Json(HttpStatusCode.OK, "{\"result\":{\"authKey\":\"short-lived-link-token\"}}");
            return Json(HttpStatusCode.OK, "{\"result\":{\"authKey\":\"account-session-key\",\"user\":{}}}");
        });
        using var client = new HttpClient(handler);
        var store = new FakeSessionStore();
        var service = new StremioLinkService(store, client);

        var completed = await service.TryCompleteLinkAsync("A1B2", CancellationToken.None);

        Assert.True(completed);
        Assert.Equal("account-session-key", store.Session);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://link.stremio.com/api/v2/read?type=Read&code=A1B2", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("https://api.strem.io/api/loginWithToken", handler.Requests[1].RequestUri!.AbsoluteUri);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        using var body = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal("LoginWithToken", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("short-lived-link-token", body.RootElement.GetProperty("token").GetString());
        Assert.NotEqual("short-lived-link-token", store.Session);
    }

    [Fact]
    public async Task TryCompleteLinkAsync_RejectsFailedTokenExchangeWithoutPersisting()
    {
        var handler = new StubHandler(request => request.RequestUri!.Host == "link.stremio.com"
            ? Json(HttpStatusCode.OK, "{\"result\":{\"authKey\":\"short-lived-link-token\"}}")
            : Json(HttpStatusCode.OK, "{\"error\":{\"code\":1,\"message\":\"rejected\"}}"));
        using var client = new HttpClient(handler);
        var store = new FakeSessionStore();
        var service = new StremioLinkService(store, client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TryCompleteLinkAsync("A1B2", CancellationToken.None));
        Assert.Null(store.Session);
    }

    [Fact]
    public async Task TryCompleteLinkAsync_RejectsBlankCode()
    {
        using var client = new HttpClient(new StubHandler(_ => throw new Xunit.Sdk.XunitException("HTTP must not be called")));
        var service = new StremioLinkService(new FakeSessionStore(), client);
        await Assert.ThrowsAsync<ArgumentException>(() => service.TryCompleteLinkAsync("  ", CancellationToken.None));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return responder(request);
        }
    }

    private sealed class FakeSessionStore : ISessionStore
    {
        public string? Session { get; private set; }
        public Task<string?> ReadSessionAsync(CancellationToken cancellationToken) => Task.FromResult(Session);
        public Task WriteSessionAsync(string? sessionMaterial, CancellationToken cancellationToken)
        {
            Session = sessionMaterial;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Session = null;
            return Task.CompletedTask;
        }
    }
}
