using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class AppSearchUsageTests
{
    private static readonly AppEntry[] Apps = [new("a", "Alpha Editor", "a"), new("b", "Beta Editor", "b"), new("exact", "Editor", "exact")];
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UsageBreaksEqualMatchesWithoutDisplacingAnExactName()
    {
        var usage = new Dictionary<string, WindowsAppUsage> { ["b"] = new(20, Now) };
        Assert.Equal(new[] { "exact", "b", "a" }, AppSearch.Find(Apps, "editor", usage: usage).Select(a => a.Id));
        Assert.Equal(new[] { "exact", "a", "b" }, AppSearch.Find(Apps, "editor").Select(a => a.Id));
        Assert.Equal(20, usage["b"].Launches);
    }

    [Fact]
    public void AliasExactMatchKeepsPriorityOverPopularNameSubstring()
    {
        var exact = AppSearchAliases.AddExecutables(new("console", "Console", ""), ["cmd.exe"]);
        var weak = new AppEntry("weak", "Some cmd tool", "");
        var usage = new Dictionary<string, WindowsAppUsage> { ["weak"] = new(1_000_000, Now) };
        Assert.Equal(exact, AppSearch.Find([weak, exact], "cmd", usage: usage)[0]);
        Assert.Empty(AppSearch.Find([weak, exact], "unrelated", usage: usage));
    }

    [Fact]
    public void EqualCountsUseLatestLaunchThenTheSuppliedWindowsNameComparer()
    {
        var usage = new Dictionary<string, WindowsAppUsage> { ["a"] = new(2, Now.AddMinutes(-1)), ["b"] = new(2, Now) };
        Assert.Equal("b", AppSearch.Find(Apps, "editor", usage: usage)[1].Id);
        usage["a"] = new(2, Now);
        var reverse = Comparer<string>.Create((a, b) => StringComparer.Ordinal.Compare(b, a));
        Assert.Equal("b", AppSearch.Find(Apps, "editor", usage: usage, names: reverse)[1].Id);
        Assert.Equal("a", AppSearch.Find(Apps, "editor", usage: usage)[1].Id);
    }

    [Fact]
    public void BlankQueriesRemainAlphabeticalAndLimitsApplyAfterUsageRanking()
    {
        var usage = new Dictionary<string, WindowsAppUsage> { ["b"] = new(500, Now), ["missing"] = new(int.MaxValue, Now) };
        Assert.Equal("a", AppSearch.Find(Apps, "", 1, usage)[0].Id);
        Assert.Equal(new[] { "exact", "b" }, AppSearch.Find(Apps, "editor", 2, usage).Select(a => a.Id));
        Assert.Empty(AppSearch.Find(Apps, "editor", 0, usage));
    }
}
