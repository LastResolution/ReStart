using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises protected-layout update controls against the current portable package without downloads, hooks or production settings.</summary>
    /// <param name="directory">Owned fixture directory for fictional layouts and English update screenshots.</param>
    /// <returns>The number of checked eligibility, persistence and localized UI invariants.</returns>
    internal static int VerifyProtectedUpdateControls(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = 0; var language = L.Language;
        void Check(bool value, string message) { Require(value, message); checks++; }
        try
        {
            foreach (var source in new[] { "newer-producer", "newer-schema", "writable" })
            {
                var path = Path.Combine(directory, source, "layout.json");
                var seed = new LayoutStore(path, source == "newer-producer" ? "99.0.0" : AppBuildInfo.Version);
                seed.Save(new()
                {
                    Preferences = new() { Language = "en", ShowWallpaper = false, Animate = false, ColorThemeId = "" },
                    Groups = [new() { Name = "Preserved sample", Tiles = [new() { Label = "Sample app", Column = 2, Row = 4 }] }]
                });
                if (source == "newer-schema")
                    File.WriteAllText(path, JsonSerializer.Serialize(new { Version = LayoutDocument.CurrentVersion + 1, Groups = new[] { new { Name = "Future sample" } } }));
                var original = File.ReadAllBytes(path);
                var store = new LayoutStore(path, AppBuildInfo.Version);
                var document = store.Load() ?? new();
                document.Preferences.ShowWallpaper = false; document.Preferences.ColorThemeId = "";
                var fixture = new MainWindow(store, false) { Document = document };
                fixture._clock.Stop();
                try
                {
                    Check(store.IsReadOnly == (source != "writable"), "The update fixture did not exercise its expected layout protection.");
                    Check(fixture.CanInstallUpdates, "A complete portable package cannot update with a protected layout.");
                    if (store.IsReadOnly)
                    {
                        Check(fixture.SaveLayoutBeforeUpdate(), "Layout protection blocked the update handoff.");
                        Check(File.ReadAllBytes(path).SequenceEqual(original) && !File.Exists(path + ".bak"), "Preparing to leave rewrote or backed up a protected layout.");
                        var refused = false;
                        try { store.Save(document); } catch (InvalidOperationException) { refused = true; }
                        Check(refused && File.ReadAllBytes(path).SequenceEqual(original), "Update eligibility weakened the layout write guard.");
                    }
                    else
                    {
                        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                            Check(!fixture.SaveLayoutBeforeUpdate(), "An ordinary layout save failure did not block the update handoff.");
                        Check(File.ReadAllBytes(path).SequenceEqual(original), "A failed ordinary save replaced the saved layout.");
                        fixture.Document.Groups[0].Name = "Saved before updating";
                        Check(fixture.SaveLayoutBeforeUpdate() && LayoutStore.Read(path).Groups[0].Name == "Saved before updating", "A writable layout was not saved before updating.");
                    }
                    foreach (var tag in L.SupportedLanguages)
                        foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                        {
                            fixture.Document.Preferences.Language = tag; fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance();
                            fixture.LastUpdateCheck = null; fixture.UpdatePreparation = PreparationState.Idle; fixture.PreparedUpdate = null;
                            var editor = new SettingsWindow(fixture, store) { ShowActivated = false };
                            try
                            {
                                editor.Show(); editor.SelectSection(L.T(K.SettingsUpdates)); editor.UpdateLayout();
                                var buttons = VisualChildren<Button>(editor).ToList();
                                var download = buttons.Single(button => Equals(button.Content, L.T(K.UpdateDownload)));
                                var restart = buttons.Single(button => Equals(button.Content, L.T(K.UpdateRestart)));
                                Check(!download.IsEnabled, "Download was enabled without an available release.");
                                fixture.LastUpdateCheck = new(UpdateCheckStatus.Available, new("v99.0.0", ReleaseVersion.Parse("99.0.0")!, "Fictional release notes."));
                                fixture.UpdateCheckChanged?.Invoke();
                                Check(download.IsEnabled, "Protected settings disabled the available update action.");
                                Check(!VisualChildren<TextBlock>(editor).Any(text => text.IsVisible && text.Text == L.T(K.UpdatePortableRequired)), "A protected layout was misreported as an invalid portable installation.");
                                Check(VisualChildren<TextBlock>(editor).Any(text => text.IsVisible && text.Text == L.T(K.MigrationReadOnly)) == store.IsReadOnly, "Updates omitted or incorrectly displayed the layout protection explanation.");
                                fixture.PreparedUpdate = new(Guid.NewGuid().ToString("N"), directory, "99.0.0"); fixture.UpdatePreparation = PreparationState.Ready;
                                fixture.UpdateCheckChanged?.Invoke(); editor.UpdateLayout();
                                Check(!download.IsEnabled && restart.IsEnabled && restart.Visibility == Visibility.Visible, "A protected layout disabled the prepared restart action.");
                                if (source == "newer-producer" && tag == "en") SaveStartupScreenshot(editor, Path.Combine(directory, $"protected-update-{theme}.png"));
                            }
                            finally { editor.DiscardAndClose(); }
                        }
                    if (store.IsReadOnly) Check(File.ReadAllBytes(path).SequenceEqual(original), "Opening update controls changed a protected layout.");
                }
                finally { fixture.Exit(); }
                var preview = new MainWindow(store, true);
                try { Check(!preview.CanInstallUpdates, "Preview mode enabled installation updates."); }
                finally { preview.Exit(); }
            }
            return checks;
        }
        finally { L.SetLanguage(language); }
    }
}
