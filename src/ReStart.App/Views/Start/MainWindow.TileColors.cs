using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private MenuItem TileColorMenu(Tile tile)
    {
        var colors = new MenuItem { Header = L.T(K.TileBackgroundColor) };
        var reset = new MenuItem { Header = L.T(K.TileThemeColor), IsCheckable = true, IsChecked = !tile.UseCustomColor };
        reset.Click += (_, _) => SetTileColor(tile, null);
        colors.Items.Add(reset);
        colors.Items.Add(new Separator());
        foreach (var color in new[] { "#0078D7", "#0063B1", "#008272", "#107C10", "#744DA9", "#B146C2", "#D83B01", "#E81123" })
        {
            var item = new MenuItem
            {
                Header = color,
                IsCheckable = true,
                IsChecked = tile.UseCustomColor && string.Equals(tile.Color, color, StringComparison.OrdinalIgnoreCase),
                Icon = new Border { Width = 18, Height = 18, Background = Brush(color, "#0078D7") }
            };
            item.Click += (_, _) => SetTileColor(tile, color);
            colors.Items.Add(item);
        }
        Add(colors, L.T(K.TileCustomColor), () =>
        {
            var value = Prompt(L.T(K.PromptTileColor), tile.UseCustomColor ? tile.Color : Document.Preferences.Accent);
            if (value == null) return;
            if (!ValidTileColor(value)) { SetStatus(L.T(K.ErrorInvalidColor)); return; }
            SetTileColor(tile, value);
        });
        return colors;
    }

    private static bool ValidTileColor(string value) => value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    private void SetTileColor(Tile tile, string? color)
    {
        if (color != null && !ValidTileColor(color)) throw new ArgumentException("Invalid RGB color.", nameof(color));
        tile.UseCustomColor = color != null;
        tile.Color = color?.ToUpperInvariant() ?? "";
        CommitLayout();
    }
}
