using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class AppFileLocationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReStart-tests-" + Guid.NewGuid().ToString("N"));
    public AppFileLocationTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void UnicodeFilesAndDirectoriesAreSelectedWithoutParsingArguments()
    {
        var path = Path.Combine(_directory, "Sample 日本語, & # (one).txt"); File.WriteAllText(path, "fixture");
        Assert.Equal(new(path, false), AppFileLocation.Find(FilePinning.FromPath(path)));
        Assert.Equal(new(_directory, true), AppFileLocation.Find(FilePinning.FromPath(_directory)));
    }

    [Fact]
    public void StartMenuShortcutWinsWithoutChangingOrSearchingTheLaunchIdentity()
    {
        var shortcut = Path.Combine(_directory, "Command sample.lnk"); File.WriteAllText(shortcut, "not executed");
        var app = new AppEntry("shell-fixture", "Localized sample", "shell-fixture") { ShortcutPath = shortcut };
        Assert.Equal(new(shortcut, false), AppFileLocation.Find(app));
        Assert.Equal("shell-fixture", app.LaunchTarget);
        Assert.Empty(AppSearch.Find([app], "Command sample"));
        Assert.Empty(AppSearch.Find([app], _directory));
    }

    [Fact]
    public void LocalShortcutsAndInternetShortcutsRevealTheirOwnLocationWithoutResolvingThem()
    {
        foreach (var extension in new[] { ".lnk", ".url" })
        {
            var path = Path.Combine(_directory, "Sample" + extension); File.WriteAllText(path, "not resolved");
            Assert.Equal(new(path, false), AppFileLocation.Find(FilePinning.FromPath(path)));
        }
    }

    [Fact]
    public void RemovedShortcutDoesNotInventAFallbackFromAShellIdentity()
    {
        var path = Path.Combine(_directory, "missing.lnk");
        Assert.Null(AppFileLocation.Find(new("shell-fixture", "Sample", "shell-fixture") { ShortcutPath = path }));
        Assert.Null(AppFileLocation.Find(new("package!app", "Packaged", _directory)));
    }

    [Theory]
    [InlineData("ms-settings:display")]
    [InlineData("https://example.invalid/app")]
    [InlineData("relative.exe")]
    [InlineData("\"quoted.exe\"")]
    [InlineData("bad\0path.exe")]
    public void ProtocolsAndInvalidPathsHaveNoFilesystemLocation(string target) =>
        Assert.Null(AppFileLocation.Find(new(target, "Sample", target, false)));

    [Fact]
    public void CatalogMergePreservesTheUserShortcutAsLocationMetadata()
    {
        var user = Path.Combine(_directory, "User.lnk"); File.WriteAllText(user, "fixture");
        var common = Path.Combine(_directory, "Common.lnk"); File.WriteAllText(common, "fixture");
        var app = AppCatalogLayout.Merge([new("shell", "Sample", "shell")],
            [new("Sample.lnk", new(user, "Sample", user, false), "shell"), new("Sample.lnk", new(common, "Sample", common, false), "shell")]).Single();
        Assert.Equal(user, app.ShortcutPath); Assert.Equal("shell", app.Id); Assert.Equal("shell", app.LaunchTarget);
        Assert.Equal(new(user, false), AppFileLocation.Find(app));
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
