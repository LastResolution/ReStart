using System.Text;
using System.Xml;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class AppSearchAliasTests
{
    [Theory]
    [InlineData("notepad")]
    [InlineData("NOTEPAD.EXE")]
    [InlineData("ｎｏｔｅｐａｄ")]
    [InlineData("メモ")]
    public void LocalizedNameAndExecutableFindTheSameOriginalEntry(string query)
    {
        var app = AppSearchAliases.AddExecutables(new("notepad-package!App", "メモ帳", "notepad-package!App"), [@"C:\Windows\System32\notepad.exe"]);
        Assert.Same(app, Assert.Single(AppSearch.Find([app], query)));
        Assert.Equal("notepad-package!App", app.LaunchTarget);
    }

    [Theory]
    [InlineData("cmd.exe --secret value")]
    [InlineData("\"cmd.exe\" /c echo secret")]
    [InlineData("https://example.test/app.exe")]
    [InlineData("file.txt")]
    [InlineData("file.lnk")]
    [InlineData("bad\nname.exe")]
    [InlineData("")]
    public void RejectsNonExecutableOrCommandValues(string value)
    {
        // URL paths are rejected rather than indexing a remote resource as an executable.
        Assert.Empty(AppSearchAliases.AddExecutables(new("a", "App", ""), [value]).SearchAliases);
    }

    [Fact]
    public void OnlyBasenamesAreSearchableAndSourcesAreNotMutated()
    {
        var original = new AppEntry("a", "Editor", "original-shortcut.lnk", false);
        var names = new[] { @"C:\PrivateDirectory\Editor.EXE", @"c:/other/editor.exe" };
        var enriched = AppSearchAliases.AddExecutables(original, names);
        Assert.Equal(new[] { "Editor.EXE", "Editor" }, enriched.SearchAliases);
        Assert.Empty(original.SearchAliases);
        Assert.Empty(AppSearch.Find([enriched], "PrivateDirectory"));
        Assert.Equal(original.LaunchTarget, enriched.LaunchTarget);
        Assert.Equal(@"C:\PrivateDirectory\Editor.EXE", names[0]);
    }

    [Fact]
    public void ExactDisplayNameThenExactAliasBeatNamePrefixes()
    {
        var named = new AppEntry("named", "cmd", "");
        var command = AppSearchAliases.AddExecutables(new("command", "コマンド プロンプト", ""), ["cmd.exe"]);
        var prefix = new AppEntry("prefix", "Cmd Helper", "");
        Assert.Equal(new[] { "named", "command", "prefix" }, AppSearch.Find([prefix, command, named], "cmd").Select(a => a.Id));
    }

    [Fact]
    public void GenericCommandRanksAheadOfArgumentBearingDeveloperPrompts()
    {
        var prompt = AppSearchAliases.AddExecutables(new("prompt", "コマンド プロンプト", ""), ["cmd.exe"]);
        var developer = AppSearchAliases.AddExecutables(new("developer", "A Developer Prompt", ""), ["cmd.exe"], secondary: true);
        var helper = new AppEntry("helper", "Cmd Helper", "");
        Assert.Equal(new[] { "prompt", "helper", "developer" }, AppSearch.Find([developer, helper, prompt], "cmd").Select(a => a.Id));
        Assert.Same(developer, Assert.Single(AppSearch.Find([developer, helper, prompt], "developer cmd")));
    }

    [Fact]
    public void AllTermsMustMatchNamesOrAliasesWithoutDuplicatingRows()
    {
        var app = AppSearchAliases.AddExecutables(new("a", "Sample Editor", ""), ["code.exe", "editor.exe"]);
        Assert.Same(app, Assert.Single(AppSearch.Find([app], "sample code")));
        Assert.Empty(AppSearch.Find([app], "sample unrelated"));
        Assert.Single(AppSearch.Find([app], "editor"));
    }

    [Fact]
    public void AliasesNeverExposeLaunchTargetsArgumentsOrAppIds()
    {
        var app = new AppEntry("private-package-id", "Editor", @"C:\SecretDirectory\secret.exe", false);
        Assert.Empty(AppSearch.Find([app], "secret"));
        Assert.Empty(AppSearch.Find([app], "private-package"));
    }

    [Fact]
    public void MissingMetadataAndEmptyQueriesRetainExistingBehavior()
    {
        var apps = new[] { new AppEntry("z", "Zulu", ""), new AppEntry("a", "Alpha", "") };
        Assert.Equal("a", Assert.Single(AppSearch.Find(apps, "", 1)).Id);
        Assert.Empty(AppSearch.Find(apps, "cmd"));
        Assert.Empty(AppSearch.Find(apps, "", 0));
        Assert.Empty(AppSearchAliases.AddExecutables(apps[0], null).SearchAliases);
    }

    [Fact]
    public void AliasesAreBoundedAndCaseInsensitive()
    {
        var app = AppSearchAliases.AddExecutables(new("a", "App", ""), Enumerable.Range(0, 100).Select(i => $"app{i}.exe"));
        Assert.Equal(64, app.SearchAliases.Count);
        Assert.Equal(64, app.SearchAliases.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ShortcutAndRegisteredNamesPreserveShellIdentityAndArgumentDisambiguation()
    {
        var shortcut = new StartMenuShortcut("Tools/Prompt.lnk", new("shortcut", "Prompt", "prompt.lnk", false, "Tools"),
            TargetPath: @"C:\Windows\System32\cmd.exe", Arguments: "/k private-argument");
        var result = AppCatalogLayout.Merge([new("shell", "コマンド プロンプト", "shell-launch")], [shortcut],
            [new("shell", @"C:\Windows\System32\cmd.exe", "/k private-argument")],
            [new("console.exe", "c:/windows/system32/CMD.EXE"), new("stale.exe", @"C:\Missing\stale.exe")]);
        var app = Assert.Single(result);
        Assert.Equal("shell", app.Id);
        Assert.Equal("shell-launch", app.LaunchTarget);
        Assert.Equal("Tools", app.FolderPath);
        Assert.Same(app, Assert.Single(AppSearch.Find(result, "console")));
        Assert.Same(app, Assert.Single(AppSearch.Find(result, "cmd")));
        Assert.Empty(AppSearch.Find(result, "private-argument"));
        Assert.Empty(AppSearch.Find(result, "stale"));
    }

    [Fact]
    public void UserShortcutPrecedenceDoesNotInheritTheCommonTargetsAlias()
    {
        var user = new StartMenuShortcut("App.lnk", new("user", "Editor", "user.lnk", false), TargetPath: @"C:\Tools\user.exe");
        var common = new StartMenuShortcut("app.LNK", new("common", "Editor", "common.lnk", false), TargetPath: @"C:\Tools\common.exe");
        var result = AppCatalogLayout.Merge([], [user, common]);
        Assert.Equal("user", Assert.Single(AppSearch.Find(result, "user")).Id);
        Assert.Empty(AppSearch.Find(result, "common"));
    }

    [Fact]
    public void AnUnmatchedShortcutKeepsItsExactLaunchPath()
    {
        var result = AppCatalogLayout.Merge([], [new("Alias.lnk", new("file", "Alias", "alias.lnk", false), TargetPath: @"C:\Tools\actual.exe")]);
        Assert.Equal("alias.lnk", Assert.Single(AppSearch.Find(result, "actual")).LaunchTarget);
    }

    [Fact]
    public void ARefreshedCatalogDoesNotRetainRemovedAliases()
    {
        var old = AppCatalogLayout.Merge([new("a", "App", "")], [], [new("a", @"C:\Tools\old.exe")]);
        var current = AppCatalogLayout.Merge([new("a", "App", "")], [], [new("a", @"C:\Tools\current.exe")]);
        Assert.Single(AppSearch.Find(old, "old"));
        Assert.Empty(AppSearch.Find(current, "old"));
        Assert.Single(AppSearch.Find(current, "current"));
    }

    [Fact]
    public void PackageAliasesBelongOnlyToTheirApplication()
    {
        using var stream = Xml("""
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
                     xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10">
              <Applications>
                <Application Id="Editor" Executable="Tools\editor.exe"><Extensions>
                  <uap3:Extension Category="windows.appExecutionAlias"><uap3:AppExecutionAlias>
                    <desktop:ExecutionAlias Alias="edit.exe"/>
                  </uap3:AppExecutionAlias></uap3:Extension>
                  <uap3:Extension Category="other"><desktop:ExecutionAlias Alias="unrelated.exe"/></uap3:Extension>
                </Extensions></Application>
                <Application Id="Terminal" Executable="terminal.exe"><Extensions>
                  <uap3:Extension Category="windows.appExecutionAlias"><uap3:AppExecutionAlias>
                    <desktop:ExecutionAlias Alias="wt.exe"/>
                  </uap3:AppExecutionAlias></uap3:Extension>
                </Extensions></Application>
              </Applications>
            </Package>
            """);
        var result = AppSearchAliases.ReadPackageManifest(stream);
        Assert.Equal(new[] { "editor.exe", "editor", "edit.exe", "edit" }, result["Editor"]);
        Assert.Equal(new[] { "terminal.exe", "terminal", "wt.exe", "wt" }, result["Terminal"]);
        Assert.DoesNotContain("unrelated", result["Editor"]);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData("<!DOCTYPE Package [<!ENTITY example SYSTEM 'file:///not-read'>]><Package>&example;</Package>")]
    [InlineData("<Package>")]
    public void RejectsUnsafeOrMalformedPackageXml(string xml)
    {
        using var stream = Xml(xml);
        Assert.Throws<XmlException>(() => AppSearchAliases.ReadPackageManifest(stream));
    }

    [Fact]
    public void RejectsOversizedPackageXml()
    {
        using var stream = Xml("<Package>" + new string(' ', 2_000_001) + "</Package>");
        Assert.Throws<XmlException>(() => AppSearchAliases.ReadPackageManifest(stream));
    }

    private static MemoryStream Xml(string text) => new(Encoding.UTF8.GetBytes(text));
}
