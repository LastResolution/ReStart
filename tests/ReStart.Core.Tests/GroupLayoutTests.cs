using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class GroupLayoutTests
{
    private static TileGroup Group(int? column = null, int? row = null) => new() { Column = column, Row = row, Tiles = [new()] };

    [Fact]
    public void ExplicitGroupsKeepIndependentRowsAndEmptySpace()
    {
        var a = Group(0, 0); var b = Group(1, 18); var c = Group(2, 4);
        var placements = GroupLayout.Pack([a, b, c], 4, 6, 52);
        Assert.Equal(new[] { (0, 0), (1, 18), (2, 4) }, placements.Select(p => (p.Column, p.Row)));
    }

    [Fact]
    public void MovingAGroupPreservesOrderAndUnaffectedAnchors()
    {
        var a = Group(0, 0); var b = Group(1, 16); var c = Group(2, 0);
        var doc = new LayoutDocument { Groups = [a, b, c] };
        GroupLayout.Move(doc, c, 3, 22, 4, 52);
        Assert.Equal(new[] { a, b, c }, doc.Groups);
        Assert.Equal((3, 22), (c.Column, c.Row));
        Assert.Equal((0, 0), (a.Column, a.Row));
        Assert.Equal((1, 16), (b.Column, b.Row));
    }

    [Fact]
    public void TheDraggedGroupTakesItsTargetAndOnlyCollisionsMoveDown()
    {
        var a = Group(0, 0); var b = Group(1, 0); var c = Group(2, 0);
        var doc = new LayoutDocument { Groups = [a, b, c] };
        GroupLayout.Move(doc, c, 1, 0, 3, 52);
        var placements = GroupLayout.Pack(doc.Groups, 3, 6, 52);
        Assert.Equal((1, 0), (c.Column, c.Row));
        Assert.Equal((0, 0), (a.Column, a.Row));
        Assert.True(b.Row >= placements.Single(p => p.Group == c).Rows);
        AssertNoOverlaps(placements);
    }

    [Fact]
    public void NarrowScreensReflowWithoutRewritingWideScreenAnchors()
    {
        var a = Group(0, 0); var b = Group(3, 0); var c = Group(3, 20);
        var narrow = GroupLayout.Pack([a, b, c], 1, 6, 52);
        AssertNoOverlaps(narrow);
        Assert.All(narrow, p => Assert.Equal(0, p.Column));
        Assert.Equal((3, 0), (b.Column, b.Row));
        var wide = GroupLayout.Pack([a, b, c], 4, 6, 52);
        Assert.Equal((3, 0), (wide[1].Column, wide[1].Row));
        Assert.Equal((3, 20), (wide[2].Column, wide[2].Row));
    }

    [Fact]
    public void LegacyGroupsUseIndependentColumnsRatherThanSharedWrapRows()
    {
        var tall = Group(); tall.Tiles[0].Row = 12; tall.Tiles[0].Column = 0;
        var shortGroup = Group(); var next = Group();
        var placements = GroupLayout.Pack([tall, shortGroup, next], 2, 6, 52);
        Assert.Equal((1, 0), (placements[1].Column, placements[1].Row));
        Assert.Equal(1, placements[2].Column);
        Assert.True(placements[2].Row < placements[0].Rows);
        AssertNoOverlaps(placements);
    }

    [Fact]
    public void SchemaOneMigratesAndSchemaTwoRoundTripsPositionsAndUnnamedGroups()
    {
        var legacy = JsonSerializer.Deserialize<LayoutDocument>("{\"Version\":1,\"Groups\":[{\"Name\":\"work\"}]}", LayoutStore.JsonOptions)!;
        legacy.Normalize();
        Assert.Equal(LayoutDocument.CurrentVersion, legacy.Version);
        Assert.Null(legacy.Groups[0].Column);
        var group = Group(3, 24); group.Name = "";
        var doc = new LayoutDocument { Groups = [group] };
        var restored = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(doc, LayoutStore.JsonOptions), LayoutStore.JsonOptions)!;
        restored.Normalize();
        Assert.Equal((3, 24), (restored.Groups[0].Column, restored.Groups[0].Row));
        Assert.Equal("", restored.Groups[0].Name);
    }

    [Fact]
    public void InvalidAnchorsAreRepairedTogether()
    {
        var a = Group(1, null); var b = Group(-1, 2); var c = Group(3, 20001);
        new LayoutDocument { Groups = [a, b, c] }.Normalize();
        Assert.All(new[] { a, b, c }, g => { Assert.Null(g.Column); Assert.Null(g.Row); });
    }

    [Fact]
    public void SingleSmallTileDoesNotReserveAMediumTileHeight()
    {
        var small = Group(); small.Tiles[0].Size = TileSize.Small;
        var medium = Group();
        var placements = GroupLayout.Pack([small, medium], 2, 6, 52);
        Assert.Equal(GroupLayout.HeaderHeight + 52, placements[0].Height);
        Assert.True(placements[0].Rows < placements[1].Rows);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void PreviewMatchesCommitWithoutChangingSingleOrMultipleTileContents(int count)
    {
        var source = Group(0, 0);
        source.Tiles = Enumerable.Range(0, count).Select(i => new Tile { Size = TileSize.Small, Column = 4, Row = i * 2 }).ToList();
        var target = Group(1, 0);
        var document = new LayoutDocument { Groups = [source, target] };
        var before = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);
        var contents = JsonSerializer.Serialize(source.Tiles, LayoutStore.JsonOptions);
        var preview = GroupLayout.PreviewMove(document, source, 1, 0, 3, 52);
        Assert.Equal(before, JsonSerializer.Serialize(document, LayoutStore.JsonOptions));
        Assert.True(preview.Single(p => p.Group == target).Row > 0);
        GroupLayout.Move(document, source, 1, 0, 3, 52);
        Assert.Equal(preview, GroupLayout.Pack(document.Groups, 3, 6, 52));
        Assert.Equal(contents, JsonSerializer.Serialize(source.Tiles, LayoutStore.JsonOptions));
    }

    [Fact]
    public void TrailingSpacingIsNotAnInvisibleCollisionAreaForExplicitGroups()
    {
        var first = Group(0, 0); first.Tiles[0].Size = TileSize.Small;
        var second = Group(0, 4); second.Tiles[0].Size = TileSize.Small;
        var placements = GroupLayout.Pack([first, second], 2, 6, 52);
        Assert.Equal(4, placements[1].Row);
        Assert.Equal(GroupLayout.HeaderHeight + 52, placements[0].Height);
        Assert.True(placements[0].Height < second.Row * 26);
    }

    private static void AssertNoOverlaps(IReadOnlyList<GroupPlacement> placements)
    {
        for (var i = 0; i < placements.Count; i++)
            for (var j = i + 1; j < placements.Count; j++)
                if (placements[i].Column == placements[j].Column)
                    Assert.True(placements[i].Row + placements[i].Rows <= placements[j].Row || placements[j].Row + placements[j].Rows <= placements[i].Row);
    }
}
