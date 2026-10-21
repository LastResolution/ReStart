using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class IndependentPinTests
{
    [Theory]
    [InlineData(6, .7)]
    [InlineData(6, 1.0)]
    [InlineData(8, 1.5)]
    public void AddingTwelveWidgetsPreservesEveryExistingGroupAndTile(int columns, double scale)
    {
        var document = new LayoutDocument
        {
            Preferences = new() { Columns = columns, TileScale = scale },
            Groups = [
            new() { Name = "Apps", Tiles = [new() { Column = 0, Row = 0 }, new() { Column = 2, Row = 0 }] },
            new() { Name = "Tools", Column = 0, Row = 30, Tiles = [new() { Column = 0, Row = 0 }] }]
        };
        var cell = 52 * scale;
        var before = GroupLayout.Pack(document.Groups, 2, columns, cell, preserveColumns: true).ToArray();
        var cells = document.Groups.SelectMany(g => g.Tiles).Select(t => (t, t.Column, t.Row)).ToArray();
        for (var i = 0; i < 12; i++)
        {
            var tile = new Tile { Widget = "script", Size = (TileSize)(i % 4) };
            var entry = LayoutEditor.PinIndependent(document, tile, 2, cell, columns * cell * 2 + GroupLayout.Gap, 200);
            Assert.True(entry.IsStandalone); Assert.Single(entry.Tiles);
        }
        var after = GroupLayout.Pack(document.Groups, 2, columns, cell, preserveColumns: true);
        foreach (var original in before)
        {
            var current = after.Single(p => p.Group == original.Group);
            Assert.Equal((original.Left, original.Top, original.Width, original.Height), (current.Left, current.Top, current.Width, current.Height));
        }
        foreach (var (tile, column, row) in cells) Assert.Equal((column, row), (tile.Column, tile.Row));
        foreach (var pin in after.Where(p => p.Group.IsStandalone))
            Assert.DoesNotContain(after, p => p.Group != pin.Group && pin.Left < p.Left + p.Width && pin.Left + pin.Width > p.Left && pin.Top < p.Top + p.Height && pin.Top + pin.Height > p.Top);
    }
}
