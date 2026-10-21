using System.Net;
using System.Text;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class TileDataTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1700000000);
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Interlocked.Increment(ref Calls); return send(request, cancellationToken); }
    }
    private static Handler Respond(string body, string media = "application/json") => new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, media) }));
    private static TileDataBinding Connection() => new() { Enabled = true, Location = "https://example.invalid/data" };

    [Fact]
    public async Task UnconfiguredConnectionsNeverMakeRequests()
    {
        using var handler = Respond("{}"); using var transport = new TileDataTransport(handler);
        using var source = new TileDataSource(transport, new(), new());
        await source.RefreshAsync(Start);
        Assert.Equal(0, handler.Calls); Assert.Equal("unconfigured", source.Snapshot.Status);
    }

    [Fact]
    public async Task CoalescesInFlightReadsCachesAndRevalidatesWithoutGivingScriptsCredentials()
    {
        var ready = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var binding = Connection(); binding.Secret = Guid.NewGuid().ToString("N");
        var sawValidator = false;
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(binding.Secret, request.Headers.GetValues("Authorization").Single());
            if (request.Headers.IfNoneMatch.Count == 0) return ready.Task;
            sawValidator = true; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        });
        using var transport = new TileDataTransport(handler);
        using var source = new TileDataSource(transport, new() { RefreshSeconds = 1, JsonPointer = "/current" }, binding);
        var first = source.RefreshAsync(Start); Assert.Same(first, source.RefreshAsync(Start)); Assert.Equal("loading", source.Snapshot.Status);
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"current\":{\"temperature\":21}}") };
        response.Headers.ETag = new("\"sample\""); ready.SetResult(response); await first;
        await source.RefreshAsync(Start.AddSeconds(29)); Assert.Equal(1, handler.Calls);
        await source.RefreshAsync(Start.AddSeconds(30)); Assert.True(sawValidator); Assert.Equal(2, handler.Calls);
        Assert.Equal(21, source.Snapshot.Value.GetProperty("temperature").GetInt32());
        var context = JsonSerializer.Serialize(source.Snapshot, ScriptTileProtocol.Json);
        Assert.DoesNotContain(binding.Secret, context); Assert.DoesNotContain(binding.Location, context);
        Assert.Equal("ready", source.Snapshot.Status); Assert.False(source.Snapshot.Stale);
    }

    [Fact]
    public async Task FailuresRetainLastGoodValueAndBackOffWithExplicitRefreshThrottling()
    {
        var count = 0;
        using var handler = new Handler((_, _) => Task.FromResult(++count == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":7}") }
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var transport = new TileDataTransport(handler); using var source = new TileDataSource(transport, new() { RefreshSeconds = 30 }, Connection());
        await source.RefreshAsync(Start); await source.RefreshAsync(Start.AddSeconds(30));
        Assert.True(source.Snapshot.Stale); Assert.Equal("error", source.Snapshot.Status); Assert.Equal(503, source.Snapshot.Error!.HttpStatus);
        Assert.Equal(7, source.Snapshot.Value.GetProperty("value").GetInt32());
        await source.RefreshAsync(Start.AddSeconds(59)); Assert.Equal(2, count);
        await source.RefreshAsync(Start.AddSeconds(60)); await source.RefreshAsync(Start.AddSeconds(90)); Assert.Equal(3, count);
        await source.RefreshAsync(Start.AddSeconds(91), true); Assert.Equal(4, count);
        await source.RefreshAsync(Start.AddSeconds(92), true); Assert.Equal(4, count);
    }

    [Theory]
    [InlineData("{\"a/b\":{\"~name\":[42]}}", "/a~1b/~0name/0", null)]
    [InlineData("{\"value\":42}", "/missing", "selector")]
    [InlineData("[42]", "/00", "selector")]
    [InlineData("[42]", "/+0", "selector")]
    public async Task SelectsEscapedJsonPointersAndReportsMissingFields(string json, string pointer, string? error)
    {
        using var handler = Respond(json); using var transport = new TileDataTransport(handler);
        using var source = new TileDataSource(transport, new() { JsonPointer = pointer }, Connection()); await source.RefreshAsync(Start);
        Assert.Equal(error, source.Snapshot.Error?.Code);
        if (error == null) Assert.Equal(42, source.Snapshot.Value.GetInt32());
    }

    [Fact]
    public async Task EmptyLocalPointerOverridesThePackageSelectorWithTheRoot()
    {
        using var handler = Respond("{\"value\":42}"); using var transport = new TileDataTransport(handler);
        var binding = Connection(); binding.JsonPointer = "";
        using var source = new TileDataSource(transport, new() { JsonPointer = "/missing" }, binding);
        await source.RefreshAsync(Start);
        Assert.Null(source.Snapshot.Error); Assert.Equal(42, source.Snapshot.Value.GetProperty("value").GetInt32());
    }

    [Fact]
    public void DeclarationsRequireCapabilityAndBoundedSourcesAndSelectors()
    {
        var manifest = new ScriptTileManifest { DataSources = new() { ["weather"] = new() } };
        Assert.Throws<InvalidDataException>(manifest.Validate);
        manifest.Capabilities = ["data.sources"]; manifest.Validate();
        manifest.DataSources["weather"].JsonPointer = "/invalid~escape";
        Assert.Throws<InvalidDataException>(manifest.Validate);
        manifest.DataSources["weather"].JsonPointer = "/valid~0~1"; manifest.Validate();
        for (var i = 0; i < 4; i++) manifest.DataSources["extra" + i] = new();
        Assert.Throws<InvalidDataException>(manifest.Validate);
        var binding = Connection(); binding.HeaderName = "Cookie"; Assert.False(binding.IsValid());
        binding.HeaderName = "Authorization"; binding.Secret = "bad\r\nvalue"; Assert.False(binding.IsValid());
    }

    [Fact]
    public async Task DeadlinePublishesASafeTimeoutWithoutBlockingSnapshotReads()
    {
        using var handler = new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); });
        using var transport = new TileDataTransport(handler); using var source = new TileDataSource(transport, new(), Connection());
        var request = source.RefreshAsync(Start);
        Assert.Equal("loading", source.Snapshot.Status);
        await request.WaitAsync(TimeSpan.FromSeconds(12));
        Assert.Equal("timeout", source.Snapshot.Error?.Code); Assert.False(source.Snapshot.Stale);
    }

    [Fact]
    public async Task SharedTransportBoundsConcurrencyAndCancelsQueuedRequests()
    {
        var occupied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var started = 0;
        using var handler = new Handler(async (_, token) =>
        { if (Interlocked.Increment(ref started) == 4) occupied.SetResult(); await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); });
        using var transport = new TileDataTransport(handler);
        var sources = Enumerable.Range(0, 8).Select(_ => new TileDataSource(transport, new(), Connection())).ToArray();
        try
        {
            var requests = sources.Select(s => s.RefreshAsync(Start)).ToArray();
            await occupied.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(4, handler.Calls);
            foreach (var source in sources) source.Stop();
            await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.All(sources, s => Assert.Null(s.Snapshot.Error));
        }
        finally { foreach (var source in sources) source.Dispose(); }
    }

    [Theory]
    [InlineData("<rss version='2.0'><channel><title>News</title><item><title>First</title><description>&lt;b&gt;Hello&lt;/b&gt;</description><link>https://example.invalid/news</link></item></channel></rss>")]
    [InlineData("<feed xmlns='http://www.w3.org/2005/Atom'><title>News</title><entry><title>First</title><summary>&lt;b&gt;Hello&lt;/b&gt;</summary><link href='https://example.invalid/news'/></entry></feed>")]
    public async Task NormalizesRssAndAtomIntoPlainBoundedItems(string xml)
    {
        using var handler = Respond(xml, "application/xml"); using var transport = new TileDataTransport(handler);
        using var source = new TileDataSource(transport, new() { Format = "feed" }, Connection()); await source.RefreshAsync(Start);
        Assert.Null(source.Snapshot.Error); Assert.Equal("News", source.Snapshot.Value.GetProperty("title").GetString());
        var item = Assert.Single(source.Snapshot.Value.GetProperty("items").EnumerateArray());
        Assert.Equal("First", item.GetProperty("title").GetString()); Assert.Equal("Hello", item.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task RejectsXmlEntitiesLargeBodiesAndLargeSelectedValues()
    {
        foreach (var (body, format, code, budget) in new[] {
            ("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///nonexistent'>]><rss><channel><title>&x;</title></channel></rss>", "feed", "format", 8192),
            ("{\"value\":\"" + new string('x', TileDataTransport.MaxResponseBytes) + "\"}", "json", "tooLarge", 8192),
            ("{\"value\":\"" + new string('x', 3000) + "\"}", "json", "tooLarge", 2048) })
        {
            using var handler = Respond(body); using var transport = new TileDataTransport(handler);
            using var source = new TileDataSource(transport, new() { Format = format }, Connection(), budget); await source.RefreshAsync(Start);
            Assert.Equal(code, source.Snapshot.Error?.Code); Assert.Null(source.Snapshot.UpdatedAt);
        }
    }

    [Fact]
    public async Task RedirectsCannotMoveCredentialsOrRequestsToAnotherOrigin()
    {
        using var handler = new Handler((_, _) => { var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new("https://other.invalid/data"); return Task.FromResult(response); });
        using var transport = new TileDataTransport(handler); using var source = new TileDataSource(transport, new(), Connection()); await source.RefreshAsync(Start);
        Assert.Equal("redirect", source.Snapshot.Error?.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task StopCancelsWorkWithoutPublishingAnErrorAndReopeningCanRetry()
    {
        var begun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, cancellation) => { begun.TrySetResult(); await Task.Delay(Timeout.Infinite, cancellation); return new(HttpStatusCode.OK); });
        using var transport = new TileDataTransport(handler); using var source = new TileDataSource(transport, new(), Connection());
        var request = source.RefreshAsync(Start); await begun.Task; source.Stop(); await request;
        Assert.Null(source.Snapshot.Error); Assert.Null(source.Snapshot.CheckedAt);
    }

    [Fact]
    public async Task ReadsExplicitLocalFilesWithBomAndKeepsPreviousDataDuringPartialWrites()
    {
        var file = Path.Combine(Path.GetTempPath(), "ReStart-data-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(file, "{\"value\":42}", new UTF8Encoding(true));
            using var handler = Respond("{}"); using var transport = new TileDataTransport(handler);
            using var source = new TileDataSource(transport, new() { RefreshSeconds = 1 }, new() { Enabled = true, Access = "file", Location = file });
            await source.RefreshAsync(Start); Assert.Equal(42, source.Snapshot.Value.GetProperty("value").GetInt32());
            File.WriteAllText(file, "{"); await source.RefreshAsync(Start.AddSeconds(1));
            Assert.True(source.Snapshot.Stale); Assert.Equal(42, source.Snapshot.Value.GetProperty("value").GetInt32()); Assert.Equal(0, handler.Calls);
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData("https://example.invalid/data", true)]
    [InlineData("http://127.0.0.1:8765/data", true)]
    [InlineData("http://localhost:8765/data", true)]
    [InlineData("http://example.invalid/data", false)]
    [InlineData("https://user:password@example.invalid/data", false)]
    [InlineData("file:///sample.json", false)]
    public void ConnectionsRequireExplicitSupportedTransports(string url, bool valid) => Assert.Equal(valid, TileDataBinding.ValidHttpAddress(url));
}
