using ReStart.App.Services;
using ReStart.Core;
using Windows.ApplicationModel.DataTransfer;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks real StorageFile payloads and unavailable targets without opening a receiver or sending any data.</summary>
    private static async Task<int> VerifyFileCommandsAsync(MainWindow owner, string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        foreach (var name in new[] { "Sample 日本語 & (one).py", "Image.png", "Document.pdf", "Archive.zip", "Unknown.extension-fixture", "No extension" })
        {
            var path = Path.Combine(directory, name); File.WriteAllText(path, "owned payload fixture; never executed");
            var app = FilePinning.FromPath(path);
            var verbs = await AppShellCommands.ReadAsync(app);
            Check(verbs.Contains("properties"), "An existing file has no filesystem properties action.");
            Check(verbs.Contains("share") == FileShareSession.IsSupported, "An ordinary file's Share action disagrees with platform capability.");
            var file = await FileShareSession.PrepareAsync(app);
            var data = new DataPackage(); FileShareSession.Populate(data, file);
            var view = data.GetView();
            var items = await view.GetStorageItemsAsync();
            Check(view.Contains(StandardDataFormats.StorageItems) && items.Count == 1 && items[0].Path == path
                && view.Properties.Title == name && view.RequestedOperation == DataPackageOperation.Copy,
                "A Share payload substituted the file, its type, name or requested operation.");
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(!await FileShareSession.CanShareAsync(app), "A read-locked file still advertises Share.");
            File.Delete(path);
            Check(!await FileShareSession.CanShareAsync(app) && !(await AppShellCommands.ReadAsync(app)).Contains("properties"),
                "A deleted file still advertises Share or Properties.");
            try { await FileShareSession.PrepareAsync(app); throw new InvalidOperationException("A stale file was prepared for sharing."); }
            catch (InvalidOperationException ex) when (ex.Message == L.T(K.ErrorAppCommandUnavailable)) { checks++; }
        }
        var folder = new AppEntry(directory, "Owned folder", directory, false);
        var virtualApp = new AppEntry("missing!app", "Missing app", "missing!app");
        var protocol = new AppEntry("protocol", "Protocol", "ms-settings:", false);
        foreach (var app in new[] { folder, virtualApp, protocol })
            Check(!await FileShareSession.CanShareAsync(app) && !(await AppShellCommands.ReadAsync(app)).Contains("share"),
                "An application, protocol or directory advertises file sharing.");
        Check(!(await AppShellCommands.ReadAsync(virtualApp)).Contains("properties") && !(await AppShellCommands.ReadAsync(protocol)).Contains("properties"),
            "A virtual identity has a filesystem Properties action.");
        foreach (var verb in new[] { "share", "properties" })
        {
            try { await AppShellCommands.InvokeAsync(folder, verb, 0); throw new IOException("A file dialog reached the short-lived Shell STA."); }
            catch (InvalidOperationException) { checks++; }
        }
        if (FileShareSession.IsSupported)
        {
            var path = Path.Combine(directory, "Focus lifetime.py"); File.WriteAllText(path, "owned fixture");
            var file = await FileShareSession.PrepareAsync(FilePinning.FromPath(path));
            var completed = 0;
            using var session = new FileShareSession(owner, file, () => completed++);
            // A menu, transient window or visibility change is not confirmation
            // from Windows that the share operation completed or was canceled.
            owner.Hide(); owner.Show(); owner.Activate(); await Task.Delay(30);
            Check(!session.IsDisposed && completed == 0, "Focus churn ended sharing before Windows requested its file.");
            session.Dispose(); session.Dispose();
            Check(session.IsDisposed && completed == 0, "Explicit share teardown is not idempotent or reports an OS completion.");
        }
        return checks;
    }
}
