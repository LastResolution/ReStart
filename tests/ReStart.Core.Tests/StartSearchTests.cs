using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class StartSearchTests
{
    [Fact]
    public void SingleEIncludesEdgeBeforeSubstringNoiseHitsLimit()
    {
        var apps = Enumerable.Range(0, 150).Select(i => new AppEntry("noise" + i, "A developer tool " + i, "none")).ToList();
        apps.Add(new("edge", "Microsoft Edge", "edge.exe"));
        apps.Add(new("excel", "Excel", "excel.exe"));
        var results = AppSearch.Find(apps, "E");
        Assert.Equal("excel", results[0].Id);
        Assert.Equal("edge", results[1].Id);
        Assert.Equal("edge", AppSearch.Find(apps, "edge")[0].Id);
    }
    [Fact]
    public void LetterAfterBareWindowsReleaseIsNormalTyping()
    {
        var state = new WindowsKeyState();
        state.Process(0x5B, true);
        Assert.Equal(WindowsKeyAction.Toggle, state.Process(0x5B, false));
        Assert.Equal(0, state.HeldKey);
        Assert.Equal(WindowsKeyAction.Pass, state.Process(0x45, true));
        Assert.Equal(WindowsKeyAction.Pass, state.Process(0x45, false));
    }
}
