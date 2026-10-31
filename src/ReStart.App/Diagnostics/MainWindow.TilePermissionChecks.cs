using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private static async Task<int> VerifyHostDataApprovalsAsync(MainWindow fixture, string directory)
    {
        var checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        var tile = fixture._scriptPackages.Import(ScriptTilePackages.BundledManifestPath("json-monitor"));
        var manifest = fixture._scriptPackages.Read(tile.ScriptId).Manifest;
        var bindingPath = fixture._scriptPackages.DataBindingPath(tile.ScriptId, tile.Id);
        var grantPath = fixture._scriptPackages.DataGrantPath(tile.ScriptId, tile.Id);
        var source = manifest.DataSources.Keys.Single();
        var file = Path.Combine(directory, "permission-fixture.json");
        File.WriteAllText(file, "{\"text\":\"Approved fixture\"}");
        var bindings = new Dictionary<string, TileDataBinding> { [source] = new() { Enabled = true, Access = "file", Location = file } };
        ScriptDataBindings.Save(bindingPath, bindings);
        using var transport = new TileDataTransport();
        ScriptDataRuntime Runtime(Tile current) => new(fixture._scriptPackages.DataBindingPath(current.ScriptId, current.Id),
            fixture._scriptPackages.DataGrantPath(current.ScriptId, current.Id), current.Id, current.ScriptId, manifest,
            () => fixture._scriptPackages.Read(current.ScriptId).Manifest, transport);
        using var deniedRuntime = Runtime(tile);
        Check(deniedRuntime.Snapshot(DateTimeOffset.UtcNow)[source].Status == "permission", "An enabled connection or manifest declaration created approval.");
        var editor = new TileDataWindow(fixture, manifest, bindingPath, new(grantPath, tile.Id, tile.ScriptId));
        editor.Loaded += (_, _) => editor.Dispatcher.BeginInvoke(() => editor.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        Check(editor.ShowDialog() == true, "The owned approval editor did not save explicit consent.");
        bindings = ScriptDataBindings.Read(bindingPath);
        using var runtime = Runtime(tile);
        Check(deniedRuntime.Snapshot(DateTimeOffset.UtcNow)[source].Status == "permission", "A stale runtime used approval for a changed connection.");
        var approval = ScriptTileGrants.Read(grantPath);
        Check(approval?.TileId == tile.Id && approval.Entities.SequenceEqual(new[] { source }) && approval.Actions.SequenceEqual(new[] { "read" }),
            "Approval was not limited to the tile, enabled source and read action.");
        async Task Ready(ScriptDataRuntime target)
        {
            var deadline = Environment.TickCount64 + 2500;
            while (target.Snapshot(DateTimeOffset.UtcNow)[source].Status != "ready" && Environment.TickCount64 < deadline) await Task.Delay(25);
            Check(target.Snapshot(DateTimeOffset.UtcNow)[source].Status == "ready", "Explicit approval did not enable the owned provider.");
        }
        await Ready(runtime);
        File.Delete(grantPath);
        var denied = runtime.Snapshot(DateTimeOffset.UtcNow)[source];
        Check(denied.Status == "permission" && denied.Value.ValueKind == JsonValueKind.Null && !denied.Stale,
            "Revocation retained cached host data.");
        ScriptTileGrants.ApproveData(grantPath, tile.Id, tile.ScriptId, manifest, bindings);
        await Ready(runtime);
        var clone = new Tile { Widget = "script", ScriptId = tile.ScriptId };
        ScriptDataBindings.Save(fixture._scriptPackages.DataBindingPath(clone.ScriptId, clone.Id), bindings);
        File.Copy(grantPath, fixture._scriptPackages.DataGrantPath(clone.ScriptId, clone.Id));
        using var clonedRuntime = Runtime(clone);
        Check(clonedRuntime.Snapshot(DateTimeOffset.UtcNow)[source].Status == "permission", "Copying encrypted approval authorized another tile identity.");
        var originalPointer = manifest.DataSources[source].JsonPointer;
        void SaveManifest()
        {
            var package = fixture._scriptPackages.Read(tile.ScriptId);
            var web = manifest.Renderer == "web" ? fixture._scriptPackages.ReadWeb(tile.ScriptId) : default;
            fixture._scriptPackages.SaveInstalled(tile.ScriptId, JsonSerializer.Serialize(manifest, ScriptTileProtocol.Json), package.Source, web.Html, web.Css);
        }
        manifest.DataSources[source].JsonPointer = "/expanded";
        SaveManifest();
        Check(runtime.Snapshot(DateTimeOffset.UtcNow)[source].Status == "permission", "Expanded package requests reused prior approval.");
        manifest.DataSources[source].JsonPointer = originalPointer;
        SaveManifest();
        bindings[source].Location = Path.Combine(directory, "different-permission-fixture.json");
        ScriptDataBindings.Save(bindingPath, bindings);
        Check(runtime.Snapshot(DateTimeOffset.UtcNow)[source].Status == "permission", "Changed connections reused an old runtime or approval.");
        return checks;
    }
}
