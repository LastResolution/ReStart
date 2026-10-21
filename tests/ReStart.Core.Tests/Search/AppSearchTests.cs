using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class AppSearchTests
{
    private static readonly AppEntry[] Apps = [new("1", "Visual Studio Code", "1"), new("2", "Code", "2"), new("3", "メモ帳", "3"), new("4", "Microsoft Edge", "4")];
    [Fact] public void RanksExactNameFirst() => Assert.Equal("2", AppSearch.Find(Apps, "code")[0].Id);
    [Fact] public void MatchesFullWidthLatinCaseInsensitively() => Assert.Equal("1", Assert.Single(AppSearch.Find(Apps, "ＶＩＳＵＡＬ　code")).Id);
    [Theory]
    [InlineData("めも")]
    [InlineData("ﾒﾓ")]
    [InlineData("メモ")]
    public void MatchesJapaneseKanaVariants(string query) => Assert.Equal("3", Assert.Single(AppSearch.Find(Apps, query)).Id);
    [Fact] public void AllTermsMustMatch() => Assert.Empty(AppSearch.Find(Apps, "visual edge"));
    [Fact] public void ResultLimitIsRespected() => Assert.Equal(2, AppSearch.Find(Apps, "", 2).Count);
}
