using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Verifies encrypted bindings, native configuration, worker data flow and multilingual themes without external requests.</summary>
    internal async Task<TileDataSmokeResult> VerifyTileDataAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Data-source verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var fixture = new MainWindow(new LayoutStore(Path.Combine(directory, "layout.json")), true, () => Task.FromResult(PreviewApps()))
        { Owner = this, Width = 1280, Height = 800, Topmost = false, ShowInTaskbar = false };
        var language = L.Language; var checks = 0; var themes = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        try
        {
            await fixture.InitializeAsync(); fixture.Present(); fixture._clock.Stop();
            var ids = new[] { "weather", "feed", "json-monitor" };
            var files = new[] { "weather.json", "feed.xml", "service.json" };
            File.WriteAllText(Path.Combine(directory, files[0]), "{\"current\":{\"temperature_2m\":18,\"weather_code\":2,\"wind_speed_10m\":12}}");
            File.WriteAllText(Path.Combine(directory, files[1]), "<rss version='2.0'><channel><title>Sample news</title><item><title>First headline</title><description>Read-only data from a configured feed.</description></item><item><title>Second headline</title></item></channel></rss>");
            File.WriteAllText(Path.Combine(directory, files[2]), "{\"text\":\"Connected\",\"detail\":\"Local application data\",\"progress\":0.75}");
            var tiles = ids.Select(id => fixture._scriptPackages.Import(ScriptTilePackages.BundledManifestPath(id))).ToArray();
            for (var i = 0; i < tiles.Length; i++)
            {
                var manifest = fixture._scriptPackages.Read(tiles[i].ScriptId).Manifest;
                var bindings = new Dictionary<string, TileDataBinding>()
                { [manifest.DataSources.Keys.Single()] = new() { Enabled = true, Access = "file", Location = Path.Combine(directory, files[i]) } };
                ScriptDataBindings.Save(fixture._scriptPackages.DataBindingPath(tiles[i].ScriptId, tiles[i].Id), bindings);
                ScriptTileGrants.ApproveData(fixture._scriptPackages.DataGrantPath(tiles[i].ScriptId, tiles[i].Id), tiles[i].Id, tiles[i].ScriptId, manifest, bindings);
            }
            fixture.Document = new()
            {
                Preferences = new() { Language = "en", Animate = false, ShowWallpaper = false, Columns = 8 },
                Groups = [new() { Name = "Connected data", Tiles = [.. tiles] }]
            };
            fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.UpdateLayout();
            await Until(() => fixture._scriptFrames.TryGetValue(tiles[0].Id, out var weather) && weather.Text == "18°C"
                && fixture._scriptFrames.TryGetValue(tiles[1].Id, out var feed) && feed.Text == "First headline"
                && fixture._scriptFrames.TryGetValue(tiles[2].Id, out var service) && service.Text == "Connected");
            Check(fixture.ScriptFrameCount == 3, "Connected samples did not render through actual tile workers.");
            checks += await VerifyHostDataApprovalsAsync(fixture, directory);
            fixture._scriptViews.Single(v => v.Tile == tiles[1]).Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => fixture._scriptFrames[tiles[1].Id].Text == "Second headline");
            Check(fixture.IsVisible, "A feed click launched an external app.");
            File.WriteAllText(Path.Combine(directory, files[2]), "{\"text\":\"Updated\",\"progress\":0.9}");
            await Until(() => fixture._scriptFrames[tiles[2].Id].Text == "Updated");
            Check(fixture._scriptFrames[tiles[2].Id].Progress == .9, "A local producer update did not reach the native frame.");
            fixture._scriptSessions[tiles[2].Id].NextUpdate = DateTimeOffset.Now.AddHours(1);
            File.WriteAllText(Path.Combine(directory, files[2]), "{\"text\":\"Published\",\"progress\":0.9}");
            for (var attempt = 0; attempt < 100 && fixture._scriptFrames[tiles[2].Id].Text != "Published"; attempt++)
            { await fixture.UpdateScriptTilesAsync(); await Task.Delay(100); }
            Check(fixture._scriptFrames[tiles[2].Id].Text == "Published", "Data polling waited for an unrelated script render interval.");
            fixture.SaveScreenshot(Path.Combine(directory, "connected-tiles.png"));
            fixture.SaveTileShowcase(Path.Combine(directory, "connected-gallery.png"));
            var context = new ContextMenu(); fixture.AddScriptMenu(context, tiles[0]);
            Check(context.Items.OfType<MenuItem>().Any(i => Equals(i.Header, L.T(K.ConfigureTileData)))
                && context.Items.OfType<MenuItem>().Any(i => Equals(i.Header, L.T(K.RefreshTileData))), "The data configuration/refresh actions are missing.");

            var credentialPath = Path.Combine(directory, "credentials.dat"); var secret = Guid.NewGuid().ToString("N");
            ScriptDataBindings.Save(credentialPath, new() { ["weather"] = new() { Enabled = true, Location = "https://example.invalid/data", Secret = secret } });
            Check(ScriptDataBindings.Read(credentialPath)["weather"].Secret == secret, "DPAPI credentials did not round-trip.");
            var stored = File.ReadAllBytes(credentialPath);
            Check(!Encoding.UTF8.GetString(stored).Contains(secret, StringComparison.Ordinal)
                && !Encoding.UTF8.GetString(stored).Contains("example.invalid", StringComparison.Ordinal), "A local connection was saved as plaintext.");
            var configuration = fixture._scriptPackages.Read(tiles[0].ScriptId).Manifest;
            foreach (var tag in L.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    fixture.Document.Preferences.Language = tag; fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance();
                    var editor = new TileDataWindow(fixture, configuration, credentialPath);
                    try
                    {
                        editor.Show(); editor.UpdateLayout();
                        Check(editor.Title == L.T(K.ConfigureTileData) && editor.LocationInputs.Count == 1, "The native data editor did not follow the UI language.");
                        if (tag == "en") SaveFixtureElement(editor, Path.Combine(directory, $"data-settings-{theme}.png"));
                        themes++;
                    }
                    finally { editor.Close(); }
                }
            fixture.Document.Preferences.Language = "en"; fixture.ApplyAppearance();
            void SaveEditor(string address)
            {
                var editor = new TileDataWindow(fixture, configuration, credentialPath);
                editor.LocationInputs[0].Text = address;
                editor.Loaded += (_, _) => editor.Dispatcher.BeginInvoke(() => editor.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                Check(editor.ShowDialog() == true, "The native data editor could not save valid connections.");
            }
            SaveEditor("https://example.invalid/next");
            Check(ScriptDataBindings.Read(credentialPath)["weather"].Secret == secret, "Saving the same server lost a blank-field saved credential.");
            SaveEditor("https://different.invalid/data");
            Check(ScriptDataBindings.Read(credentialPath)["weather"].Secret.Length == 0, "Changing servers retained a credential for another origin.");
            File.WriteAllBytes(credentialPath, [1, 2, 3]); var rejected = false;
            try { ScriptDataBindings.Read(credentialPath); } catch (CryptographicException) { rejected = true; }
            Check(rejected, "Corrupt encrypted bindings were accepted.");
            fixture.Hide(); Check(fixture._scriptWorkerPool == null, "Hiding Start retained a worker pool.");
            Check(fixture._scriptSessions.Values.All(s => s.NextUpdate == DateTimeOffset.MinValue), "Reopening would wait for an old frame deadline.");
            return new(checks, themes, directory);
        }
        finally { fixture.Exit(); L.SetLanguage(language); ApplyAppearance(); }

        async Task Until(Func<bool> ready)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                foreach (var runtime in fixture._scriptSessions.Values) runtime.NextUpdate = DateTimeOffset.MinValue;
                await fixture.UpdateScriptTilesAsync(); fixture.UpdateLayout();
                if (ready()) return;
                await Task.Delay(100);
            }
            throw new InvalidOperationException("Connected tile fixture did not receive the expected data.");
        }
    }
}

internal sealed record TileDataSmokeResult(int Checks, int LanguageThemeCases, string Screenshots);
