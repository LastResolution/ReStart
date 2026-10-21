using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    internal void OpenTileStudio(Tile? tile = null, Window? owner = null)
    {
        WithDialog(() => new TileStudioWindow(this, tile) { Owner = owner ?? this }.ShowDialog());
    }

    internal Button CreateStudioPreview(TileSize size, string title, ScriptTileFrame frame)
    {
        var tile = new Tile { Label = title, Widget = "script", Size = size, UseLocalizedName = false };
        var button = CreateTileButton(tile, studioPreview: true);
        var (width, height) = TileLayout.Dimensions(size); button.Width = width * Cell - 4; button.Height = height * Cell - 4;
        var body = ((Grid)button.Content).Children.OfType<Grid>().SelectMany(g => g.Children.OfType<StackPanel>()).Single();
        ApplyScriptFrame(body, button, tile, frame);
        return button;
    }

    internal bool CanPinStudioTile => CanEditLayout;

    internal void ReloadStudioTile(Tile tile)
    {
        if (_htmlTiles.Remove(tile.Id, out var view)) view.Dispose();
        RetireTileData(tile.Id); _scriptSessions.Remove(tile.Id); _scriptFrames.Remove(tile.Id);
        _scriptPackages.InvalidateTitle(tile.ScriptId); RenderGroups();
    }
}
