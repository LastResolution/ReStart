using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks display-refresh browser replacement and in-flight reply isolation without changing display settings or showing windows.</summary>
    /// <param name="directory">Isolated local directory receiving a report with counts only; no user configuration is read.</param>
    /// <returns>The number of lifecycle, state, input and layout preservation assertions completed.</returns>
    internal async Task<int> VerifyWebTileRecoveryAsync(string directory)
    {
        if (!_preview) throw new InvalidOperationException("Web recovery verification requires preview mode.");
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        async Task UntilAsync(Func<bool> predicate, string message)
        {
            var deadline = Environment.TickCount64 + 10000;
            while (!predicate())
            {
                if (Environment.TickCount64 >= deadline) throw new InvalidOperationException(message);
                await Task.Delay(16);
            }
        }
        var original = Document;
        var originalPreparing = _startupPreparing;
        var originalHtmlRendering = _documentationHtmlRendering;
        var originalWidth = Root.Width; var originalHeight = Root.Height;
        _clock.Stop(); _ready = true; _startupPreparing = _documentationHtmlRendering = true;
        Content = null; Root.Width = 1280; Root.Height = 800;
        using var host = HiddenRenderHost(Root, 1280, 800);
        InitializeAppearanceRefresh();
        try
        {
            var counter = _scriptPackages.Import(Services.ScriptTilePackages.BundledManifestPath("clock"));
            var orbit = _scriptPackages.Import(Services.ScriptTilePackages.BundledManifestPath("orbit"));
            counter.AllowWebNetwork = true; counter.Size = TileSize.Wide;
            File.WriteAllText(Path.Combine(_scriptPackages.Folder(counter.ScriptId), "tile.js"),
                "async function render(ctx){if(ctx.event?.type==='click'){globalThis.recoveryWaiting=true;await new Promise(r=>setTimeout(r,750));}const count=(ctx.state.count||0)+1;globalThis.recoveryCount=count;return {text:'Count '+count,state:{count}}}");
            Document = new()
            {
                Preferences = new() { Language = "en", Animate = false, ShowWallpaper = false },
                Groups = [new() { Name = "Recovery fixture", Tiles = [counter, orbit] }]
            };
            ApplyAppearance(); RenderGroups(); Root.UpdateLayout();
            await WaitForDocumentationFramesAsync();
            Check(ScriptsReady && _htmlTiles.Count == 2, "Recovery fixture did not render both browsers.");
            var layout = JsonSerializer.Serialize(Document, LayoutStore.JsonOptions);
            var savedLayout = File.ReadAllText(_store.FilePath);

            for (var cycle = 0; cycle < 3; cycle++)
            {
                var previousViews = _htmlTiles.ToDictionary(pair => pair.Key, pair => pair.Value);
                var previousSession = _scriptSessions[counter.Id];
                var previousState = previousSession.State.GetRawText();
                previousSession.Inputs.Enqueue(new("click", cycle + 1)); previousSession.NextUpdate = DateTimeOffset.MinValue;
                var rendering = UpdateScriptTilesAsync();
                await UntilAsync(() => previousSession.Rendering, "The browser never entered an in-flight update.");
                Check(previousSession.Inputs.Count == 1, "An unfinished browser input was consumed prematurely.");
                var before = _boardRenderCount;
                OnDisplaySettingsChanged(null, EventArgs.Empty);
                OnDisplaySettingsChanged(null, EventArgs.Empty);
                OnDpiChanged(new(1, 1), new(1.25, 1.25));
                await UntilAsync(() => _boardRenderCount > before, "A display-change notification did not refresh the board.");
                Check(_boardRenderCount == before + 1, "A display/DPI notification burst rebuilt the board repeatedly.");
                foreach (var pair in previousViews)
                {
                    Check(!ReferenceEquals(pair.Value, _htmlTiles[pair.Key]), "A display refresh retained an invalidated browser presentation.");
                    Check(!pair.Value.BrowserReady, "A replaced browser remained alive after a display refresh.");
                }
                var resumed = _scriptSessions[counter.Id];
                Check(!ReferenceEquals(previousSession, resumed) && !resumed.Rendering, "A new browser inherited an in-flight rendering lock.");
                Check(resumed.State.GetRawText() == previousState, "Display recovery discarded tile JSON state.");
                Check(resumed.Inputs.Count == 1 && resumed.Inputs.Peek().UnixMs == cycle + 1, "Display recovery discarded or duplicated queued input.");
                Check(!_scriptFrames.ContainsKey(counter.Id) && !_scriptFrames.ContainsKey(orbit.Id), "Display recovery retained stale success frames.");
                await rendering.WaitAsync(TimeSpan.FromSeconds(5));
                Check(ReferenceEquals(resumed, _scriptSessions[counter.Id]) && resumed.State.GetRawText() == previousState,
                    "A reply from a retired browser overwrote the replacement session.");
                Root.UpdateLayout(); await WaitForDocumentationFramesAsync();
                Check(ScriptsReady && ScriptFrameCount == 2, "Live tiles required a manual Reload after display recovery.");
                Check(resumed.Inputs.Count == 0 && resumed.State.GetProperty("count").GetInt32() > previousSession.State.GetProperty("count").GetInt32(),
                    "The replacement browser did not resume state and complete its queued click.");
                Check(await _htmlTiles[counter.Id].InspectAsync("String(globalThis.recoveryCount)") == JsonSerializer.Serialize(resumed.State.GetProperty("count").GetInt32().ToString()),
                    "The replacement document did not receive live updates.");
                Check(JsonSerializer.Serialize(Document, LayoutStore.JsonOptions) == layout && File.ReadAllText(_store.FilePath) == savedLayout,
                    "Display recovery changed the layout, preferences, network permission or saved configuration.");
            }

            var retained = _htmlTiles[counter.Id]; var board = _boardRenderCount;
            OnDesktopAppearanceChanged(this, new(Microsoft.Win32.UserPreferenceCategory.Color));
            await UntilAsync(() => _boardRenderCount > board, "A color change did not refresh appearance.");
            Check(ReferenceEquals(retained, _htmlTiles[counter.Id]), "An ordinary color change unnecessarily recreated a browser.");
            board = _boardRenderCount; _dragging = true;
            OnDisplaySettingsChanged(null, EventArgs.Empty);
            await Task.Delay(450);
            Check(_boardRenderCount == board && ReferenceEquals(retained, _htmlTiles[counter.Id]), "Display recovery interrupted an active drag.");
            _dragging = false;
            await UntilAsync(() => _boardRenderCount > board, "Deferred display recovery did not run after dragging ended.");
            Check(!ReferenceEquals(retained, _htmlTiles[counter.Id]), "Deferred display recovery retained the old presentation.");
            Root.UpdateLayout(); await WaitForDocumentationFramesAsync();
            Check(ScriptsReady && JsonSerializer.Serialize(Document, LayoutStore.JsonOptions) == layout, "Deferred recovery failed to resume the unchanged layout.");
            retained = _htmlTiles[counter.Id]; board = _boardRenderCount;
            var header = GroupsPanel.Children.OfType<StackPanel>().SelectMany(stack => stack.Children.OfType<GroupHeader>()).Single();
            header.BeginEdit();
            Check(header.IsEditing && _editingGroupHeader == header, "The recovery fixture did not enter inline naming.");
            OnDisplaySettingsChanged(null, EventArgs.Empty);
            await Task.Delay(450);
            Check(header.IsEditing && _boardRenderCount == board && ReferenceEquals(retained, _htmlTiles[counter.Id]), "Display recovery interrupted inline naming.");
            header.EndEdit(false);
            await UntilAsync(() => _boardRenderCount > board, "Display recovery did not resume after inline naming ended.");
            Check(!ReferenceEquals(retained, _htmlTiles[counter.Id]), "Inline naming left an invalidated browser presentation behind.");
            Root.UpdateLayout(); await WaitForDocumentationFramesAsync();
            Check(ScriptsReady && JsonSerializer.Serialize(Document, LayoutStore.JsonOptions) == layout, "Inline naming recovery changed state or layout.");
            Check(!IsVisible, "Web recovery verification showed a window.");
            File.WriteAllText(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(new { success = true, checks, displayCycles = 3, browserTiles = 2, visibleWindow = IsVisible }));
        }
        finally
        {
            _dragging = false; _refreshWebPresentations = false; _appearanceRefresh?.Stop(); _appearanceRefresh = null;
            RetireHtmlTiles(all: true);
            _startupPreparing = originalPreparing; _documentationHtmlRendering = originalHtmlRendering;
            host.RootVisual = null; Content = Root; Root.Width = originalWidth; Root.Height = originalHeight;
            Document = original; ApplyAppearance(); RenderGroups();
        }
        return checks;
    }
}
