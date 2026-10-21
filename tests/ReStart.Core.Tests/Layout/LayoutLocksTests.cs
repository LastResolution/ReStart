using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class LayoutLocksTests
{
    [Fact]
    public void GlobalLockRejectsGeometryAndMembershipButAllowsPresentation()
    {
        var original = Groups();
        var preferences = new Preferences { LayoutLocked = true };
        var candidate = LayoutHistory.CopyGroups(original);
        candidate[0].Name = "Renamed";
        candidate[0].Tiles[0].Label = "New label";
        candidate[0].Tiles[0].Color = "#123456";
        candidate[0].Tiles[0].Live = false;
        Assert.True(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
        candidate[0].Tiles[0].Size = TileSize.Large;
        Assert.False(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
        candidate = LayoutHistory.CopyGroups(original);
        candidate[0].Tiles.RemoveAt(0);
        Assert.False(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
        candidate = LayoutHistory.CopyGroups(original);
        candidate[0].Tiles[0].Children.Add(new() { Label = "Child" });
        Assert.False(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
        candidate = LayoutHistory.CopyGroups(original);
        candidate.Add(new() { Tiles = [new()] });
        Assert.False(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
    }

    [Fact]
    public void GroupLockAllowsUnrelatedMovesButProtectsCollisionDisplacement()
    {
        var original = Groups();
        var preferences = new Preferences { LockedGroupIds = [original[0].Id] };
        var candidate = LayoutHistory.CopyGroups(original);
        candidate[1].X = 15;
        Assert.True(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
        candidate[0].X = 4;
        Assert.False(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
        candidate = LayoutHistory.CopyGroups(original);
        candidate.Reverse(); candidate[0].X = 0;
        Assert.False(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
        candidate = LayoutHistory.CopyGroups(original);
        candidate[0].Tiles[0].Column = 0; candidate[0].Tiles[0].Row = 4;
        Assert.False(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
        candidate = LayoutHistory.CopyGroups(original);
        candidate[0].Tiles.Clear();
        candidate[1].Tiles.Add(original[0].Tiles[0]);
        Assert.False(LayoutLocks.AllowsChange(original, candidate, preferences, 4));
        Assert.Single(original[0].Tiles);
        Assert.Null(original[0].Tiles[0].Row);
    }

    [Fact]
    public void UndoDoesNotRestoreOldLockChoices()
    {
        var document = new LayoutDocument { Groups = Groups() };
        var history = new LayoutHistory(); history.Reset(document);
        document.Groups[0].Name = "After"; history.Record(document);
        document.Preferences.LayoutLocked = true;
        document.Preferences.LockedGroupIds.Add(document.Groups[0].Id);
        Assert.True(history.Undo(document));
        Assert.True(document.Preferences.LayoutLocked);
        Assert.Contains(document.Groups[0].Id, document.Preferences.LockedGroupIds);
    }

    private static List<TileGroup> Groups() =>
    [
        new() { Name = "Protected", Column = 0, X = 0, Row = 0, Tiles = [new() { Label = "First" }] },
        new() { Name = "Other", Column = 1, X = 8, Row = 0, Tiles = [new() { Label = "Second" }] }
    ];
}

