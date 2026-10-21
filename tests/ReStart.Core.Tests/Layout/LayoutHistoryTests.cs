using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class LayoutHistoryTests
{
    [Fact]
    public void UndoAndRedoRestoreNestedContentButKeepPreferencesAndVersionMetadata()
    {
        var folder = new Tile { Children = [new() { Label = "Original", UseCustomColor = true, Color = "#123456" }] };
        var document = new LayoutDocument { Groups = [new() { X = 2.5, Column = 0, Row = 6, Tiles = [folder] }] };
        var history = new LayoutHistory(); history.Reset(document);
        folder.Children[0].Label = "Changed"; folder.Children[0].Size = TileSize.Large;
        document.Groups[0].X = 8; history.Record(document);
        document.Preferences.Theme = ThemeMode.Light; document.SavedWithAppVersion = "9.0.0";
        Assert.True(history.Undo(document));
        var restored = Assert.Single(Assert.Single(document.Groups).Tiles).Children[0];
        Assert.Equal("Original", restored.Label); Assert.Equal(TileSize.Medium, restored.Size);
        Assert.True(restored.UseCustomColor); Assert.Equal("#123456", restored.Color);
        Assert.Equal(2.5, document.Groups[0].X); Assert.Equal(folder.Id, document.Groups[0].Tiles[0].Id);
        Assert.Equal(ThemeMode.Light, document.Preferences.Theme); Assert.Equal("9.0.0", document.SavedWithAppVersion);
        Assert.True(history.Redo(document)); Assert.Equal("Changed", document.Groups[0].Tiles[0].Children[0].Label);
        Assert.Equal(8, document.Groups[0].X);
    }

    [Fact]
    public void IdenticalSavesKeepRedoAndANewEditDiscardsIt()
    {
        var document = new LayoutDocument(); var history = new LayoutHistory(); history.Reset(document);
        document.Groups.Add(new()); history.Record(document); history.Undo(document);
        document.Preferences.Animate = false; history.Record(document); Assert.True(history.CanRedo);
        document.Groups.Add(new() { Name = "Replacement" }); history.Record(document); Assert.False(history.CanRedo);
        history.RestoreCurrent(document); document.Groups[0].Name = "Unsaved"; history.RestoreCurrent(document);
        Assert.Equal("Replacement", document.Groups[0].Name);
    }

    [Fact]
    public void HistoryLimitsAndResetDropOldSnapshots()
    {
        var document = new LayoutDocument(); var history = new LayoutHistory(maximumEdits: 2); history.Reset(document);
        for (var i = 0; i < 4; i++) { document.Groups = [new() { Name = i.ToString() }]; history.Record(document); }
        Assert.True(history.Undo(document)); Assert.True(history.Undo(document)); Assert.False(history.Undo(document));
        Assert.Equal("1", document.Groups[0].Name);
        history.Reset(document); Assert.False(history.CanUndo); Assert.False(history.CanRedo);
        var bounded = new LayoutHistory(maximumBytes: 1); bounded.Reset(document);
        document.Groups.Clear(); bounded.Record(document); Assert.False(bounded.CanUndo);
    }
}
