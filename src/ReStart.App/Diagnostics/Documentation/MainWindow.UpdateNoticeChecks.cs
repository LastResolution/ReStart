using System.Windows;
using System.Windows.Controls;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private int VerifyUpdateNotices(string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        var version = ReleaseVersion.Parse("1.5.1")!;
        var available = new UpdateCheckResult(UpdateCheckStatus.Available, new("v1.5.1", version, "Fictional preview release."));
        var notifications = 0;
        void Received(UpdateRelease release) { notifications++; }
        StartupUpdateAvailable += Received;
        try
        {
            foreach (var status in Enum.GetValues<UpdateCheckStatus>().Where(s => s != UpdateCheckStatus.Available)) PresentStartupUpdate(new(status));
            Check(notifications == 0 && _updateNotice == null, "A current or unsuccessful update check displayed a notice.");
            Document.Preferences.CheckUpdatesOnStartup = false; PresentStartupUpdate(available);
            Check(notifications == 0, "Disabling startup checks did not disable their notices.");
            Document.Preferences.CheckUpdatesOnStartup = true; PresentStartupUpdate(available); PresentStartupUpdate(available);
            Check(notifications == 1 && _updateNotice != null, "The available release was missed or notified repeatedly.");
            _updateNotice!.LaterButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(_updateNotice == null, "Not now did not dismiss the update notice.");
            foreach (var language in L.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    Document.Preferences.Language = language; Document.Preferences.Theme = theme; ApplyAppearance();
                    var views = 0; var dismissals = 0;
                    var notice = new UpdateNotice("1.5.1", "1.5.0", () => views++, () => dismissals++);
                    var panel = new Border { Width = 488, Padding = new(24), Child = notice, Background = Root.Background };
                    panel.Measure(new(488, 400)); panel.Arrange(new Rect(0, 0, 488, panel.DesiredSize.Height)); panel.UpdateLayout();
                    Check(notice.ActualHeight > 100 && notice.ActualHeight < 350 && notice.ViewReleaseButton.ActualWidth > 0, "A localized notice clipped its actions.");
                    notice.ViewReleaseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); notice.LaterButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(views == 1 && dismissals == 1, "An update notice executed the wrong action.");
                    if (language is "en" or "ja") SaveFixtureElement(panel, Path.Combine(directory, $"update-notice-{language}-{theme}.png"));
                }
            Check(!IsVisible, "Rendering an update notice opened a window.");
        }
        finally
        {
            StartupUpdateAvailable -= Received; _startupUpdateNotified = false;
            Document.Preferences.Language = "en"; Document.Preferences.Theme = ReStart.Core.ThemeMode.Dark; ApplyAppearance();
        }
        return checks;
    }
}
