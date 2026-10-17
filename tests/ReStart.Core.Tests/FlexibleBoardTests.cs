using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class FlexibleBoardTests
{
    [Fact]
    public void GroupsMeasureTheirContentInsteadOfAConfiguredMinimumWidth()
    {
        var narrow = new TileGroup { Column = 0, Row = 0, Tiles = [new() { Size = TileSize.Small }] };
        var wide = new TileGroup { Column = 1, Row = 0, Tiles = [new() { Size = TileSize.Wide }] };
        var placements = GroupLayout.Pack([narrow, wide], 3, 8, 52);
        Assert.Equal(52, placements[0].Width);
        Assert.Equal(208, placements[1].Width);
    }

    [Fact]
    public void AdjacentFlexibleGroupsDoNotDisplaceEachOtherInsideTheOldColumn()
    {
        var a = new TileGroup { X = 0, Column = 0, Row = 0, Tiles = [new() { Size = TileSize.Small }] };
        var b = new TileGroup { X = 1.5, Column = 0, Row = 0, Tiles = [new() { Size = TileSize.Small }] };
        var document = new LayoutDocument { Groups = [a, b] };
        var preview = GroupLayout.PreviewMove(document, b, 0, 0, 1, 52, 1);
        Assert.All(preview, p => Assert.Equal(0, p.Row));
        GroupLayout.Move(document, b, 0, 0, 1, 52, 1);
        Assert.Equal(preview, GroupLayout.Pack(document.Groups, 1, 6, 52));
        Assert.Equal(52, preview[1].Left);
    }

    [Fact]
    public void HorizontalAnchorsSurviveNarrowViewsAndRoundTrips()
    {
        var group = new TileGroup { X = 42.5, Column = 0, Row = 3, Tiles = [new() { Column = 2, Row = 8 }] };
        var document = new LayoutDocument { Groups = [group] };
        var saved = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(document, LayoutStore.JsonOptions), LayoutStore.JsonOptions)!;
        saved.Normalize();
        var placement = Assert.Single(GroupLayout.Pack(saved.Groups, 1, 6, 52, preserveColumns: true));
        Assert.Equal(42.5 * 52, placement.Left);
        Assert.Equal((2, 8), (saved.Groups[0].Tiles[0].Column, saved.Groups[0].Tiles[0].Row));
        Assert.Equal(3, placement.Row);
    }
}
