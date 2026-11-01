using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class AppHistoryMetadataTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MergingShortcutMetadataPreservesBothSourcesExclusions(bool shellExclusion, bool linkExclusion)
    {
        var shell = new AppEntry("owned.app", "Owned app", "owned.app") { ExcludeFromRecentlyAdded = shellExclusion, ExcludeFromMostUsed = shellExclusion };
        var link = new AppEntry("owned-link", "Owned app", "owned.lnk", false) { ExcludeFromRecentlyAdded = linkExclusion, ExcludeFromMostUsed = linkExclusion };
        var merged = Assert.Single(AppCatalogLayout.Merge([shell], [new("Owned.lnk", link, shell.Id)]));
        Assert.True(merged.ExcludeFromRecentlyAdded); Assert.True(merged.ExcludeFromMostUsed);
        Assert.Equal(shell.Id, merged.Id); Assert.Equal(shell.LaunchTarget, merged.LaunchTarget);
    }
}
