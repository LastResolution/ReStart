using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class TileIntegrationPermissionTests
{
    private static TileIntegrationRequest Request() => new(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), "home-assistant",
        new('a', 64), new('b', 64), ["sensor.temperature", "light.desk"], ["read", "turn_on"]);
    private static TileIntegrationGrant Approve(TileIntegrationRequest request) => new(Guid.NewGuid().ToString("N"), request.TileId, request.PackageId,
        request.Integration, request.RequestFingerprint, request.ConnectionFingerprint, ["sensor.temperature"], ["read"]);

    [Fact]
    public async Task ManifestRequestsNeverGrantAccessOrInvokeAProvider()
    {
        var request = Request(); var calls = 0;
        var broker = new TileIntegrationBroker(() => (request, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => broker.InvokeAsync("home-assistant", "sensor.temperature", "read", _ => Task.FromResult(++calls)));
        Assert.Equal(0, calls);
    }
    [Theory]
    [InlineData("home-assistant", "sensor.temperature", "read", true)]
    [InlineData("other-integration", "sensor.temperature", "read", false)]
    [InlineData("home-assistant", "light.desk", "read", false)]
    [InlineData("home-assistant", "sensor.temperature", "turn_on", false)]
    [InlineData("home-assistant", "sensor.other", "read", false)]
    [InlineData("home-assistant", "sensor.temperature", "delete", false)]
    public async Task EveryOperationChecksTheCurrentGrantedIntegrationEntityAndAction(string integration, string entity, string action, bool allowed)
    {
        var request = Request(); var grant = Approve(request); var calls = 0;
        var broker = new TileIntegrationBroker(() => (request, grant));
        Task<int> Invoke() => broker.InvokeAsync(integration, entity, action, _ => Task.FromResult(++calls));
        if (allowed) { Assert.Equal(1, await Invoke()); Assert.Equal(1, calls); }
        else { await Assert.ThrowsAsync<UnauthorizedAccessException>(Invoke); Assert.Equal(0, calls); }
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ReloadCopiesAndChangedRequestsOrConnectionsNeverReusePriorApproval(int change)
    {
        var request = Request(); var grant = Approve(request);
        var broker = new TileIntegrationBroker(() => (request, grant));
        Assert.NotNull(broker.Stamp("home-assistant", "sensor.temperature", "read"));
        request = change switch
        {
            0 => request with { TileId = Guid.NewGuid().ToString("N") },
            1 => request with { PackageId = Guid.NewGuid().ToString("N") },
            2 => request with { RequestFingerprint = new('c', 64), Entities = [.. request.Entities, "sensor.other"] },
            _ => request with { ConnectionFingerprint = new('c', 64) }
        };
        Assert.Null(broker.Stamp("home-assistant", "sensor.temperature", "read"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => broker.InvokeAsync("home-assistant", "sensor.temperature", "read", _ => Task.FromResult(1)));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationOrReapprovalDuringAProviderCallCannotPublishItsOldResult(bool reapprove)
    {
        var request = Request(); TileIntegrationGrant? grant = Approve(request);
        var broker = new TileIntegrationBroker(() => (request, grant));
        var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = broker.InvokeAsync("home-assistant", "sensor.temperature", "read", _ => ready.Task);
        grant = reapprove ? Approve(request) : null;
        ready.SetResult(42);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pending);
    }
    [Fact]
    public void CurrentDataDeclarationsAndConnectionsChangeTheApprovalFingerprint()
    {
        var id = Guid.NewGuid().ToString("N"); var package = Guid.NewGuid().ToString("N");
        var manifest = new ScriptTileManifest { Capabilities = ["data.sources"], DataSources = new() { ["weather"] = new() } };
        var bindings = new Dictionary<string, TileDataBinding> { ["weather"] = new() { Enabled = true, Location = "https://example.invalid/data" } };
        var first = TileDataPermissions.Request(id, package, manifest, bindings);
        manifest.Title = "Changed display title";
        Assert.Equal(first.RequestFingerprint, TileDataPermissions.Request(id, package, manifest, bindings).RequestFingerprint);
        manifest.DataSources["weather"].JsonPointer = "/expanded";
        Assert.NotEqual(first.RequestFingerprint, TileDataPermissions.Request(id, package, manifest, bindings).RequestFingerprint);
        bindings["weather"].Location = "https://other.invalid/data";
        Assert.NotEqual(first.ConnectionFingerprint, TileDataPermissions.Request(id, package, manifest, bindings).ConnectionFingerprint);
        Assert.Equal(new[] { "read" }, first.Actions);
    }
    [Fact]
    public void UnreadableOrMalformedApprovalFailsClosed()
    {
        var request = Request(); var grant = Approve(request) with { Token = "invalid" };
        Assert.Null(new TileIntegrationBroker(() => (request, grant)).Stamp("home-assistant", "sensor.temperature", "read"));
        Assert.Null(new TileIntegrationBroker(() => throw new IOException()).Stamp("home-assistant", "sensor.temperature", "read"));
    }
}
