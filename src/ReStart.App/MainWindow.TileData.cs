using System.Windows;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    internal void ConfigureTileData(Tile tile, Window owner)
    {
        WithDialog(() =>
        {
            try
            {
                var manifest = _scriptPackages.Read(tile.ScriptId).Manifest;
                if (!manifest.Capabilities.Contains("data.sources")) return;
                var window = new TileDataWindow(owner, manifest, _scriptPackages.DataBindingPath(tile.ScriptId));
                if (window.ShowDialog() != true) return;
                RetireTileData(tile.Id);
                if (_scriptSessions.TryGetValue(tile.Id, out var session)) session.NextUpdate = DateTimeOffset.MinValue;
                SetStatus(L.T(K.TileDataSaved));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
            { SetStatus(L.T(K.TileDataInvalid)); }
        });
    }
}
