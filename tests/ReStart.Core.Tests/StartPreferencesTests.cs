using System.Text.Json;

namespace ReStart.Core.Tests;

public class StartPreferencesTests
{
    [Fact]
    public void OlderPreferencesKeepFunctionalDefaultsWithoutEnablingUsageRecording()
    {
        var preferences = JsonSerializer.Deserialize<Preferences>("{}")!;
        Assert.True(preferences.SearchSettings && preferences.SearchFiles && preferences.ShowJumpLists && preferences.ExpandNavigationOnHover);
        Assert.True(preferences.TileHoverHighlight && preferences.DragEdgeScrolling && preferences.ShowDropHints);
        Assert.True(preferences.GroupTilesOnDrop && preferences.CreateFoldersOnDrop && preferences.ShowGroupBoundariesOnHover && preferences.ShowRecentlyAdded);
        Assert.False(preferences.ShowMostUsed); Assert.Null(preferences.NavigationFolders);
    }
    [Fact]
    public void TileOptionsRoundTripWithoutChangingTheTargetOrDefaultLaunch()
    {
        var tile = new Tile { Target = "sample.exe", LaunchArguments = "--example \"two words\"", WorkingDirectory = "%TEMP%", RunAsAdministrator = true };
        var restored = JsonSerializer.Deserialize<Tile>(JsonSerializer.Serialize(tile))!;
        Assert.False(restored.UseLaunchOptions);
        Assert.Equal(tile.Target, restored.Target); Assert.Equal(tile.LaunchArguments, restored.LaunchArguments);
        Assert.Equal(tile.WorkingDirectory, restored.WorkingDirectory); Assert.True(restored.RunAsAdministrator);
        var document = new LayoutDocument { Groups = [new() { Tiles = [restored] }] };
        document.Normalize(); Assert.Equal(tile.LaunchArguments, restored.LaunchArguments);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectsNulInEitherLaunchField(bool arguments)
    {
        var tile = new Tile();
        if (arguments) tile.LaunchArguments = "ab\0cd"; else tile.WorkingDirectory = "ab\0cd";
        Assert.Throws<InvalidDataException>(() => new LayoutDocument { Groups = [new() { Tiles = [tile] }] }.Normalize());
    }
}
