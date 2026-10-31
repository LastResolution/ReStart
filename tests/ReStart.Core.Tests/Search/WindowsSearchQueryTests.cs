using ReStart.App.Services;

namespace ReStart.Core.Tests;

public class WindowsSearchQueryTests
{
    [Theory]
    [InlineData("hello & #world?", "hello & #world?")]
    [InlineData(" 日本語 / text ", "日本語 / text")]
    [InlineData("\"quoted\" % value", "\"quoted\" % value")]
    public void HandoffUsesOneEncodedQueryAndAFixedScheme(string text, string expected)
    {
        var uri = WindowsSearchHandoff.ForQuery(text)!;
        Assert.Equal("ms-search", uri.Scheme); Assert.Equal("search", uri.AbsolutePath);
        Assert.Equal(expected, Uri.UnescapeDataString(uri.Query["?query=".Length..]));
        Assert.Equal("", uri.Fragment);
    }

    [Fact]
    public void RejectsEmptyOversizedOrControlBearingHandoffs()
    {
        Assert.Null(WindowsSearchHandoff.ForQuery(null));
        Assert.Null(WindowsSearchHandoff.ForQuery(" "));
        Assert.Null(WindowsSearchHandoff.ForQuery(new string('x', 129)));
        Assert.Null(WindowsSearchHandoff.ForQuery("bad\nquery"));
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
