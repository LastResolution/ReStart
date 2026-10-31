using ReStart.App.Services;

namespace ReStart.Core.Tests;

public class WindowsSearchQueryTests
{
    [Fact]
    public void HandoffContainsOnlyOneBalancedWindowsSearchChord()
    {
        if (!OperatingSystem.IsWindows()) return;
        var inputs = WindowsSearchLauncher.CreateInputs();
        Assert.Equal(new ushort[] { 0x5B, 0x53, 0x53, 0x5B }, inputs.Select(i => i.Union.Keyboard.Vk));
        Assert.Equal(new uint[] { 0, 0, 2, 2 }, inputs.Select(i => i.Union.Keyboard.Flags));
        Assert.All(inputs, i => { Assert.Equal(1u, i.Type); Assert.Equal(0, i.Union.Keyboard.Scan); });
    }

    [Fact]
    public void AdvancedQueriesStartOffAndWindowsSearchActionStartsOn()
    {
        Assert.False(new Preferences().SearchWindowsSyntax); Assert.True(new Preferences().ShowWindowsSearchHandoff);
    }

    [Theory]
    [InlineData("System.ItemNameDisplay", true)]
    [InlineData("System.Kind", true)]
    [InlineData("System.Search.Contents", false)]
    [InlineData("System.Comment", false)]
    public void ContentDisabledProjectionAllowsOnlyApprovedMetadataProperties(string property, bool accepted)
    {
        if (!OperatingSystem.IsWindows()) return;
        var sql = $"SELECT TOP 50 System.ItemUrl,System.ItemNameDisplay FROM \"SystemIndex\" WHERE CONTAINS({property},'\"owned\"') AND System.ItemUrl LIKE 'file:%'";
        Assert.Equal(accepted, WindowsSearchQuery.IsSafeProjection(sql, false));
        Assert.True(WindowsSearchQuery.IsSafeProjection(sql, true));
    }

    [Fact]
    public void RejectsWildcardContentAndRemoteCatalogQueries()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.False(WindowsSearchQuery.IsSafeProjection("SELECT * FROM SystemIndex WHERE CONTAINS(*,'owned') AND System.ItemUrl LIKE 'file:%'", false));
        Assert.False(WindowsSearchQuery.IsSafeProjection("SELECT System.ItemUrl FROM SystemIndex WHERE FREETEXT((System.ItemNameDisplay:0.9,*:0.1),'owned') AND System.ItemUrl LIKE 'file:%'", false));
        Assert.False(WindowsSearchQuery.IsSafeProjection("SELECT System.ItemUrl FROM SystemIndex WHERE CONTAINS(\"*\",'owned') AND System.ItemUrl LIKE 'file:%'", false));
        Assert.False(WindowsSearchQuery.IsSafeProjection("SELECT * FROM Remote.SystemIndex WHERE System.ItemUrl LIKE 'file:%'", true));
    }
}
