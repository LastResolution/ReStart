using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class GroupDisplacementTests
{
    public static IEnumerable<object[]> HorizontalCases()
    {
        foreach (var scale in new[] { .7, 1.0, 1.5 })
            foreach (var fromRight in new[] { false, true })
                foreach (var movingSize in Enum.GetValues<TileSize>())
                    foreach (var targetSize in Enum.GetValues<TileSize>())
                        yield return [scale, fromRight, movingSize, targetSize];
    }

    [Theory]
    [MemberData(nameof(HorizontalCases))]
    public void HorizontalArrivalUsesTheSourceSideWithoutGrowingRows(double scale, bool fromRight, TileSize movingSize, TileSize targetSize)
    {
        var cell = 52 * scale;
        var moving = Group("Moving", fromRight ? 12 : 0, 0, movingSize);
        var target = Group("Recipient", fromRight ? 2 : 12, 0, targetSize);
        target.Tiles[0].Children = [new() { Label = "Nested app" }];
        var unrelated = Group("Unrelated", 20, 8);
        var document = new LayoutDocument { Groups = [target, unrelated, moving] };
        var before = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);
        var contents = JsonSerializer.Serialize(target.Tiles, LayoutStore.JsonOptions);
        var baseline = Pack(document, cell);

        var preview = GroupLayout.PreviewMove(document, moving, 0, 0, 4, cell, target.X, reorderOnDrag: true);
        var incoming = preview.Single(p => p.Group == moving);
        var recipient = preview.Single(p => p.Group == target);

        Assert.Equal(0, recipient.Row);
        if (fromRight) Assert.True(recipient.Left >= incoming.Left + incoming.Width + GroupLayout.Gap - .001);
        else Assert.True(recipient.Left + recipient.Width <= incoming.Left - GroupLayout.Gap + .001);
        Assert.Equal(baseline.Single(p => p.Group == unrelated), preview.Single(p => p.Group == unrelated));
        Assert.Equal(before, JsonSerializer.Serialize(document, LayoutStore.JsonOptions));
        AssertDisjoint(preview);

        GroupLayout.Move(document, moving, 0, 0, 4, cell, target.X, reorderOnDrag: true);
        Assert.Equal(preview, Pack(document, cell));
        Assert.Equal(contents, JsonSerializer.Serialize(target.Tiles, LayoutStore.JsonOptions));
        Assert.Equal(recipient.Left / cell, target.X!.Value, 6);
    }

    [Theory]
    [InlineData(1.5, false)]
    [InlineData(1.0, true)]
    public void HorizontalOverlapUsesTheSameHalfSizeIntentAsVerticalReordering(double x, bool displaced)
    {
        var moving = Group("Moving", 8, 0);
        var target = Group("Recipient", 0, 0);
        var document = new LayoutDocument { Groups = [moving, target] };
        var before = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);

        var preview = GroupLayout.PreviewMove(document, moving, 0, 0, 4, 52, x, reorderOnDrag: true);

        Assert.Equal(displaced, preview.Single(p => p.Group == target).Left > 0);
        Assert.Equal(before, JsonSerializer.Serialize(document, LayoutStore.JsonOptions));
        GroupLayout.Move(document, moving, 0, 0, 4, 52, x, reorderOnDrag: true);
        Assert.Equal(preview, Pack(document, 52));
    }

    [Fact]
    public void ARecipientKeepsItsVacatedHorizontalSlotAfterPassingItAndReversesFromTheBaseline()
    {
        var moving = Group("Moving", 8, 0);
        var target = Group("Recipient", 4, 0);
        var document = new LayoutDocument { Groups = [moving, target] };
        var before = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);
        foreach (var x in new[] { 5.5, 5, 4, 3, 1, 3, 4, 5, 5.5, 8 })
        {
            var preview = GroupLayout.PreviewMove(document, moving, 0, 0, 4, 52, x, reorderOnDrag: true);
            Assert.Equal(x <= 5 ? 8 * 52 : 4 * 52, preview.Single(p => p.Group == target).Left);
            Assert.Equal(preview, GroupLayout.PreviewMove(document, moving, 0, 0, 4, 52, x, reorderOnDrag: true));
            Assert.Equal(before, JsonSerializer.Serialize(document, LayoutStore.JsonOptions));
            AssertDisjoint(preview);
        }
    }

    [Theory]
    [InlineData(6, 0, 4, 8, "up")]
    [InlineData(6, 16, 4, 8, "down")]
    [InlineData(12, 0, 4, 2, "right")]
    [InlineData(0, 0, 8, 2, "left")]
    public void DiagonalArrivalChoosesItsDominantSourceSide(double sourceX, int sourceRow, double targetX, int targetRow, string direction)
    {
        var moving = Group("Moving", sourceX, sourceRow);
        var target = Group("Recipient", targetX, targetRow);
        var document = new LayoutDocument { Groups = [moving, target] };

        var preview = GroupLayout.PreviewMove(document, moving, 0, targetRow, 4, 52, targetX, reorderOnDrag: true);
        var incoming = preview.Single(p => p.Group == moving);
        var recipient = preview.Single(p => p.Group == target);

        Assert.True(direction switch
        {
            "up" => recipient.Top + recipient.Height <= incoming.Top - GroupLayout.Gap,
            "down" => recipient.Top >= incoming.Top + incoming.Height + GroupLayout.Gap,
            "right" => recipient.Left >= incoming.Left + incoming.Width + GroupLayout.Gap,
            _ => recipient.Left + recipient.Width <= incoming.Left - GroupLayout.Gap
        });
        AssertDisjoint(preview);
        GroupLayout.Move(document, moving, 0, targetRow, 4, 52, targetX, reorderOnDrag: true);
        Assert.Equal(preview, Pack(document, 52));
    }

    [Fact]
    public void AnOccupiedPreferredSideDoesNotMoveAnUnrelatedGroup()
    {
        var moving = Group("Moving", 8, 2);
        var target = Group("Recipient", 0, 0);
        var blocker = Group("Fixed neighbor", 3, 0);
        var document = new LayoutDocument { Groups = [moving, target, blocker] };

        var preview = GroupLayout.PreviewMove(document, moving, 0, 0, 4, 52, 0, reorderOnDrag: true);

        Assert.Equal(3 * 52, preview.Single(p => p.Group == blocker).Left);
        Assert.Equal(0, preview.Single(p => p.Group == blocker).Row);
        Assert.True(preview.Single(p => p.Group == target).Row > 0);
        Assert.Equal(0, preview.Single(p => p.Group == target).Left);
        AssertDisjoint(preview);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InsufficientSpaceAtTheBoardEdgeFallsBackWithoutInvalidAnchors(bool rightEdge)
    {
        var moving = Group("Moving", rightEdge ? 5 : 0, rightEdge ? 6 : 4, TileSize.Small);
        var target = Group("Recipient", rightEdge ? 0 : 1.5, 0, TileSize.Wide);
        var document = new LayoutDocument { Groups = [moving, target] };

        var preview = GroupLayout.PreviewMove(document, moving, 0, 0, 1, 52, rightEdge ? 3 : 1.5, reorderOnDrag: true);

        Assert.True(preview.Single(p => p.Group == target).Row > 0);
        Assert.Equal(target.X!.Value * 52, preview.Single(p => p.Group == target).Left);
        Assert.All(preview, p => { Assert.True(p.Left >= 0); Assert.True(p.Top >= 0); });
        AssertDisjoint(preview);
    }

    [Fact]
    public void MultipleDirectRecipientsKeepTheirOrderOnThePreferredSide()
    {
        var moving = Group("Moving", 12, 0, TileSize.Wide);
        var first = Group("First", 0, 0);
        var second = Group("Second", 2.5, 0);
        var document = new LayoutDocument { Groups = [second, moving, first] };
        var before = JsonSerializer.Serialize(document, LayoutStore.JsonOptions);

        var preview = GroupLayout.PreviewMove(document, moving, 0, 0, 4, 52, 1, reorderOnDrag: true);

        Assert.True(preview.Single(p => p.Group == first).Left > preview.Single(p => p.Group == moving).Left);
        Assert.True(preview.Single(p => p.Group == second).Left > preview.Single(p => p.Group == first).Left);
        Assert.All(preview, p => Assert.Equal(0, p.Row));
        AssertDisjoint(preview);
        Assert.Equal(before, JsonSerializer.Serialize(document, LayoutStore.JsonOptions));
        GroupLayout.Move(document, moving, 0, 0, 4, 52, 1, reorderOnDrag: true);
        Assert.Equal(preview, Pack(document, 52));
    }

    [Fact]
    public void HorizontalDisplacementIsUndoableAndCannotBypassARecipientLock()
    {
        var moving = Group("Moving", 8, 0);
        var target = Group("Recipient", 0, 0);
        var document = new LayoutDocument { Groups = [moving, target] };
        var history = new LayoutHistory(); history.Reset(document);
        var before = LayoutHistory.CopyGroups(document.Groups);
        document.Preferences.LockedGroupIds.Add(target.Id);
        GroupLayout.Move(document, moving, 0, 0, 4, 52, 0, reorderOnDrag: true);
        Assert.False(LayoutLocks.AllowsChange(before, document.Groups, document.Preferences, 4));
        document.Preferences.LockedGroupIds.Clear();
        var after = Pack(document, 52); history.Record(document);
        Assert.True(history.Undo(document));
        Assert.Equal(new double?[] { 8, 0 }, document.Groups.Select(g => g.X));
        Assert.True(history.Redo(document));
        Assert.Equal(after.Select(p => (p.Group.Id, p.Left, p.Top)), Pack(document, 52).Select(p => (p.Group.Id, p.Left, p.Top)));
    }

    [Fact]
    public void DiscreteEditsAndStandaloneEntriesRetainTheirExistingCollisionRules()
    {
        var moving = Group("Moving", 8, 0);
        var target = Group("Recipient", 0, 0);
        var document = new LayoutDocument { Groups = [moving, target] };
        var discrete = GroupLayout.PreviewMove(document, moving, 0, 0, 4, 52, 0);
        Assert.True(discrete.Single(p => p.Group == target).Row > 0);
        Assert.Equal(0, discrete.Single(p => p.Group == target).Left);
        target.IsStandalone = true;
        var independent = GroupLayout.PreviewMove(document, moving, 0, 0, 4, 52, 0, reorderOnDrag: true);
        Assert.Equal(0, independent.Single(p => p.Group == target).Left);
        AssertDisjoint(independent);
    }

    private static IReadOnlyList<GroupPlacement> Pack(LayoutDocument document, double cell)
        => GroupLayout.Pack(document.Groups, 4, document.Preferences.Columns, cell, preserveColumns: true);

    private static void AssertDisjoint(IReadOnlyList<GroupPlacement> placements)
    {
        foreach (var a in placements)
            foreach (var b in placements.Where(p => p.Group != a.Group))
                Assert.False(a.Left < b.Left + b.Width - .001 && a.Left + a.Width > b.Left + .001 && a.Top < b.Top + b.Height - .001 && a.Top + a.Height > b.Top + .001);
    }

    private static TileGroup Group(string name, double x, int row, TileSize size = TileSize.Medium) => new()
    {
        Name = name,
        X = x,
        Column = 0,
        Row = row,
        Tiles = [new() { Label = name, Size = size, Column = 0, Row = 0 }]
    };
}
