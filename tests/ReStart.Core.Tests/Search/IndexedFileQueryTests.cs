namespace ReStart.Core.Tests;

public class IndexedFileQueryTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    [InlineData("ab\ncd")]
    public void RejectsEmptyShortAndControlInput(string text) => Assert.Null(IndexedFileQuery.Build(text));

    [Fact]
    public void TreatsQuotesAndWildcardCharactersAsLiterals()
    {
        var sql = IndexedFileQuery.Build("O'Brien_[x]%");
        Assert.Contains("O''Brien[_][[]x[]][%]", sql);
        Assert.Contains("TOP 20", sql);
        Assert.Contains("System.ItemUrl LIKE 'file:%'", sql);
        Assert.Null(IndexedFileQuery.Build(new string('a', 129)));
    }
}
