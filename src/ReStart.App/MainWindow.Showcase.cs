using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Captures the complete launcher with a fixed, nonpersonal app list and demonstration live data.</summary>
    private async Task SaveLauncherShowcaseAsync(Tile[] liveTiles, MainWindow iconSource, string directory)
    {
        if (!_preview) throw new InvalidOperationException("Showcase rendering requires preview mode.");
        _verifyHostMetrics = false;
        Root.Width = 1920; Root.Height = 1080;
        Document = new() { Preferences = new() { Language = "en", Columns = 8, TileScale = 1.1, Theme = ReStart.Core.ThemeMode.Dark, ShowWallpaper = false, Animate = false } };
        ApplyAppearance();
        _apps = PreviewApps();
        foreach (var app in _apps)
            if (iconSource._catalog.Icons.TryGetValue(app.Id, out var icon)) _catalog.Icons[app.Id] = icon;
        Tile App(string id, int column, int row)
        {
            var tile = Tile.FromApp(_apps.Single(app => app.Id == id));
            tile.Column = column; tile.Row = row;
            return tile;
        }
        Document.Groups.Add(new()
        {
            Name = "Everyday",
            Column = 0,
            X = 0,
            Row = 0,
            Tiles = [App("edge", 0, 0), App("mail", 2, 0), App("photos", 4, 0),
                new() { AppId = "restart:calendar", Label = "Calendar", Widget = "calendar", Size = TileSize.Large, Color = "#0078D7", Column = 0, Row = 2 },
                new() { AppId = "restart:clock", Label = "Clock", Widget = "clock", Color = "#325B8C", Column = 4, Row = 2 }, App("store", 4, 4)]
        });
        Document.Groups.Add(new()
        {
            Name = "Create & work",
            Column = 0,
            X = 6.5,
            Row = 0,
            Tiles = [App("notepad", 0, 0), App("calc", 2, 0), App("paint", 4, 0), App("terminal", 0, 2), App("code", 2, 2),
                new() { Label = "Quick tools", Column = 4, Row = 2, Children = [App("notepad", 0, 0), App("calc", 0, 0), App("paint", 0, 0)] }]
        });
        var fixtureDirectory = Path.Combine(directory, "showcase-files"); Directory.CreateDirectory(fixtureDirectory);
        var projects = Path.Combine(fixtureDirectory, "Projects"); Directory.CreateDirectory(projects);
        var notes = Path.Combine(fixtureDirectory, "Notes.txt"); File.WriteAllText(notes, "Demonstration document.");
        var files = new[] { FilePinning.FromPath(projects), FilePinning.FromPath(notes) };
        await _catalog.LoadIconsAsync(files);
        Document.Groups.Add(new()
        {
            Name = "Files & folders",
            Column = 0,
            X = 6.5,
            Row = 11,
            Tiles = files.Select((entry, i) =>
        { var tile = Tile.FromApp(entry); tile.Column = i * 2; tile.Row = 0; return tile; }).ToList()
        });
        for (var group = 0; group < 2; group++)
        {
            var contents = liveTiles.Skip(group * 6).Take(6).ToList();
            for (var i = 0; i < contents.Count; i++)
            {
                contents[i].Size = TileSize.Wide; contents[i].Column = i % 2 * 4; contents[i].Row = i / 2 * 2;
                contents[i].UseCustomColor = false; contents[i].UseLocalizedName = true;
            }
            Document.Groups.Add(new() { Name = group == 0 ? "At a glance" : "Make time", Column = 0, X = 13 + group * 8.5, Row = 0, Tiles = contents });
        }
        foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
        {
            Document.Preferences.Theme = theme;
            ApplyAppearance(); RenderGroups(); RenderApps(); UpdateLayout();
            for (var attempt = 0; attempt < 50; attempt++)
            {
                foreach (var runtime in _scriptSessions.Values) runtime.NextUpdate = DateTimeOffset.MinValue;
                await UpdateScriptTilesAsync(); UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                UpdateLayout();
                if (!_scriptRendering && ScriptFrameCount == liveTiles.Length && _scriptFrames[liveTiles[^1].Id].Image == "current" &&
                    _scriptViews.All(view => view.Body.Children.OfType<TextBlock>().Any(text => Equals(text.Tag, "ScriptValue") && text.Text.Length > 0 && text.ActualHeight > 0))) break;
                await Task.Delay(100);
            }
            Require(ScriptFrameCount == 12, "The complete launcher showcase is missing live tiles.");
            Require(_scriptViews.All(view => view.Body.Children.OfType<TextBlock>().Any(text => Equals(text.Tag, "ScriptValue") && text.Text.Length > 0 && text.ActualHeight > 0)),
                "A complete launcher showcase tile has no rendered value.");
            SetStatus("");
            SaveScreenshot(Path.Combine(directory, $"launcher-{theme.ToString().ToLowerInvariant()}.png"));
        }
    }
}
