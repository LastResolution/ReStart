using System.Net;

namespace ReStart.Core.Tests;

public sealed class TileDataPermissionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> read) : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Interlocked.Increment(ref Calls); return read(cancellationToken); }
    }
    private static HttpResponseMessage Response() => new(HttpStatusCode.OK) { Content = new StringContent("{\"value\":42}") };
    private static TileDataBinding Binding() => new() { Enabled = true, Location = "https://example.invalid/data" };

    [Fact]
    public async Task DeniedHostApprovalNeverStartsHttpAndRevocationClearsAlreadyCachedData()
    {
        string? approval = null;
        using var handler = new Handler(_ => Task.FromResult(Response()));
        using var transport = new TileDataTransport(handler);
        using var source = new TileDataSource(transport, new(), Binding(), authorization: () => approval);
        await source.RefreshAsync(Now);
        Assert.Equal(0, handler.Calls); Assert.Equal("permission", source.Snapshot.Status);
        approval = "first"; await source.RefreshAsync(Now);
        Assert.Equal(42, source.Snapshot.Value.GetProperty("value").GetInt32());
        approval = null;
        Assert.Equal("permission", source.Snapshot.Status); Assert.Equal(System.Text.Json.JsonValueKind.Null, source.Snapshot.Value.ValueKind);
        approval = "second"; await source.RefreshAsync(Now.AddSeconds(1));
        Assert.Equal("ready", source.Snapshot.Status); Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task RevokedInflightReadCannotPublishOrRetainThePreviousValue()
    {
        string? approval = "first";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(_ => { entered.TrySetResult(); return response.Task; });
        using var transport = new TileDataTransport(handler);
        using var source = new TileDataSource(transport, new(), Binding(), authorization: () => approval);
        var pending = source.RefreshAsync(Now); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        approval = null; response.SetResult(Response()); await pending;
        Assert.Equal("permission", source.Snapshot.Status); Assert.Null(source.Snapshot.UpdatedAt);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, source.Snapshot.Value.ValueKind);
    }

    [Fact]
    public async Task AQueuedTransportReadRechecksApprovalBeforeItCanContactTheProvider()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        using var handler = new Handler(async _ => { if (Interlocked.Increment(ref count) == 4) entered.TrySetResult(); await release.Task; return Response(); });
        using var transport = new TileDataTransport(handler);
        var active = Enumerable.Range(0, 4).Select(_ => new TileDataSource(transport, new(), Binding())).ToArray();
        string? approval = "first";
        using var queued = new TileDataSource(transport, new(), Binding(), authorization: () => approval);
        try
        {
            var pending = active.Select(s => s.RefreshAsync(Now)).ToArray();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var denied = queued.RefreshAsync(Now);
            approval = null; release.TrySetResult();
            await Task.WhenAll(pending.Append(denied)).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(4, handler.Calls); Assert.Equal("permission", queued.Snapshot.Status);
        }
        finally { release.TrySetResult(); foreach (var source in active) source.Dispose(); }
    }
}
