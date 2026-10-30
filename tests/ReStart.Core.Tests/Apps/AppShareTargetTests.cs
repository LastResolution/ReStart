using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class AppShareTargetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ReStart-share-" + Guid.NewGuid().ToString("N"));
    public AppShareTargetTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("Example.py")]
    [InlineData("Photo.png")]
    [InlineData("Document.pdf")]
    [InlineData("Archive.zip")]
    [InlineData("Text 日本語 & (one).txt")]
    [InlineData("Extensionless")]
    public void Explicit_existing_files_keep_their_own_identity(string name)
    {
        var path = Path.Combine(_root, name); File.WriteAllText(path, "owned fixture");
        Assert.Equal(path, AppShareTarget.Find(FilePinning.FromPath(path)));
    }

    [Theory]
    [InlineData("App.lnk")]
    [InlineData("Website.URL")]
    public void Shortcuts_are_not_resolved_or_shared(string name)
    {
        var path = Path.Combine(_root, name); File.WriteAllText(path, "owned shortcut fixture");
        Assert.Null(AppShareTarget.Find(FilePinning.FromPath(path)));
        Assert.Equal(path, AppFileLocation.Find(FilePinning.FromPath(path))?.Path);
    }

    [Fact]
    public void Applications_do_not_share_their_catalog_source_or_target()
    {
        var path = Path.Combine(_root, "App.exe"); File.WriteAllText(path, "owned non-executed fixture");
        var app = new AppEntry("AppId!main", "Application", path) { ShortcutPath = path };
        Assert.Null(AppShareTarget.Find(app));
        Assert.Null(AppShareTarget.Find(app with { IsShellApp = false }));
        Assert.Null(AppShareTarget.Find(app with { ShortcutPath = "" }));
        // An explicitly pinned executable is a file, not a catalog identity.
        Assert.Equal(path, AppShareTarget.Find(FilePinning.FromPath(path)));
    }

    [Fact]
    public void Folders_missing_stale_and_virtual_items_have_no_share_target()
    {
        Assert.Null(AppShareTarget.Find(null));
        Assert.Null(AppShareTarget.Find(FilePinning.FromPath(_root)));
        Assert.Null(AppShareTarget.Find(new("virtual", "Virtual", "ms-settings:", false)));
        Assert.Null(AppShareTarget.Find(new("relative", "Relative", "file.py", false)));
        var path = Path.Combine(_root, "Removed.py"); File.WriteAllText(path, "owned fixture");
        var app = FilePinning.FromPath(path); File.Delete(path);
        Assert.Null(AppShareTarget.Find(app));
        Assert.Null(AppFileLocation.Find(app));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
