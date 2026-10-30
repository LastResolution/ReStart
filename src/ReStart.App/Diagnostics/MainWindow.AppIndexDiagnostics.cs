using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.App.Services;
using ReStart.Core;
using Windows.Globalization.Collation;

namespace ReStart.App;

public partial class MainWindow
{
    private int VerifyLocalizedAppIndex(string directory)
    {
        var checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        string[] samples = ["!Symbols", "123 Tools", "９Tools", "Alpha", "alpha", "Éditeur", "E\u0301diteur", "Öffnen", "Ñotas", "ＺTools",
            "あいう", "カレンダー", "ｶﾒﾗ", "メモ帳", "設定", "電卓", "がくしゅう", "ぱそこん", "가게", "까치", "하나", "中文", "设置", "😀Sample", "Ωmega", "ЯSample"];
        foreach (var language in L.SupportedLanguages)
        {
            var index = WindowsAppIndex.ForLanguage(language);
            var native = new CharacterGroupings(language);
            foreach (var sample in samples.Where(s => s != "😀Sample"))
                Check(index.Section(sample) == native.Lookup(sample), "An app heading disagrees with Windows collation for " + language + ".");
            Check(index.Section("😀Sample") == "&" && index.Section("") == "&" && index.Section("   ") == "&"
                && index.Section("\ud800") == "&", "A blank, malformed or symbol name created a spurious alphabet button.");
            Check(index.IsInitial("é") && index.IsInitial("E\u0301") && index.IsInitial("か") && index.IsInitial("ㄱ")
                && !index.IsInitial("ab") && !index.IsInitial("😀") && !index.IsInitial("\ud800"), "Unicode initial-key recognition is inconsistent.");
            Check(index.Section("  Alpha") == "A" && index.Section("ＺTools") == "Z", "Width or leading whitespace changed an app heading.");
            var declared = native.Where(g => g.Label.Length != 0).Select(g => g.Label).ToArray();
            var alphabet = index.Alphabet(samples);
            Check(alphabet.Where(declared.Contains).SequenceEqual(declared), "The picker changed Windows' native alphabet order for " + language + ".");
            Check(alphabet.Distinct().Count() == alphabet.Length && samples.All(s => alphabet.Contains(index.Section(s))),
                "The alphabet omitted or duplicated a populated mixed-script group.");
            var expected = language switch
            {
                "ja" => new[] { ("カレンダー", "か"), ("メモ帳", "め"), ("設定", "漢字") },
                "ko" => new[] { ("가게", "ㄱ"), ("까치", "ㄲ"), ("하나", "ㅎ") },
                "zh-Hans" => new[] { ("中文", "拼音Z"), ("设置", "拼音S") },
                "zh-Hant" => new[] { ("中文", "4筆"), ("設定", "11筆") },
                "es" => new[] { ("Ñotas", "Ñ"), ("Éditeur", "E") },
                _ => new[] { ("Ñotas", "N"), ("Éditeur", "E"), ("Öffnen", "O") }
            };
            foreach (var (name, label) in expected) Check(index.Section(name) == label, "A documented language-specific heading regressed.");
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                LocalizationResources.Apply(language);
                Document.Preferences.Language = language; Document.Preferences.Theme = theme; ApplyAppearance();
                _apps = samples.Select((name, i) => new AppEntry("index-" + i, name, "", false)).ToList();
                _apps.Add(new("nested-index", "Nested sample", "", false, "Éditeur folder"));
                _expandedAppFolders.Clear();
                Root.Width = 900; Root.Height = 740;
                RenderApps(); ShowAllApps(this, new()); UpdateLayout();
                Check(_appIndex == index && L.Language == language, "The rendered list ignored the selected display language.");
                Check(_sections.Keys.SequenceEqual(_appAlphabet.Where(s => _sections.ContainsKey(s))), "Rendered headings do not follow the native picker order.");
                Check(VisibleAppNames().Length == samples.Length && _appFolderRows.Count == 1, "Localized grouping hid or duplicated apps or directories.");
                if (language is "ja" or "zh-Hant" or "es") SaveScreenshot(Path.Combine(directory, $"app-index-{language}-{theme}.png"));
                OpenAlphabet(); UpdateLayout();
                if (language is "ja" or "zh-Hant" or "es") SaveScreenshot(Path.Combine(directory, $"alphabet-{language}-{theme}-top.png"));
                var buttons = AlphabetButtons.Children.OfType<Button>().ToArray();
                Check(buttons.Select(b => (string)b.Content).SequenceEqual(_appAlphabet), "The picker lost its selected language.");
                Check(buttons.All(b => b.IsEnabled == _sections.ContainsKey((string)b.Content)), "An empty group is enabled or a populated group is disabled.");
                Check(buttons.Any(b => !b.IsEnabled), "The fixture failed to retain empty alphabet slots.");
                foreach (var button in buttons)
                {
                    var text = new System.Windows.Media.FormattedText((string)button.Content, L.Culture, FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface(button.FontFamily, button.FontStyle, button.FontWeight, button.FontStretch),
                        button.FontSize, System.Windows.Media.Brushes.Black, 1);
                    Check(text.WidthIncludingTrailingWhitespace <= button.ActualWidth - button.Padding.Left - button.Padding.Right - 2,
                        "A native alphabet label is clipped inside its picker button.");
                }
                _apps.Add(new("new-index", "Q added sample", "", false)); RenderApps();
                Check(AlphabetButtons.Children.OfType<Button>().Single(b => b.Content is "Q").IsEnabled, "An open picker did not enable a newly installed group's letter.");
                _apps.RemoveAll(a => a.Id == "new-index"); RenderApps();
                Check(!AlphabetButtons.Children.OfType<Button>().Single(b => b.Content is "Q").IsEnabled, "An open picker retained an uninstalled group's enabled letter.");
                buttons = AlphabetButtons.Children.OfType<Button>().ToArray();
                var scroll = VisualChildren<ScrollViewer>(AlphabetOverlay).Single();
                Check(scroll.ExtentWidth <= scroll.ViewportWidth + 1 && scroll.ViewportHeight > 0, "The alphabet grid overflows horizontally or has no usable viewport.");
                if (language is "ja" or "zh-Hant") Check(scroll.ScrollableHeight > 0, "A large alphabet is clipped instead of scrollable.");
                foreach (var button in buttons.Where(b => b.IsEnabled))
                {
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(_sections[(string)button.Content].IsKeyboardFocused, "A localized picker action failed to focus its live heading.");
                }
                var first = AppsPanel.Children.OfType<Button>().First(b => b.Tag is AppEntry);
                first.Focus();
                foreach (var text in new[] { "é", "E\u0301", "Ｚ", "か", "ㄱ", "中" })
                {
                    var section = index.Section(text);
                    var exists = AppsPanel.Children.OfType<Button>().Any(b => b.Tag is AppEntry a && index.Section(a.Name) == section);
                    Check(FocusAppInitial(text), "A localized initial unexpectedly started a search.");
                    if (exists) Check(Keyboard.FocusedElement is Button selected
                        && index.Section(selected.Tag is AppEntry app ? app.Name : AutomationProperties.GetName(selected)) == section,
                        "A localized initial focused a different alphabet group: " + language + "/" + text + ".");
                }
                Check(SearchBox.Text.Length == 0 && !FocusAppInitial("two"), "Initial navigation consumed a search phrase.");
                OpenAlphabet(); UpdateLayout();
                buttons = AlphabetButtons.Children.OfType<Button>().ToArray();
                var last = buttons.Last(b => b.IsEnabled);
                last.BringIntoView(); UpdateLayout();
                var bounds = last.TransformToAncestor(scroll).TransformBounds(new Rect(last.RenderSize));
                Check(bounds.Top >= -1 && bounds.Bottom <= scroll.ActualHeight + 1, "A late alphabet button cannot be reached by scrolling.");
                if (language is "ja" or "zh-Hant" or "es") SaveScreenshot(Path.Combine(directory, $"alphabet-{language}-{theme}.png"));
                AlphabetOverlay.Visibility = Visibility.Collapsed;
                // A full language preview/cancel rebuild must also replace an
                // already open picker; no stale labels or captured row objects.
                OpenAlphabet(); LocalizationResources.Apply(language == "ja" ? "es" : "ja"); RenderApps();
                Check(AlphabetButtons.Children.OfType<Button>().Select(b => (string)b.Content).SequenceEqual(_appAlphabet), "An open picker retained a previous language's labels.");
                AlphabetOverlay.Visibility = Visibility.Collapsed;
            }
        }
        return checks;
    }
}
