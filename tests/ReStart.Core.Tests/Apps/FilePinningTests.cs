using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.Core.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class FilePinningTests
{
    [Fact]
    public void FilesDirectoriesAndShortcutsKeepTheirExactLaunchPathAndDeduplicate()
    {
        var root = Path.Combine(Path.GetTempPath(), "ReStart pin " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var path in new[] { root, Path.Combine(root, "report sample.txt"), Path.Combine(root, "shortcut sample.lnk"), Path.Combine(root, "website.url") })
            {
                if (path != root) File.WriteAllText(path, "inert fixture");
                var entry = FilePinning.FromPath(path);
                Assert.False(entry.IsShellApp);
                Assert.Equal(path, entry.LaunchTarget);
                var document = new LayoutDocument();
                var first = AppPinning.Pin(document, entry);
                Assert.Same(first, AppPinning.Pin(document, entry with { Id = "alternate-id" }));
                Assert.False(first.IsFolder);
                Assert.Single(document.Groups);
                if (Path.GetExtension(path) is ".lnk" or ".url") Assert.Equal(Path.GetFileNameWithoutExtension(path), first.Label);
            }
            Assert.Throws<FileNotFoundException>(() => FilePinning.FromPath(Path.Combine(root, "missing")));
            Assert.Throws<ArgumentException>(() => FilePinning.FromPath("https://example.invalid"));
            Assert.Throws<ArgumentException>(() => FilePinning.FromPath("--pin"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TheExistingProcessAcknowledgesEveryPinAndRejectsAnAbsentServer()
    {
        if (!OperatingSystem.IsWindows()) return;
        var identity = "test-" + Guid.NewGuid().ToString("N");
        var received = new List<string>();
        using var channel = new PinRequestChannel(identity, path => { received.Add(path); return Task.FromResult(new PinReply(true)); });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var path in new[] { @"C:\Fixture path\日本語 folder", @"C:\Fixture path\report (final).txt", @"\\server\share\shortcut.lnk" })
            Assert.True((await PinRequestChannel.SendAsync(identity, path, deadline.Token)).Success);
        Assert.Equal(3, received.Count);
        using var absent = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PinRequestChannel.SendAsync(identity + "-absent", "fixture", absent.Token));
    }

    [Fact]
    public void ExplorerRegistrationIsQuotedLocalizedAndReversibleInAnIsolatedKey()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = @"Software\ReStart.Tests\" + Guid.NewGuid().ToString("N");
        var registration = new ExplorerPinRegistration(root);
        const string executable = @"C:\Fixture path\ReStart.exe";
        try
        {
            Assert.False(registration.IsEnabled);
            registration.SetEnabled(true, executable);
            Assert.True(registration.IsEnabled);
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(root + @"\AllFilesystemObjects\shell\ReStart.Pin");
            Assert.Equal(TextCatalog.T(TextKey.PinToReStart), key!.GetValue(""));
            Assert.Equal("Single", key.GetValue("MultiSelectModel"));
            using var command = key.OpenSubKey("command");
            Assert.Equal("\"" + executable + "\" --pin \"%1\"", command!.GetValue(""));
            registration.SetEnabled(false);
            Assert.False(registration.IsEnabled);
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(root, false); }
    }
}
