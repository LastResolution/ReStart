namespace ReStart.Core.Tests;

public class LegacyTileAssetPathTests
{
    [Theory]
    [InlineData("Assets/icon.png", "C:\\OwnedPackage\\Assets\\icon.png")]
    [InlineData("ms-appx:///Assets/icon.png", "C:\\OwnedPackage\\Assets\\icon.png")]
    [InlineData("ms-appdata:///local/icon.png", "C:\\OwnedLocal\\icon.png")]
    public void ResolvesOnlyOwningPackageAndAppLocalAssets(string source, string expected)
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Equal(expected, LegacyTileAssetPath.Resolve(source, @"C:\OwnedPackage", @"C:\OwnedLocal"));
    }

    [Theory]
    [InlineData("https://example.com/image.png")]
    [InlineData("file:///C:/Other/image.png")]
    [InlineData("ms-appx://other/Assets/image.png")]
    [InlineData("../Other/image.png")]
    [InlineData("ms-appdata:///local/%2e%2e/Other/image.png")]
    [InlineData("Assets/active.svg")]
    [InlineData("Assets/script.js")]
    [InlineData("Assets/*.png")]
    [InlineData("\\\\server\\share\\image.png")]
    [InlineData("Assets/image.png:stream")]
    public void RejectsRemoteAbsoluteTraversingAndExecutableSources(string source)
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Null(LegacyTileAssetPath.Resolve(source, @"C:\OwnedPackage", @"C:\OwnedLocal"));
    }
}
