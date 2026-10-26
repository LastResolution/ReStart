using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class GroupDragReorderTests
{
    [Fact]
    public void DownwardDragReordersAtHalfOverlapRatherThanChasingTheNeighborDown()
    {
        var moving = Group("Moving", 0);
        var target = Group("Target", 8);
        var below = Group("Below", 16);
        var document = new LayoutDocument { Groups = [moving, target, below] };
        var before = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);

        var preview = Drag(document, moving, 6);

        Assert.Equal(0, preview.Single(p => p.Group == target).Row);
        Assert.Equal(8, preview.Single(p => p.Group == moving).Row);
        Assert.Equal(16, preview.Single(p => p.Group == below).Row);
        Assert.Equal(before, JsonSerializer.Serialize(document, LayoutStore.JsonOptions));
    }

    [Fact]
    public void ShallowOverlapDoesNotStartPushingTheRecipientDown()
    {
        var moving = Group("Moving", 0);
        var target = Group("Target", 8);
        var document = new LayoutDocument { Groups = [moving, target] };

        var preview = Drag(document, moving, 5);

        Assert.Equal(8, preview.Single(p => p.Group == target).Row);
    }

    [Theory]
    [InlineData(6, 8)]
    [InlineData(8, 8)]
    [InlineData(14, 14)]
    public void RecipientStaysInTheVacatedSpaceAfterThePointerPassesIt(int row, int expectedMovingRow)
    {
        var moving = Group("Moving", 0);
        var target = Group("Target", 8);
        var document = new LayoutDocument { Groups = [moving, target] };

        var preview = Drag(document, moving, row);

        Assert.Equal(0, preview.Single(p => p.Group == target).Row);
        Assert.Equal(expectedMovingRow, preview.Single(p => p.Group == moving).Row);
    }

    public static IEnumerable<object[]> SizePairs()
    {
        foreach (var scale in new[] { .7, 1.0, 1.5 })
            foreach (var movingSize in Enum.GetValues<TileSize>())
                foreach (var targetSize in Enum.GetValues<TileSize>())
                    foreach (var downward in new[] { false, true })
                        yield return [scale, movingSize, targetSize, downward];
    }

    [Theory]
    [MemberData(nameof(SizePairs))]
    public void MixedSizesReorderBothWaysWithoutChangingContentOrExtent(double scale, TileSize movingSize, TileSize targetSize, bool downward)
    {
        var cell = 52 * scale;
        var half = cell / 2;
        var moving = Group("Moving", 0, movingSize);
        var target = Group("Target", 0, targetSize);
        var document = new LayoutDocument { Groups = downward ? [moving, target] : [target, moving] };
        var first = document.Groups[0];
        var firstHeight = GroupLayout.OccupiedAreas(first, document.Preferences.Columns, cell).Single().Height;
        document.Groups[1].Row = (int)Math.Ceiling((firstHeight + GroupLayout.Gap) / half);
        // A nested folder is retained as data, without flattening its children.
        target.Tiles[0].Children = [new() { Label = "Nested app", Size = TileSize.Small, Column = 1, Row = 1 }];
        var baseline = GroupLayout.Pack(document.Groups, 3, document.Preferences.Columns, cell, preserveColumns: true);
        var origin = baseline.Single(p => p.Group == moving);
        var recipient = baseline.Single(p => p.Group == target);
        var overlap = Math.Min(origin.Height, recipient.Height) / 2;
        var threshold = downward ? (recipient.Top + overlap - origin.Height) / half : (recipient.Top + recipient.Height - overlap) / half;
        var row = downward ? (int)Math.Ceiling(threshold - .000001) : (int)Math.Floor(threshold + .000001);
        var before = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);

        var shallow = Drag(document, moving, row + (downward ? -1 : 1), cell);
        Assert.Equal(recipient.Row, shallow.Single(p => p.Group == target).Row);
        var preview = Drag(document, moving, row, cell);
        var ordered = preview.OrderBy(p => p.Top).ToArray();
        Assert.Same(downward ? target : moving, ordered[0].Group);
        Assert.Same(downward ? moving : target, ordered[1].Group);
        Assert.Equal(0, ordered[0].Top, 6);
        Assert.Equal(baseline.Max(p => p.Top + p.Height), ordered.Max(p => p.Top + p.Height), 6);
        Assert.Equal(baseline[1].Top - baseline[0].Height, ordered[1].Top - ordered[0].Height, 6);
        Assert.Equal(before, JsonSerializer.Serialize(document, LayoutStore.JsonOptions));

        GroupLayout.Move(document, moving, 0, row, 3, cell, 0, reorderVertical: true);
        var committed = GroupLayout.Pack(document.Groups, 3, document.Preferences.Columns, cell, preserveColumns: true);
        Assert.Equal(preview, committed);
        Assert.Single(target.Tiles[0].Children);
        Assert.All(document.Groups.SelectMany(g => g.Tiles), t => { Assert.Equal(0, t.Column); Assert.Equal(0, t.Row); });
    }

    [Fact]
    public void LongDragUsesTheOriginalBaselineAndLeavesOtherGroupsAlone()
    {
        var moving = Group("Moving", 0);
        var middle = Group("Middle", 8);
        var target = Group("Target", 16);
        var below = Group("Below", 24);
        var side = Group("Side", 0, TileSize.Large); side.X = 8;
        var document = new LayoutDocument { Groups = [moving, middle, target, below, side] };
        var before = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);

        foreach (var row in new[] { 5, 6, 10, 13, 14, 16, 14, 6, 5, 0 })
        {
            var preview = Drag(document, moving, row);
            Assert.Equal(preview, Drag(document, moving, row));
            Assert.Equal(24, preview.Single(p => p.Group == below).Row);
            Assert.Equal(0, preview.Single(p => p.Group == side).Row);
            if (row >= 14)
            {
                Assert.Equal(0, preview.Single(p => p.Group == middle).Row);
                Assert.Equal(8, preview.Single(p => p.Group == target).Row);
                Assert.Equal(16, preview.Single(p => p.Group == moving).Row);
            }
            if (row <= 5) Assert.Equal(8, preview.Single(p => p.Group == middle).Row);
            Assert.Equal(before, JsonSerializer.Serialize(document, LayoutStore.JsonOptions));
        }
    }

    [Fact]
    public void ATooWideRecipientCannotEvictAnUnrelatedLateralGroup()
    {
        var moving = Group("Moving", 0);
        var target = Group("Wide", 8, TileSize.Wide);
        var side = Group("Side", 0); side.X = 2.5;
        var document = new LayoutDocument { Groups = [moving, target, side] };

        var preview = Drag(document, moving, 6);

        Assert.Equal(0, preview.Single(p => p.Group == side).Row);
        Assert.True(preview.Single(p => p.Group == target).Row > 6);
        AssertDisjoint(preview);
    }

    [Fact]
    public void HorizontallyOffsetGroupsCanStillReorderWithinTheirVerticalStack()
    {
        var moving = Group("Moving", 0);
        var target = Group("Target", 8); target.X = .5;
        var document = new LayoutDocument { Groups = [moving, target] };

        var preview = Drag(document, moving, 6);

        Assert.Equal(0, preview.Single(p => p.Group == target).Row);
        Assert.Equal(26, preview.Single(p => p.Group == target).Left);
        Assert.Equal(8, preview.Single(p => p.Group == moving).Row);
        AssertDisjoint(preview);
    }

    [Fact]
    public void AGroupCoveringParallelStacksDoesNotCollapseThemIntoOneSequence()
    {
        var moving = Group("Moving", 0, TileSize.Wide);
        var left = Group("Left", 8);
        var right = Group("Right", 8); right.X = 2;
        var document = new LayoutDocument { Groups = [moving, left, right] };

        var preview = Drag(document, moving, 6);

        Assert.Equal(6, preview.Single(p => p.Group == moving).Row);
        Assert.Equal(preview.Single(p => p.Group == left).Row, preview.Single(p => p.Group == right).Row);
        AssertDisjoint(preview);
    }

    [Fact]
    public void CrossColumnAndDiscreteEditsKeepTheirRequestedAnchor()
    {
        var moving = Group("Moving", 0);
        var target = Group("Target", 8);
        var document = new LayoutDocument { Groups = [moving, target] };

        var crossColumn = GroupLayout.PreviewMove(document, moving, 1, 6, 3, 52, 8, reorderVertical: true);
        Assert.Equal(6, crossColumn.Single(p => p.Group == moving).Row);
        Assert.Equal(8, crossColumn.Single(p => p.Group == target).Row);
        var discrete = GroupLayout.PreviewMove(document, moving, 0, 1, 3, 52, 0);
        Assert.Equal(1, discrete.Single(p => p.Group == moving).Row);
        Assert.Equal(8, discrete.Single(p => p.Group == target).Row);
    }

    [Fact]
    public void ReorderIsOneUndoableEditAndRespectsTheRecipientsLock()
    {
        var moving = Group("Moving", 0);
        var target = Group("Target", 8);
        var document = new LayoutDocument { Groups = [moving, target] };
        var history = new LayoutHistory(); history.Reset(document);
        var before = LayoutHistory.CopyGroups(document.Groups);
        document.Preferences.LockedGroupIds.Add(target.Id);

        GroupLayout.Move(document, moving, 0, 6, 3, 52, 0, reorderVertical: true);

        Assert.False(LayoutLocks.AllowsChange(before, document.Groups, document.Preferences, 3));
        document.Preferences.LockedGroupIds.Clear();
        Assert.True(LayoutLocks.AllowsChange(before, document.Groups, document.Preferences, 3));
        history.Record(document);
        Assert.True(history.Undo(document));
        Assert.Equal(new int?[] { 0, 8 }, document.Groups.Select(g => g.Row));
        Assert.False(history.CanUndo);
        Assert.True(history.Redo(document));
        Assert.Equal(new int?[] { 8, 0 }, document.Groups.Select(g => g.Row));
    }

    private static void AssertDisjoint(IReadOnlyList<GroupPlacement> placements)
    {
        foreach (var a in placements)
            foreach (var b in placements.Where(p => p.Group != a.Group))
                Assert.False(a.Left < b.Left + b.Width && a.Left + a.Width > b.Left && a.Top < b.Top + b.Height && a.Top + a.Height > b.Top);
    }

    private static TileGroup Group(string name, int row, TileSize size = TileSize.Medium) => new()
    {
        Name = name,
        Column = 0,
        Row = row,
        X = 0,
        Tiles = [new() { Label = name, Size = size, Column = 0, Row = 0 }]
    };

    private static IReadOnlyList<GroupPlacement> Drag(LayoutDocument document, TileGroup group, int row, double cell = 52)
        => GroupLayout.PreviewMove(document, group, 0, row, 3, cell, 0, reorderVertical: true);
}
