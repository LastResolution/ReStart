using System.Text;
using System.Text.Json;

namespace ReStart.Core.Tests;

public class AppTileArtworkTests
{
    private static AppTileArtwork? Read(string xml, string? app = "Main") => AppTileArtwork.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)), app);
    private static string Package(string body) => "<Package xmlns='http://schemas.microsoft.com/appx/manifest/foundation/windows10' xmlns:uap='http://schemas.microsoft.com/appx/manifest/uap/windows10'><Applications>" + body + "</Applications></Package>";

    [Fact]
    public void ReadsOnlyTheExactSelectedApplicationAndAllFourSizes()
    {
        var xml = Package("<Application Id='Other'><uap:VisualElements Square150x150Logo='other.png'/></Application>"
            + "<Application Id='Main'><uap:VisualElements Square150x150Logo='medium.png' BackgroundColor='#123456'><uap:DefaultTile Square71x71Logo='small.png' Wide310x150Logo='wide.png' Square310x310Logo='large.png'><uap:ShowNameOnTiles><uap:ShowOn Tile='wide310x150Logo'/></uap:ShowNameOnTiles></uap:DefaultTile></uap:VisualElements></Application>");
        var result = Read(xml)!;
        Assert.Equal(4, result.Images.Count);
        Assert.Equal("small.png", result.Images[TileSize.Small]); Assert.Equal("medium.png", result.Images[TileSize.Medium]);
        Assert.Equal("wide.png", result.Images[TileSize.Wide]); Assert.Equal("large.png", result.Images[TileSize.Large]);
        Assert.Equal("#123456", result.Background); Assert.Equal([TileSize.Wide], result.NamedSizes);
        Assert.Null(Read(xml, "Missing")); Assert.Null(Read(xml, "main"));
    }

    [Fact]
    public void MissingSizesDoNotInventWideArtworkOrConfuseSplashAndListIcons()
    {
        var result = Read(Package("<Application Id='Main'><uap:VisualElements Square150x150Logo='tile.png' Square44x44Logo='list.png'><uap:SplashScreen Image='splash.png'/></uap:VisualElements></Application>"))!;
        Assert.Equal("tile.png", result.Images[TileSize.Small]); Assert.False(result.Images.ContainsKey(TileSize.Wide));
        Assert.False(result.Images.ContainsKey(TileSize.Large)); Assert.Empty(result.NamedSizes);
    }

    [Theory]
    [InlineData("<Application><VisualElements Square150x150Logo='tile.png' Square70x70Logo='small.png' ForegroundText='light' ShowNameOnSquare150x150Logo='off'/></Application>")]
    [InlineData("<Application xmlns='http://schemas.microsoft.com/2013/startmenu'><VisualElements Square150x150Logo='tile.png' Square70x70Logo='small.png' ForegroundText='light' ShowNameOnSquare150x150Logo='off'/></Application>")]
    public void ReadsDesktopManifestsWithoutLaunchingTheExecutable(string xml)
    {
        var result = Read(xml, null)!;
        Assert.Equal("small.png", result.Images[TileSize.Small]); Assert.Equal("light", result.Foreground); Assert.Empty(result.NamedSizes);
    }

    [Theory]
    [InlineData("<!DOCTYPE Application [<!ENTITY x SYSTEM 'file:///C:/fixture.txt'>]><Application><VisualElements Square150x150Logo='&x;'/></Application>")]
    [InlineData("<Application xmlns='urn:unrelated'><VisualElements Square150x150Logo='tile.png'/></Application>")]
    [InlineData("<Application><Nested><VisualElements Square150x150Logo='tile.png'/></Nested></Application>")]
    [InlineData("<Application>")]
    public void RejectsEntitiesUnrelatedNamespacesAndNestedDeclarations(string xml) => Assert.Null(Read(xml, null));

    [Fact]
    public void BoundsXmlAndRejectsSpoofedPackageVisuals()
    {
        Assert.Null(Read(new string(' ', 2_000_001)));
        Assert.Null(Read(Package("<Application Id='Main'><VisualElements xmlns='urn:spoof' Square150x150Logo='other.png'/></Application>")));
    }

    [Theory]
    [InlineData(100, "tile.scale-100.png")]
    [InlineData(180, "tile.scale-200.png")]
    [InlineData(400, "tile.scale-400.png")]
    [InlineData(800, "tile.scale-400.png")]
    public void PrefersSufficientResolutionBeforeScalingUp(int pixels, string expected)
    {
        var result = AppTileArtwork.RankVariants("tile.png", ["tile.scale-400.png", "tile.scale-100.png", "tile.scale-200.png"], 100, pixels, false, "en-US");
        Assert.Equal(expected, result[0]);
    }

    [Theory]
    [InlineData(true, "en-US", "tile.scale-200_language-en-US_theme-light.png")]
    [InlineData(false, "en-US", "tile.scale-200_language-en_theme-dark.png")]
    [InlineData(true, "ja-JP", "tile.scale-100.png")]
    public void SelectsThemeAndLanguageBeforeGenericImages(bool light, string language, string expected)
    {
        var result = AppTileArtwork.RankVariants("tile.png", ["tile.scale-100.png", "tile.scale-200_language-en-US_theme-light.png", "tile.scale-200_language-en_theme-dark.png"], 150, 150, light, language);
        Assert.Equal(expected, result[0]);
    }

    [Theory]
    [InlineData("other.scale-200.png")]
    [InlineData("tile.scale-0.png")]
    [InlineData("tile.scale-200_scale-100.png")]
    [InlineData("tile.scale-200_targetsize-64.png")]
    [InlineData("tile.targetsize-64_altform-unplated.png")]
    [InlineData("tile.scale-200_contrast-white.png")]
    [InlineData("tile.scale-200_theme-dark.png")]
    [InlineData("tile.scale-200_language-ja.png")]
    [InlineData("tile.scale-200_unknown-value.png")]
    [InlineData("../tile.png")]
    public void ExcludesIncompatibleMalformedOrUnrelatedVariants(string candidate)
        => Assert.Empty(AppTileArtwork.RankVariants("tile.png", [candidate], 150, 150, true, "en-US"));

    [Fact]
    public void DefaultAndExplicitOffRoundTripWithoutEnablingNotificationAccess()
    {
        var older = JsonSerializer.Deserialize<Preferences>("{}")!;
        Assert.True(older.UseAppTileImages); Assert.False(older.LegacyUwpLiveTiles);
        older.UseAppTileImages = false;
        var restored = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(older))!;
        Assert.False(restored.UseAppTileImages); Assert.False(restored.LegacyUwpLiveTiles);
    }
}
