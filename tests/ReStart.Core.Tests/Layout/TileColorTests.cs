using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class TileColorTests
{
    [Fact]
    public void AnExplicitColorRoundTripsWithoutPromotingLegacyDecorativeColors()
    {
        var document = new LayoutDocument { Groups = [new() { Tiles = [new() { Color = "#107C10", UseCustomColor = true }, new() { Color = "#E81123" }] }] };
        var restored = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(document, LayoutStore.JsonOptions), LayoutStore.JsonOptions)!;
        restored.Normalize();
        Assert.True(restored.Groups[0].Tiles[0].UseCustomColor);
        Assert.Equal("#107C10", restored.Groups[0].Tiles[0].Color);
        Assert.False(restored.Groups[0].Tiles[1].UseCustomColor);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#FF000000")]
    [InlineData("#GG0000")]
    public void InvalidImportedOverridesFallBackToTheTheme(string color)
    {
        var tile = new Tile { Color = color, UseCustomColor = true };
        new LayoutDocument { Groups = [new() { Tiles = [tile] }] }.Normalize();
        Assert.False(tile.UseCustomColor);
    }
}
