using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class SeedAppSelectorTests
{
    [Fact]
    public void StablePackageIdentitiesWorkWithoutRecognizableDisplayNames()
    {
        var apps = new[]
        {
            new AppEntry("MSEdge", "Browser fixture", "MSEdge"),
            Packaged("Microsoft.OutlookForWindows", "Mail fixture"),
            Packaged("Microsoft.Windows.Photos", "Photo fixture"),
            Packaged("Microsoft.WindowsStore", "Store fixture"),
            Packaged("Microsoft.WindowsNotepad", "Text fixture"),
            Packaged("Microsoft.WindowsCalculator", "Calculation fixture"),
            Packaged("Microsoft.Paint", "Drawing fixture"),
            Packaged("Microsoft.WindowsTerminal", "Console fixture")
        };

        var selected = SeedAppSelector.Select(apps);

        Assert.Equal(new[] { apps[0], apps[1], apps[2], apps[3] }, selected.Everyday);
        Assert.Equal(new[] { apps[4], apps[5], apps[6], apps[7] }, selected.Tools);
    }

    [Fact]
    public void ExactAliasesCoverAllSupportedLanguagesWithoutChangingTheUiLanguage()
    {
        var before = TextCatalog.Language;
        foreach (var language in TextCatalog.SupportedLanguages)
        {
            var keys = new[] { K.AppMail, K.AppPhotos, K.AppNotepad, K.AppCalculator, K.AppPaint };
            var apps = keys.Select(key => new AppEntry(key.ToString(), TextCatalog.Translate(key, language), key + ".lnk", false)).ToArray();
            var selected = SeedAppSelector.Select(apps);
            Assert.Equal(apps, selected.Everyday.Concat(selected.Tools));
        }
        Assert.Equal(before, TextCatalog.Language);
    }

    [Fact]
    public void HelperAndDocumentNamesCannotWinBySubstring()
    {
        var apps = new[]
        {
            new AppEntry("helper", "Microsoft Edge WebView2 Runtime", "helper"),
            new AppEntry("installer", "Install Microsoft Edge", "installer"),
            new AppEntry("help", "Notepad++ Help", "help.lnk", false),
            new AppEntry("uninstall", "Uninstall Spotify", "uninstall.lnk", false),
            new AppEntry("document", "Photos", "sample.txt", false),
            new AppEntry("website", "Visual Studio Code", "sample.url", false),
            new AppEntry("purchase", "Store purchase fixture", "Microsoft.StorePurchaseApp_fixture!App"),
            new AppEntry("unrelated", "Sample business app", "unrelated")
        };

        var selected = SeedAppSelector.Select(apps);

        Assert.Empty(selected.Everyday);
        Assert.Empty(selected.Tools);
    }

    [Fact]
    public void OneBrowserPerRolePrefersAStableIdentityRegardlessOfCatalogOrder()
    {
        var edge = new AppEntry("MSEdge", "Browser fixture", "MSEdge");
        var chrome = new AppEntry("Chrome", "Google Chrome", "Chrome");
        var shortcut = new AppEntry("shortcut", "Microsoft Edge", "edge.lnk", false);
        var apps = new[] { shortcut, chrome, edge, new AppEntry("code", "Visual Studio Code", "code") };

        Assert.Same(edge, Assert.Single(SeedAppSelector.Select(apps).Everyday));
        var forward = SeedAppSelector.Select(apps);
        var reverse = SeedAppSelector.Select(apps.Reverse().ToArray());
        Assert.Equal(forward.Everyday, reverse.Everyday);
        Assert.Equal(forward.Tools, reverse.Tools);
    }

    [Fact]
    public void DuplicateIdsAndTargetsCannotOccupyTwoRoles()
    {
        var apps = new[]
        {
            new AppEntry("shared", "Microsoft Edge", "shell:AppsFolder\\same"),
            new AppEntry("SHARED", "Outlook", "other"),
            new AppEntry("different", "Notepad", "SAME"),
            new AppEntry("calculator", "Calculator", "calculator")
        };
        var selected = SeedAppSelector.Select(apps);

        Assert.Single(selected.Everyday);
        Assert.Equal("calculator", Assert.Single(selected.Tools).Id);
    }

    [Fact]
    public void ToolCountIsBoundedAndDoesNotMutateTheCatalog()
    {
        var apps = new[] { "Notepad", "Calculator", "Visual Studio Code", "Paint", "Windows Terminal" }
            .Select(name => new AppEntry(name, name, name)).ToArray();

        var selected = SeedAppSelector.Select(apps);

        Assert.Equal(4, selected.Tools.Count);
        Assert.DoesNotContain(selected.Tools, app => app.Name == "Windows Terminal");
        Assert.Equal(5, apps.Length);
        Assert.All(selected.Tools, app => Assert.Contains(app, apps));
        Assert.Empty(SeedAppSelector.Select([]).Everyday);
        Assert.Throws<ArgumentNullException>(() => SeedAppSelector.Select(null!));
    }

    private static AppEntry Packaged(string family, string name) => new(family + "_fixture!App", name, family + "_fixture!App");

}
