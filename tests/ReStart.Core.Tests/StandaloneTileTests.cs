using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class StandaloneTileTests
{
    [Fact]
    public void AdjacentCellsAcceptATileWithoutChangingTheGroupsCollisionFootprint()
    {
        var group = new TileGroup { Column = 0, Row = 0, Tiles = [new()] };
        var loose = new TileGroup { IsStandalone = true, Column = 2, Row = 0, Tiles = [new()] };
        var placements = GroupLayout.Pack([group, loose], 3, 6, 52);
        Assert.Same(group, GroupLayout.DropRecipient(placements, 156, 92, 52));
        Assert.Same(group, GroupLayout.DropRecipient(placements, 52, 196, 52));
        Assert.Null(GroupLayout.DropRecipient(placements, 158, 92, 52));
        Assert.Null(GroupLayout.DropRecipient(placements, 700, 92, 52));
        Assert.Equal((104d, 144d), (placements[0].Width, placements[0].Height));
        Assert.Single(group.Tiles);
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void IndependentTilesShareTheNamedTileBaselineAtEveryScale(double scale)
    {
        var cell = 52 * scale;
        var grouped = new TileGroup { Column = 0, Row = 3, Tiles = [new() { Column = 0, Row = 6 }] };
        var standalone = new TileGroup { IsStandalone = true, Column = 1, Row = 15, Tiles = [new()] };
        var placements = GroupLayout.Pack([grouped, standalone], 3, 6, cell);
        Assert.Equal(placements[0].Top + GroupLayout.HeaderHeight + 6 * cell, placements[1].Top, 6);
        Assert.Equal(2 * cell, placements[1].Height);
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void SchemaThreeAlignsOnceAndPreservesTheClosestExistingPosition(double scale)
    {
        var standalone = new TileGroup { IsStandalone = true, Column = 1, Row = 13, Tiles = [new()] };
        var document = new LayoutDocument { Version = 3, Preferences = new() { TileScale = scale }, Groups = [standalone] };
        var previousTop = 13 * 52 * scale / 2;
        document.Normalize();
        var newTop = Assert.Single(GroupLayout.Pack(document.Groups, 3, 6, 52 * scale)).Top;
        Assert.InRange(Math.Abs(newTop - previousTop), 0, 52 * scale / 4 + 0.001);
        var row = standalone.Row;
        document.Normalize();
        Assert.Equal(row, standalone.Row);
        var restored = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(document, LayoutStore.JsonOptions), LayoutStore.JsonOptions)!;
        restored.Normalize();
        Assert.Equal(row, restored.Groups[0].Row);
    }

    [Fact]
    public void NearbyGroupsSnapToAVisibleGapWithoutReservingCollisionPadding()
    {
        var first = new TileGroup { Column = 0, Row = 0, Tiles = [new()] };
        var moving = new TileGroup { Column = 1, Row = 0, Tiles = [new()] };
        var placements = GroupLayout.Pack([first, moving], 3, 6, 52);
        Assert.Equal(104 + GroupLayout.Gap, GroupLayout.SnapBeside(placements, moving, 104, 0, 104, 144, 52));
        Assert.Equal(104, GroupLayout.SnapBeside(placements, moving, 104, 1000, 104, 144, 52));
        Assert.Equal(300, GroupLayout.SnapBeside(placements, moving, 300, 0, 104, 144, 52));
        Assert.Equal((0, 0), (first.Column, first.Row));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void GroupAndIndependentNeighborsShareTheSameGap(bool firstStandalone, bool movingStandalone)
    {
        var first = new TileGroup { Column = 0, Row = 0, IsStandalone = firstStandalone, Tiles = [new()] };
        var moving = new TileGroup { Column = 1, Row = 0, IsStandalone = movingStandalone, Tiles = [new()] };
        var placements = GroupLayout.Pack([first, moving], 3, 6, 52);
        var expected = 104 + (firstStandalone && movingStandalone ? 0 : GroupLayout.Gap);
        Assert.Equal(expected, GroupLayout.SnapBeside(placements, moving, 104, 40, 104, 144, 52));
        Assert.Equal(104, GroupLayout.SnapBeside(placements, moving, 104, 1000, 104, 144, 52));
        var lower = GroupLayout.SnapRow(placements, moving, 0, 6, 104, movingStandalone ? 104 : 144, 52);
        if (firstStandalone && movingStandalone) Assert.Equal(6, lower);
        else Assert.True((movingStandalone ? 40 : 0) + lower * 26 >= placements[0].Top + placements[0].Height + GroupLayout.Gap);
    }

    [Fact]
    public void DetachedTileHasOnlyItsOwnFootprintAndPreservesOtherItems()
    {
        var tile = new Tile { Size = TileSize.Small, Column = 2, Row = 4 };
        var other = new Tile { Column = 4, Row = 8 };
        var source = new TileGroup { Column = 0, Row = 0, Tiles = [tile, other] };
        var document = new LayoutDocument { Groups = [source] };
        var entry = LayoutEditor.Detach(document, tile, 1, 0, 3, 3, 52);
        var bounds = GroupLayout.Pack(document.Groups, 3, 6, 52).Single(p => p.Group == entry);
        Assert.True(entry.IsStandalone);
        Assert.Same(tile, Assert.Single(entry.Tiles));
        Assert.Equal((52d, 52d), (bounds.Width, bounds.Height));
        Assert.Equal(336 + 3 * 52, bounds.Left);
        Assert.Same(other, Assert.Single(source.Tiles));
        Assert.Equal((4, 8), (other.Column, other.Row));
        Assert.Equal((3, 0), (tile.Column, tile.Row));
    }

    [Fact]
    public void IndependentNeighborsDoNotReserveAnInvisibleGroupWidthOrFooter()
    {
        var document = new LayoutDocument();
        var a = LayoutEditor.Detach(document, new() { Size = TileSize.Small }, 0, 0, 0, 3, 52);
        var b = LayoutEditor.Detach(document, new() { Size = TileSize.Small }, 0, 0, 1, 3, 52);
        var c = LayoutEditor.Detach(document, new() { Size = TileSize.Small }, 0, 2, 0, 3, 52);
        Assert.Equal(new int?[] { 0, 0, 2 }, new[] { a.Row, b.Row, c.Row });
        var incoming = LayoutEditor.Detach(document, new() { Size = TileSize.Small }, 0, 0, 1, 3, 52);
        Assert.Equal(0, a.Row);
        Assert.Equal(2, b.Row);
        Assert.Equal(2, c.Row);
        Assert.Equal(0, incoming.Row);
    }

    [Fact]
    public void RepeatedDetachesAndFolderExtractionNeverDuplicateOrLoseIdentity()
    {
        var tile = new Tile();
        var sibling = new Tile();
        var folder = new Tile { Children = [tile, sibling] };
        var document = new LayoutDocument { Groups = [new() { Tiles = [folder] }] };
        LayoutEditor.Detach(document, tile, 1, 0, 4, 3, 52);
        for (var i = 0; i < 8; i++) LayoutEditor.Detach(document, tile, i % 3, i * 4, i % 5, 3, 52);
        Assert.Same(tile, LayoutEditor.Find(document, tile.Id));
        Assert.Single(document.Groups, g => g.IsStandalone);
        Assert.Same(sibling, Assert.Single(folder.Children));
        var json = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);
        var restored = JsonSerializer.Deserialize<LayoutDocument>(json, LayoutStore.JsonOptions)!;
        restored.Normalize();
        Assert.True(restored.Groups.Single(g => g.Tiles.Any(t => t.Id == tile.Id)).IsStandalone);
        LayoutEditor.Remove(restored, tile.Id);
        Assert.DoesNotContain(restored.Groups, g => g.IsStandalone);
    }

    [Fact]
    public void LegacySingleUnnamedGroupsMigrateOnceWithoutLosingNamedGroups()
    {
        var loose = new TileGroup { Name = "", Column = 1, Row = 4, Tiles = [new() { Column = 3, Row = 2 }] };
        var named = new TileGroup { Name = "Work", Tiles = [new()] };
        var document = new LayoutDocument { Version = 2, Groups = [loose, named] };
        document.Normalize();
        Assert.True(loose.IsStandalone);
        Assert.Equal((3, 0), (loose.Tiles[0].Column, loose.Tiles[0].Row));
        Assert.Equal(8, loose.Row);
        Assert.False(named.IsStandalone);
        document.Normalize();
        Assert.Equal(8, loose.Row);
    }

    [Fact]
    public void PinningNeverAddsAnotherAppToAnIndependentTile()
    {
        var document = new LayoutDocument();
        AppPinning.Pin(document, new("a", "A", "a"));
        AppPinning.Pin(document, new("b", "B", "b"));
        Assert.Equal(2, document.Groups.Count);
        Assert.All(document.Groups, g => { Assert.True(g.IsStandalone); Assert.Single(g.Tiles); });
        Assert.Throws<ArgumentException>(() => AppPinning.Pin(document, new("c", "C", "c"), document.Groups[0]));
    }

    [Fact]
    public void UnpackingAnIndependentFolderKeepsItsChildrenIndependent()
    {
        var a = new Tile(); var b = new Tile();
        var folder = new Tile { Children = [a, b] };
        var document = new LayoutDocument();
        LayoutEditor.Detach(document, folder, 2, 12, 0, 3, 52);
        LayoutEditor.UnpackFolder(document, folder);
        Assert.Equal(2, document.Groups.Count);
        Assert.All(document.Groups, g => { Assert.True(g.IsStandalone); Assert.Single(g.Tiles); Assert.Equal((2, 12), (g.Column, g.Row)); });
        Assert.Equal(new[] { a.Id, b.Id }, document.Groups.SelectMany(g => g.Tiles).Select(t => t.Id));
        var placements = GroupLayout.Pack(document.Groups, 3, 6, 52);
        Assert.Equal(placements[0].Row, placements[1].Row);
        Assert.True(placements[0].Left + placements[0].Width <= placements[1].Left);
    }

    [Theory]
    [InlineData(6, 0.7)]
    [InlineData(8, 1.5)]
    public void RepeatedGroupCollisionsPreviewsAndPersistenceNeverShiftAnInternalItem(int columns, double scale)
    {
        var moving = new TileGroup { Column = 2, Row = 0, Tiles = [new() { Size = TileSize.Small, Column = 1, Row = 3 }, new() { Column = 4, Row = 8 }, new()] };
        var document = new LayoutDocument { Preferences = new() { Columns = columns, TileScale = scale }, Groups = [new() { Column = 0, Row = 0, Tiles = [new()] }, moving] };
        var contents = JsonSerializer.Serialize(moving.Tiles, LayoutStore.JsonOptions);
        var initial = TileLayout.Pack(moving.Tiles, columns).Select(p => (p.Tile.Id, p.Column, p.Row)).ToArray();
        for (var i = 0; i < 40; i++)
        {
            var before = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);
            GroupLayout.PreviewMove(document, moving, i % 3, i % 7, 3, 52 * scale);
            Assert.Equal(before, JsonSerializer.Serialize(document, LayoutStore.JsonOptions));
            GroupLayout.Move(document, moving, i % 3, i % 7, 3, 52 * scale);
            document.Normalize();
            Assert.Equal(contents, JsonSerializer.Serialize(moving.Tiles, LayoutStore.JsonOptions));
            Assert.Equal(initial, TileLayout.Pack(moving.Tiles, columns).Select(p => (p.Tile.Id, p.Column, p.Row)));
            var restored = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(document, LayoutStore.JsonOptions), LayoutStore.JsonOptions)!;
            restored.Normalize();
            Assert.Equal(initial, TileLayout.Pack(restored.Groups.Single(g => g.Id == moving.Id).Tiles, columns).Select(p => (p.Tile.Id, p.Column, p.Row)));
        }
    }
}
