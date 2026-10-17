using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class ReleaseCredentialStoreTests
{
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void CredentialsRoundTripOnlyWithinAnIsolatedTestTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        var store = new ReleaseCredentialStore("ReStart.Tests/" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(store.Read());
            store.Save("fixture-not-a-real-token");
            Assert.Equal("fixture-not-a-real-token", store.Read());
            store.Save("replacement-fixture");
            Assert.Equal("replacement-fixture", store.Read());
            Assert.Throws<ArgumentException>(() => store.Save("bad\nvalue"));
            store.Delete(); Assert.Null(store.Read());
        }
        finally { store.Delete(); }
    }
}
