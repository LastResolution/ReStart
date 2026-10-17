using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

internal sealed class SettingsWindow : Window
{
    private readonly MainWindow _main;
    private readonly LayoutStore _store;
    private readonly Preferences _draft;
    private readonly TextBlock _feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 0) };
    private bool _dataChanged;

    public SettingsWindow(MainWindow main, LayoutStore store)
    {
        _main = main;
        ThemeWindow.Attach(this);
        Style = (Style)FindResource(typeof(Window));
        _store = store;
        _draft = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(main.Document.Preferences))!;
        if (!main.IsPreview) _draft.StartWithWindows = StartupRegistration.Current.IsEnabled;
        Title = L.T("ReStart の設定"); Width = 940; Height = 740; MinWidth = 760; MinHeight = 500;
        WindowStartupLocation = main.IsPreview ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner; ShowInTaskbar = main.IsPreview;
        var root = new DockPanel { Margin = new(28, 24, 28, 20) };
        var footer = new StackPanel { Margin = new(0, 12, 0, 0) };
        footer.Children.Add(_feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var close = new Button { Content = L.T("閉じる"), MinWidth = 90, IsCancel = true };
        var save = new Button { Content = L.T("保存"), MinWidth = 100, Margin = new(12, 0, 0, 0), IsDefault = true };
        save.SetResourceReference(BackgroundProperty, "AccentBrush"); save.Foreground = Brushes.White;
        save.Click += Save;
        buttons.Children.Add(close); buttons.Children.Add(save); footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var title = new TextBlock { Text = Title, FontSize = 26, Margin = new(0, 0, 0, 24) };
        DockPanel.SetDock(title, Dock.Top); root.Children.Add(title);
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new GridLength(180) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var navigation = new StackPanel { Margin = new(0, 0, 24, 0) }; body.Children.Add(navigation);
        var view = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(0, 0, 16, 0) };
        Grid.SetColumn(view, 1); body.Children.Add(view);
        var navigationButtons = new List<Button>();
        StackPanel Section(string name)
        {
            var section = new StackPanel(); Heading(section, name);
            var button = new Button { Content = name, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new(0, 0, 0, 4), Padding = new(12, 12, 8, 12) };
            button.Click += (_, _) => { view.Content = section; view.ScrollToTop(); foreach (var b in navigationButtons) b.Background = Brushes.Transparent; button.Background = Resource("TileBrush"); };
            navigation.Children.Add(button); navigationButtons.Add(button);
            if (navigationButtons.Count == 1) { view.Content = section; button.Background = Resource("TileBrush"); }
            return section;
        }
        var content = Section(L.T("起動と操作"));
        Check(content, L.T("Windows キー単押しで ReStart を開く"), _draft.CaptureWindowsKey, v => _draft.CaptureWindowsKey = v);
        Note(content, L.T("Win+E / Win+D などのショートカットは Windows に渡します。Ctrl+Alt+Space とトレイからも開けます。"));
        Check(content, L.T("タスクバーのスタートボタンも置き換える（試験的）"), _draft.CaptureStartButton, v => _draft.CaptureStartButton = v);
        Note(content, L.T("Windows がネイティブの Start ボタンを公開する構成で動作します。Windows 11 の構成によっては利用できません。"));
        var autostart = Check(content, L.T("Windows へのサインイン時に起動する"), _draft.StartWithWindows, v => _draft.StartWithWindows = v);
        autostart.IsEnabled = !main.IsPreview;
        Note(content, L.T(main.IsPreview ? "プレビューでは自動起動の登録を変更しません。" : "サインイン時は通知領域に常駐します。画面はキー操作で開きます。"));
        if (!main.IsPreview && StartupRegistration.Current.Command is string command) Note(content, L.T("登録済みの起動コマンド: ") + command);
        Check(content, L.T("別のアプリに切り替えたら閉じる"), _draft.CloseOnDeactivate, v => _draft.CloseOnDeactivate = v);
        content = Section(L.T("外観"));
        Select(content, L.T("テーマ"), new[] { L.T("システムに合わせる"), L.T("ダーク"), L.T("ライト") }, (int)_draft.Theme, i => _draft.Theme = (ReStart.Core.ThemeMode)i);
        Check(content, L.T("デスクトップの壁紙を背景に表示する"), _draft.ShowWallpaper, v => _draft.ShowWallpaper = v);
        Check(content, L.T("旧スタートの色付きタイルを使う"), _draft.ColoredTiles, v => _draft.ColoredTiles = v);
        Check(content, L.T("表示時のアニメーション"), _draft.Animate, v => _draft.Animate = v);
        Select(content, L.T("グループの幅"), new[] { L.T("中タイル 3 列"), L.T("中タイル 4 列") }, _draft.Columns == 8 ? 1 : 0, i => _draft.Columns = i == 1 ? 8 : 6);
        Slide(content, L.T("タイルの大きさ"), 0.7, 1.5, _draft.TileScale, v => _draft.TileScale = v, true);
        Slide(content, L.T("背景の濃さ"), 0.3, 1, _draft.BackgroundOpacity, v => _draft.BackgroundOpacity = v, false);
        var colors = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 8, 0, 12) };
        foreach (var color in new[] { "#0078D7", "#0063B1", "#008272", "#107C10", "#744DA9", "#C239B3", "#D83B01", "#E81123" })
        {
            var button = new Button { Width = 42, Height = 34, Background = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!, Margin = new(0, 0, 8, 0), ToolTip = color, Padding = new(0), HorizontalContentAlignment = HorizontalAlignment.Center, Content = _draft.Accent == color ? "✓" : "", Foreground = Brushes.White };
            System.Windows.Automation.AutomationProperties.SetName(button, L.T("アクセント ") + color);
            button.Click += (_, _) => { _draft.Accent = color; foreach (Button b in colors.Children) b.Content = ""; button.Content = "✓"; };
            colors.Children.Add(button);
        }
        content.Children.Add(new TextBlock { Text = L.T("アクセントカラー"), Margin = new(0, 8, 0, 0) }); content.Children.Add(colors);
        content = Section(L.T("言語"));
        Select(content, L.T("表示言語"), new[] { L.T("システムに合わせる"), L.T("日本語"), "English" }, _draft.Language == "ja" ? 1 : _draft.Language == "en" ? 2 : 0, i => _draft.Language = i == 1 ? "ja" : i == 2 ? "en" : "system");
        Note(content, L.T("保存すると表示言語が切り替わります。アプリ名や自分で付けた名前は変更しません。"));
        content = Section(L.T("レイアウトとデータ"));
        Note(content, L.T("並べ替え・サイズ・フォルダーは自動保存されます。タイルを Shift を押しながら重ねるとフォルダーになります。"));
        var dataButtons = new WrapPanel { Margin = new(0, 10, 0, 4) };
        DataButton(dataButtons, L.T("エクスポート"), Export);
        DataButton(dataButtons, L.T("インポート"), Import);
        DataButton(dataButtons, L.T("初期レイアウトに戻す"), Reset);
        content.Children.Add(dataButtons);
        Note(content, L.T("保存先: ") + _store.FilePath);
        Note(content, L.T("通信・広告・利用状況の送信はありません。インストール済みアプリとショートカットはこの PC だけで読み取ります。"));
        content = Section(L.T("情報"));
        Note(content, "ReStart v" + typeof(App).Assembly.GetName().Version?.ToString(3));
        Heading(content, L.T("再現範囲"));
        Note(content, L.T("Windows 10 の全画面スタートを参考に独立実装しています。旧 UWP のライブ通知、Cortana、Windows Search 全体、Explorer 内部の置き換えは含まれません。時計・日付タイルはローカルで更新します。"));
        var links = new WrapPanel { Margin = new(0, 10, 0, 0) };
        DataButton(links, "GitHub / README", () => ShellLauncher.Launch("https://github.com/LastResolution/ReStart"));
        DataButton(links, L.T("Windows の設定"), () => ShellLauncher.Launch("ms-settings:"));
        DataButton(links, L.T("ReStart を終了"), () => { DialogResult = false; ((App)Application.Current).ExitApplication(); });
        content.Children.Add(links);
        root.Children.Add(body);
        Content = root;
        Closed += (_, _) => { if (_dataChanged) _main.RefreshLayout(); };
    }

    private static System.Windows.Media.Brush Resource(string key) => (System.Windows.Media.Brush)Application.Current.FindResource(key);
    private static void Heading(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontSize = 20, Margin = new(0, 0, 0, 16) });
    private static void Note(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Resource("MutedBrush"), Margin = new(0, 3, 0, 8), LineHeight = 20 });
    private static CheckBox Check(Panel panel, string label, bool initial, Action<bool> changed)
    {
        var checkbox = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = initial };
        System.Windows.Automation.AutomationProperties.SetName(checkbox, label);
        checkbox.Checked += (_, _) => changed(true); checkbox.Unchecked += (_, _) => changed(false); panel.Children.Add(checkbox); return checkbox;
    }
    private static void Select(Panel panel, string label, string[] options, int index, Action<int> changed)
    {
        var row = new DockPanel { Margin = new(0, 10, 0, 7) };
        row.Children.Add(new TextBlock { Text = label, Width = 170, VerticalAlignment = VerticalAlignment.Center });
        var select = new ComboBox { ItemsSource = options, SelectedIndex = index, MinWidth = 180, HorizontalAlignment = HorizontalAlignment.Left, Padding = new(6) };
        select.SelectionChanged += (_, _) => changed(select.SelectedIndex); row.Children.Add(select); panel.Children.Add(row);
    }
    private static void Slide(Panel panel, string label, double min, double max, double value, Action<double> changed, bool snap)
    {
        var row = new DockPanel { Margin = new(0, 12, 0, 7) };
        row.Children.Add(new TextBlock { Text = label, Width = 170 });
        var percent = new TextBlock { Text = $"{value:P0}", Width = 48, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(percent, Dock.Right); row.Children.Add(percent);
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = 0.05, IsSnapToTickEnabled = snap, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) };
        System.Windows.Automation.AutomationProperties.SetName(slider, label);
        slider.ValueChanged += (_, _) => { changed(slider.Value); percent.Text = $"{slider.Value:P0}"; }; row.Children.Add(slider); panel.Children.Add(row);
    }
    private static void DataButton(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Margin = new(0, 0, 8, 6), Background = Resource("TileBrush") };
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        var previous = _main.Document.Preferences;
        StartupRegistration.Entry? registration = null;
        try
        {
            if (!_main.IsPreview)
            {
                registration = StartupRegistration.Current.Capture();
                StartupRegistration.Current.SetEnabled(_draft.StartWithWindows);
            }
            _main.Document.Preferences = _draft;
            _store.Save(_main.Document);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            _main.Document.Preferences = previous;
            if (registration != null)
            {
                try { StartupRegistration.Current.Restore(registration); }
                catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException or System.Security.SecurityException) { _feedback.Text = L.T("自動起動の登録を戻せませんでした: ") + rollback.Message; return; }
            }
            _feedback.Text = L.T("保存できませんでした: ") + ex.Message;
        }
    }
    private void Export()
    {
        var dialog = new SaveFileDialog { Title = L.T("レイアウトをエクスポート"), FileName = "ReStart-layout.json", Filter = "JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_main.Document, LayoutStore.JsonOptions)); _feedback.Text = L.T("エクスポートしました。起動先のパスなどが含まれます。"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _feedback.Text = ex.Message; }
    }
    private void Import()
    {
        var dialog = new OpenFileDialog { Title = L.T("レイアウトをインポート"), Filter = "JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var imported = LayoutStore.Read(dialog.FileName);
            if (MessageBox.Show(this, L.T("現在のピン留めを置き換えますか？ インポート元のタイルには起動先のパスが含まれます。信頼できるファイルを使用してください。"), "ReStart", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            // Preferences are local to this PC; importing a layout must not enable hooks or autostart.
            imported.Preferences = _main.Document.Preferences;
            _store.Save(imported); _main.ReplaceDocument(imported); _dataChanged = true; _feedback.Text = L.T("レイアウトをインポートしました。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or NullReferenceException) { _feedback.Text = L.T("読み込めませんでした: ") + ex.Message; }
    }
    private void Reset()
    {
        if (MessageBox.Show(this, L.T("現在のピン留めを初期状態に戻しますか？"), "ReStart", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var document = _main.CreateInitialLayout(); document.Preferences = _main.Document.Preferences;
        _main.ReplaceDocument(document); _dataChanged = true; _feedback.Text = L.T("初期レイアウトに戻しました。");
    }
}
