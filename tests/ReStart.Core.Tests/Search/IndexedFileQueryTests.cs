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
        Assert.Contains("TOP 50", sql);
        Assert.Contains("System.ItemUrl LIKE 'file:%'", sql);
        Assert.Null(IndexedFileQuery.Build(new string('a', 129)));
    }

    [Theory]
    [InlineData("O'Brien OR 1=1 --")]
    [InlineData("\"literal phrase\" AND NOT other")]
    [InlineData("日本語 résumé %_[text]")]
    public void ContentsAreOptInAndTreatInputAsFullTextNotSqlOrContainsGrammar(string query)
    {
        Assert.DoesNotContain("FREETEXT", IndexedFileQuery.Build(query));
        var sql = IndexedFileQuery.Build(query, true)!;
        Assert.Contains("OR FREETEXT(System.Search.Contents, '" + query.Replace("'", "''") + "')", sql);
        Assert.Contains("System.ItemUrl LIKE 'file:%' AND (", sql);
        Assert.Contains("SELECT TOP " + IndexedFileQuery.MaximumResults, sql);
        Assert.DoesNotContain("CONTAINS(", sql);
    }
}
