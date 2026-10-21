using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class SeedLayoutTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SparseCatalogStillProducesTwoUsableGroupsWithDistinctOfflinePackages(int appCount)
    {
        using var temporary = new PackageDirectory();
        var apps = new[] { new AppEntry("browser", "Firefox", "browser"), new AppEntry("editor", "Notepad", "editor") }.Take(appCount).ToArray();
        var document = SeedLayout.Create(apps, temporary.Packages);

        Assert.Equal(2, document.Groups.Count);
        Assert.All(document.Groups, group => Assert.Equal(6, group.Tiles.Count));
        var tiles = document.Groups.SelectMany(group => group.Tiles).ToArray();
        Assert.Equal(tiles.Length, tiles.Select(tile => tile.AppId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(apps, app => Assert.Single(tiles, tile => tile.AppId == app.Id && tile.Target == app.LaunchTarget));
        Assert.Single(tiles, tile => tile.AppId == "restart:explorer");
        Assert.Single(tiles, tile => tile.AppId == "restart:settings");
        var manifests = tiles.Where(tile => tile.Widget == "script").Select(tile => temporary.Packages.Read(tile.ScriptId).Manifest).ToArray();
        Assert.Equal(10 - appCount, manifests.Length);
        Assert.Equal(manifests.Length, manifests.Select(manifest => manifest.Title).Distinct().Count());
        Assert.All(manifests, manifest =>
        {
            Assert.Empty(manifest.DataSources);
            Assert.DoesNotContain("data.sources", manifest.Capabilities);
            Assert.DoesNotContain("photos.slideshow", manifest.Capabilities);
        });
        Assert.All(tiles, tile => { Assert.Null(tile.Column); Assert.Null(tile.Row); });
    }

    [Fact]
    public void RichCatalogRetainsRecognizedAppsInTheirRolesWithoutUnnecessaryFallbacks()
    {
        using var temporary = new PackageDirectory();
        var everydayNames = new[] { "Firefox", "Outlook", "Photos", "Spotify", "Microsoft Store", "Discord", "VLC" };
        var toolNames = new[] { "Notepad", "Calculator", "Visual Studio Code", "Paint" };
        var names = everydayNames.Concat(toolNames).ToArray();
        var apps = names.Select(name => new AppEntry("fixture:" + name, name, "fixture:" + name)).ToArray();

        var document = SeedLayout.Create(apps.Reverse().ToArray(), temporary.Packages);

        Assert.Equal(everydayNames, document.Groups[0].Tiles.Where(tile => tile.Widget != "script").Select(tile => tile.Label));
        Assert.Equal(toolNames, document.Groups[1].Tiles.Take(4).Select(tile => tile.Label));
        Assert.Equal(9, document.Groups[0].Tiles.Count);
        Assert.Equal(6, document.Groups[1].Tiles.Count);
        Assert.Equal(2, Directory.GetDirectories(temporary.Packages.Root).Length);
        Assert.All(apps, app => Assert.Single(document.Groups.SelectMany(group => group.Tiles), tile => tile.AppId == app.Id && tile.Target == app.LaunchTarget));
    }

    [Fact]
    public void SeededTilesPackWithinSupportedGroupWidthsWithoutCollisions()
    {
        using var temporary = new PackageDirectory();
        var document = SeedLayout.Create([], temporary.Packages);
        foreach (var columns in new[] { 4, 6, 8, 16 })
            foreach (var group in document.Groups)
            {
                var occupied = new HashSet<(int X, int Y)>();
                foreach (var placement in TileLayout.Pack(group.Tiles, columns))
                {
                    Assert.InRange(placement.Column, 0, columns - placement.Width);
                    for (var y = placement.Row; y < placement.Row + placement.Height; y++)
                        for (var x = placement.Column; x < placement.Column + placement.Width; x++)
                            Assert.True(occupied.Add((x, y)), "Seeded tile rectangles must not overlap.");
                }
            }
    }

    private sealed class PackageDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "ReStart-seed-" + Guid.NewGuid().ToString("N"));
        internal ScriptTilePackages Packages { get; }
        internal PackageDirectory() => Packages = new(_path);
        public void Dispose() { if (Directory.Exists(_path)) Directory.Delete(_path, true); }
    }
}
