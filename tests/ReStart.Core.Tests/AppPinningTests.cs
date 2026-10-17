using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class AppPinningTests
{
    private static readonly AppEntry App = new("app-id", "Sample app", "sample-target");
    [Fact]
    public void PinCreatesAnUnnamedGroupAndPreservesLaunchIdentity()
    {
        var document = new LayoutDocument();
        document.Groups.Clear();
        var pin = AppPinning.Pin(document, App);
        Assert.Same(pin, Assert.Single(Assert.Single(document.Groups).Tiles));
        Assert.Equal("", document.Groups[0].Name);
        Assert.Equal(App.LaunchTarget, pin.Target);
        Assert.True(pin.IsShellApp);
    }
    [Fact]
    public void PinDoesNotDuplicateAnExistingFolderChildOrMoveIt()
    {
        var pin = Tile.FromApp(App);
        pin.Row = 6; pin.Column = 2;
        var folder = new Tile { Label = "Folder", Children = [pin] };
        var document = new LayoutDocument { Groups = [new() { Tiles = [folder] }, new() { Name = "Destination" }] };
        Assert.Same(pin, AppPinning.Pin(document, App with { Id = "APP-ID" }, document.Groups[1]));
        Assert.Empty(document.Groups[1].Tiles);
        Assert.True(AppPinning.IsPinned(document, App));
        Assert.Equal(6, pin.Row);
        Assert.Equal(2, pin.Column);
    }
    [Fact]
    public void UnpinRemovesLegacyDuplicatesAndOnlyNewlyEmptyContainers()
    {
        var other = new Tile { AppId = "other", Label = "Other" };
        var folder = new Tile { Label = "Keep folder", Children = [Tile.FromApp(App), other] };
        var empty = new TileGroup { Name = "Existing empty group" };
        var keep = new TileGroup { Tiles = [folder] };
        var document = new LayoutDocument
        {
            Groups = [new() { Tiles = [Tile.FromApp(App)] },
            new() { Tiles = [new() { Children = [Tile.FromApp(App)] }] }, keep, empty]
        };
        Assert.Equal(3, AppPinning.Unpin(document, App));
        Assert.Equal(new[] { keep, empty }, document.Groups);
        Assert.Same(other, Assert.Single(Assert.Single(document.Groups[0].Tiles).Children));
        Assert.False(AppPinning.IsPinned(document, App));
    }
    [Fact]
    public void EqualDisplayNamesDoNotSharePinState()
    {
        var document = new LayoutDocument();
        AppPinning.Pin(document, App);
        var sameName = App with { Id = "different-app-id" };
        Assert.False(AppPinning.IsPinned(document, sameName));
        Assert.Equal(0, AppPinning.Unpin(document, sameName));
        Assert.True(AppPinning.IsPinned(document, App));
    }
    [Fact]
    public void UnknownDestinationIsRejectedBeforeChangingPins()
    {
        var document = new LayoutDocument();
        Assert.Throws<ArgumentException>(() => AppPinning.Pin(document, App, new()));
        Assert.False(AppPinning.IsPinned(document, App));
    }
}
