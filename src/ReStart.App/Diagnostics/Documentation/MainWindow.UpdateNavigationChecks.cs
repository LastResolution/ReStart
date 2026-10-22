using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises the actual notice and tray destination with visible, hidden and already editing fictional windows.</summary>
    /// <param name="fixture">Isolated initialized launcher; production settings, hooks and downloads are disabled.</param>
    /// <param name="store">Fixture-only settings store used to detect unintended persistence.</param>
    /// <param name="directory">Owned output directory for update-choice screenshots.</param>
    /// <returns>Number of checked navigation and draft-preservation invariants.</returns>
    private static int VerifyAvailableUpdateNavigation(MainWindow fixture, LayoutStore store, string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        const string releaseNotes = "Fictional release notes for the update choice.";
        fixture.LastUpdateCheck = new(UpdateCheckStatus.Available, new("v9.0.0", ReleaseVersion.Parse("9.0.0")!, releaseNotes));
        foreach (var language in L.SupportedLanguages)
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                fixture.Document.Preferences.Language = language; fixture.Document.Preferences.Theme = theme;
                fixture.RefreshLayout(); store.Save(fixture.Document);
                fixture._startupUpdateNotified = false;
                fixture.PresentStartupUpdate(fixture.LastUpdateCheck);
                var notice = fixture._updateNotice!;
                Check(Equals(notice.ActionButton.Content, L.T(K.UpdateReview)), "The available notice does not offer an in-app update choice.");
                var original = JsonSerializer.Serialize(fixture.Document.Preferences); var disk = File.ReadAllBytes(store.FilePath);
                Review(() => notice.ActionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), editor =>
                {
                    Check(fixture.IsVisible && fixture._updateNotice == null && ShowingNotes(editor), "The startup notice did not open Updates with release notes.");
                    Check(VisualChildren<Button>(editor).Any(b => b.IsVisible && Equals(b.Content, L.T(K.UpdateDownload))), "The update choice has no download action.");
                    if (language == "en") SaveStartupScreenshot(editor, Path.Combine(directory, $"update-choice-{theme}.png"));
                    editor.SelectSection(L.T(K.SettingsAppearance));
                    editor.UpdateLayout();
                    var color = VisualChildren<TextBox>(editor).Single(box => AutomationProperties.GetName(box) == L.T(K.SettingsBackgroundColor));
                    color.Text = "#123456"; editor.ApplyDraftPreview();
                    Check(editor.HasPendingChanges, "The draft-preservation case did not create an edit.");
                    fixture.ReviewAvailableUpdate(); // Same destination used by the Windows notification.
                    Check(fixture._activeSettings == editor && ShowingNotes(editor)
                        && Application.Current.Windows.OfType<SettingsWindow>().Count(w => w.IsVisible) == 1,
                        "Reviewing an update opened a second editor or missed the update section.");
                    Check(editor.HasPendingChanges && fixture.DisplayPreferences.BackgroundColor == "#123456"
                        && JsonSerializer.Serialize(fixture.Document.Preferences) == original && File.ReadAllBytes(store.FilePath).SequenceEqual(disk),
                        "Reviewing an update saved or discarded the existing draft.");
                });
                Check(fixture.UpdatePreparation == PreparationState.Idle && fixture.PreparedUpdate == null,
                    "Reviewing or closing an update choice started preparation.");
            }
        fixture.Hide();
        Review(fixture.ReviewAvailableUpdate, editor => Check(fixture.IsVisible && ShowingNotes(editor), "The Windows notification destination did not present a hidden launcher at Updates."));
        return checks;

        static bool ShowingNotes(SettingsWindow editor)
        {
            editor.UpdateLayout();
            return VisualChildren<TextBox>(editor).Any(box => box.IsVisible && box.IsReadOnly && box.Text == releaseNotes);
        }
        void Review(Action open, Action<SettingsWindow> inspect)
        {
            Exception? error = null; var opened = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) =>
            {
                if (fixture._activeSettings is not { IsVisible: true } editor) return;
                timer.Stop(); opened++;
                try { editor.UpdateLayout(); inspect(editor); }
                catch (Exception ex) { error = ex; }
                finally { editor.DiscardAndClose(); }
            };
            timer.Start();
            try { open(); }
            finally { timer.Stop(); }
            if (error != null) throw new InvalidOperationException("The native update-choice navigation fixture failed.", error);
            Check(opened == 1 && fixture._activeSettings == null, "The update choice did not open and close exactly once.");
        }
    }
}
