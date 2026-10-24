using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private static int VerifyProfileBackupSettings(MainWindow fixture, LayoutStore store, string directory)
    {
        var checks = 0; var original = JsonSerializer.Serialize(fixture.Document, LayoutStore.JsonOptions);
        void Check(bool value, string message) { Require(value, message); checks++; }
        var archive = Path.Combine(Path.GetDirectoryName(directory)!, "profile-fixture.zip");
        ProfileBackup.Export(archive, directory, fixture.Document);
        using (var pending = ProfileBackup.PrepareRestore(archive, directory))
        {
            pending.Commit(store.Save); fixture.AdoptRestoredProfile(pending.Layout);
            Check(JsonSerializer.Serialize(fixture.Document, LayoutStore.JsonOptions) == original, "Profile adoption changed fictional app identities or placement.");
        }
        foreach (var language in L.SupportedLanguages)
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                fixture.Document.Preferences.Language = language; fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance();
                var settings = new SettingsWindow(fixture, store) { Owner = fixture, ShowActivated = false };
                try
                {
                    settings.Show(); settings.SelectSection(L.T(K.SettingsLayout)); settings.UpdateLayout();
                    var buttons = VisualChildren<Button>(settings).ToArray();
                    Check(buttons.Single(b => Equals(b.Content, L.T(K.BackupCreate))).IsEnabled && buttons.Single(b => Equals(b.Content, L.T(K.BackupRestore))).IsEnabled, "Backup actions are unavailable in a writable profile.");
                    Check(VisualChildren<TextBlock>(settings).Any(t => t.IsVisible && t.Text == L.T(K.BackupHelp)), "Backup scope and credential exclusions are not visible.");
                    if (language == "en") SaveStartupScreenshot(settings, Path.Combine(directory, $"profile-backup-{theme}.png"));
                    settings.SelectSection(L.T(K.SettingsScriptTiles)); settings.UpdateLayout();
                    Check(VisualChildren<CheckBox>(settings).Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == L.T(K.PauseOffscreenLiveTiles))
                        && VisualChildren<CheckBox>(settings).Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == L.T(K.AutoRecoverLiveTiles)), "New live-tile controls lack localized accessible names.");
                }
                finally { settings.DiscardAndClose(); }
            }
        fixture.Document.Preferences.LayoutLocked = true;
        var locked = new SettingsWindow(fixture, store) { Owner = fixture, ShowActivated = false };
        try
        {
            locked.Show(); locked.SelectSection(L.T(K.SettingsLayout)); locked.UpdateLayout();
            Check(!VisualChildren<Button>(locked).Single(b => Equals(b.Content, L.T(K.BackupRestore))).IsEnabled, "Profile restore bypasses layout locks.");
        }
        finally { locked.DiscardAndClose(); }
        fixture.Document = JsonSerializer.Deserialize<LayoutDocument>(original, LayoutStore.JsonOptions)!;
        fixture.RefreshLayout(); store.Save(fixture.Document);
        return checks;
    }
}
