using System;
using System.Text.Json;
using WatchStream.Xbox.Services;
using Xunit;

namespace WatchStream.Tests;

public sealed class StremioProtocolParserTests
{
    [Fact]
    public void ReadsAuthKeyFromSuccessfulLoginResponse()
    {
        Assert.Equal("session-key", StremioProtocolParser.ReadAuthKey("""{"result":{"authKey":"session-key"}}"""));
    }

    [Fact]
    public void RejectsApiErrorInsteadOfTreatingItAsSession()
    {
        Assert.Null(StremioProtocolParser.ReadAuthKey("""{"error":{"message":"unauthorized"}}"""));
    }

    [Fact]
    public void ParsesCatalogMetaAndSkipsMalformedEntries()
    {
        var metas = StremioProtocolParser.ParseCatalogMetas("""{"metas":[{"id":"tt123","type":"movie","name":"Example","poster":"https://img.test/poster.jpg","releaseInfo":"2024"},{"id":"bad","type":"movie"}]}""");
        Assert.Single(metas);
        Assert.Equal("tt123", metas[0].Id);
        Assert.Equal("Example", metas[0].Name);
        Assert.Equal("2024", metas[0].Year);
    }

    [Fact]
    public void ParsesValidStreamsAndSkipsUnsafeOrMalformedUrls()
    {
        var streams = StremioProtocolParser.ParseStreams("""{"streams":[{"name":"HD","url":"https://cdn.test/video.mp4","behaviorHints":{"quality":"1080p"}},{"name":"bad","url":"file:///secret"},{"name":"missing-url"}]}""");
        Assert.Single(streams);
        Assert.Equal("HD", streams[0].Name);
        Assert.Equal("1080p", streams[0].Quality);
    }

    [Fact]
    public void ParsesLibraryProgress()
    {
        var items = StremioProtocolParser.ParseLibraryItems("{\"result\":[{\"_id\":\"tt123\",\"type\":\"movie\",\"name\":\"Example\",\"state\":{\"timeOffset\":120,\"duration\":600,\"timesWatched\":1}}]}");
        Assert.Single(items);
        Assert.True(items[0].Watched);
        Assert.Equal(120, items[0].TimeOffset);
        Assert.Equal(600, items[0].Duration);
    }

    [Fact]
    public void EmptyCatalogResponseProducesNoItems()
    {
        Assert.Empty(StremioProtocolParser.ParseCatalogMetas("""{"metas":[]}"""));
        Assert.Empty(StremioProtocolParser.ParseStreams("""{"streams":[]}"""));
    }
}
