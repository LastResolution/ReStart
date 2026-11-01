using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class WebSearchQueryTests
{
    [Theory]
    [InlineData("app & setting?x=1#fragment")]
    [InlineData("メモ帳　😀")]
    [InlineData("https://example.test/?a=b&c=d")]
    [InlineData("powershell; example | sample")]
    public void EscapesUntrustedTextAsOneHttpsQuery(string text)
    {
        var uri = WebSearchQuery.Build(" " + text + " ")!;
        Assert.Equal("https", uri.Scheme); Assert.Equal("www.bing.com", uri.Host);
        Assert.Equal("/search", uri.AbsolutePath); Assert.Empty(uri.Fragment); Assert.Empty(uri.UserInfo);
        Assert.Equal(text, Uri.UnescapeDataString(uri.Query[3..]));
        Assert.Equal("?q=" + Uri.EscapeDataString(text), uri.Query);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\nb")]
    [InlineData("a\0b")]
    [InlineData("\nleading")]
    public void RejectsBlankOrControlText(string? text) => Assert.Null(WebSearchQuery.Build(text));

    [Fact]
    public void BoundsAndValidatesUnicodeWithoutTruncatingTheQuery()
    {
        Assert.NotNull(WebSearchQuery.Build(new string('a', 2048)));
        Assert.Null(WebSearchQuery.Build(new string('a', 2049)));
        Assert.Null(WebSearchQuery.Build("a" + (char)0xD800));
        Assert.Null(WebSearchQuery.Build("a" + (char)0xDC00));
        Assert.True(new Preferences().ShowWebSearch);
    }
}
