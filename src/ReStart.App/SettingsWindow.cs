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
        Style = (Style)FindResource(typeof(Window));
        _store = store;
        _draft = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(main.Document.Preferences))!;
        Title = "ReStart の設定"; Width = 690; Height = 760; MinWidth = 530; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var root = new DockPanel { Margin = new(28, 24, 28, 20) };
        var footer = new StackPanel { Margin = new(0, 12, 0, 0) };
        footer.Children.Add(_feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var close = new Button { Content = "閉じる", MinWidth = 90, IsCancel = true };
        var save = new Button { Content = "保存", MinWidth = 100, Margin = new(12, 0, 0, 0), IsDefault = true };
        save.SetResourceReference(BackgroundProperty, "AccentBrush"); save.Foreground = Brushes.White;
        save.Click += Save;
        buttons.Children.Add(close); buttons.Children.Add(save); footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = "ReStart", FontSize = 30, FontWeight = FontWeights.Light });
        content.Children.Add(new TextBlock { Text = "全画面のスタートを、もう一度。    v0.1.0", Margin = new(0, 6, 0, 20), Foreground = Resource("MutedBrush") });
        Heading(content, "起動と操作");
        Check(content, "Windows キー単押しで ReStart を開く", _draft.CaptureWindowsKey, v => _draft.CaptureWindowsKey = v);
        Note(content, "Win+E / Win+D などのショートカットは Windows に渡します。Ctrl+Alt+Space とトレイからも開けます。");
        Check(content, "タスクバーのスタートボタンも置き換える（試験的）", _draft.CaptureStartButton, v => _draft.CaptureStartButton = v);
        Note(content, "Windows がネイティブの Start ボタンを公開する構成で動作します。Windows 11 の構成によっては利用できません。");
        Check(content, "Windows へのサインイン時に起動する", _draft.StartWithWindows, v => _draft.StartWithWindows = v);
        Check(content, "別のアプリに切り替えたら閉じる", _draft.CloseOnDeactivate, v => _draft.CloseOnDeactivate = v);
        Heading(content, "外観");
        Select(content, "テーマ", new[] { "システムに合わせる", "ダーク", "ライト" }, (int)_draft.Theme, i => _draft.Theme = (ReStart.Core.ThemeMode)i);
        Check(content, "デスクトップの壁紙を背景に表示する", _draft.ShowWallpaper, v => _draft.ShowWallpaper = v);
        Check(content, "旧スタートの色付きタイルを使う", _draft.ColoredTiles, v => _draft.ColoredTiles = v);
        Check(content, "表示時のアニメーション", _draft.Animate, v => _draft.Animate = v);
        Select(content, "グループの幅", new[] { "中タイル 3 列", "中タイル 4 列" }, _draft.Columns == 8 ? 1 : 0, i => _draft.Columns = i == 1 ? 8 : 6);
        Slide(content, "タイルの大きさ", 0.7, 1.5, _draft.TileScale, v => _draft.TileScale = v, true);
        Slide(content, "背景の濃さ", 0.3, 1, _draft.BackgroundOpacity, v => _draft.BackgroundOpacity = v, false);
        var colors = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 8, 0, 12) };
        foreach (var color in new[] { "#0078D7", "#0063B1", "#008272", "#107C10", "#744DA9", "#C239B3", "#D83B01", "#E81123" })
        {
            var button = new Button { Width = 42, Height = 34, Background = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!, Margin = new(0, 0, 8, 0), ToolTip = color, Padding = new(0), HorizontalContentAlignment = HorizontalAlignment.Center, Content = _draft.Accent == color ? "✓" : "", Foreground = Brushes.White };
            System.Windows.Automation.AutomationProperties.SetName(button, "アクセント " + color);
            button.Click += (_, _) => { _draft.Accent = color; foreach (Button b in colors.Children) b.Content = ""; button.Content = "✓"; };
            colors.Children.Add(button);
        }
        content.Children.Add(new TextBlock { Text = "アクセントカラー", Margin = new(0, 8, 0, 0) }); content.Children.Add(colors);
        Heading(content, "レイアウトとデータ");
        Note(content, "並べ替え・サイズ・フォルダーは自動保存されます。タイルを Shift を押しながら重ねるとフォルダーになります。");
        var dataButtons = new WrapPanel { Margin = new(0, 10, 0, 4) };
        DataButton(dataButtons, "エクスポート", Export);
        DataButton(dataButtons, "インポート", Import);
        DataButton(dataButtons, "初期レイアウトに戻す", Reset);
        content.Children.Add(dataButtons);
        Note(content, "保存先: " + _store.FilePath);
        Note(content, "通信・広告・利用状況の送信はありません。インストール済みアプリとショートカットはこの PC だけで読み取ります。");
        Heading(content, "再現範囲");
        Note(content, "Windows 10 の全画面スタートを参考に独立実装しています。旧 UWP のライブ通知、Cortana、Windows Search 全体、Explorer 内部の置き換えは含まれません。時計・日付タイルはローカルで更新します。");
        var links = new WrapPanel { Margin = new(0, 10, 0, 0) };
        DataButton(links, "GitHub / README", () => ShellLauncher.Launch("https://github.com/LastResolution/ReStart"));
        DataButton(links, "Windows の設定", () => ShellLauncher.Launch("ms-settings:"));
        DataButton(links, "ReStart を終了", () => { DialogResult = false; ((App)Application.Current).ExitApplication(); });
        content.Children.Add(links);
        root.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(0, 0, 16, 0) });
        Content = root;
        Closed += (_, _) => { if (_dataChanged) _main.RefreshLayout(); };
    }

    private static System.Windows.Media.Brush Resource(string key) => (System.Windows.Media.Brush)Application.Current.FindResource(key);
    private static void Heading(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontSize = 18, Margin = new(0, 22, 0, 8) });
    private static void Note(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Resource("MutedBrush"), Margin = new(0, 3, 0, 8), LineHeight = 20 });
    private static void Check(Panel panel, string label, bool initial, Action<bool> changed)
    {
        var checkbox = new CheckBox { Content = label, IsChecked = initial };
        checkbox.Checked += (_, _) => changed(true); checkbox.Unchecked += (_, _) => changed(false); panel.Children.Add(checkbox);
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
        try
        {
            _main.Document.Preferences = _draft;
            _store.Save(_main.Document);
            if (previous.StartWithWindows != _draft.StartWithWindows) StartupRegistration.SetEnabled(_draft.StartWithWindows);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _main.Document.Preferences = previous;
            try { _store.Save(_main.Document); } catch (IOException) { }
            _feedback.Text = "保存できませんでした: " + ex.Message;
        }
    }
    private void Export()
    {
        var dialog = new SaveFileDialog { Title = "レイアウトをエクスポート", FileName = "ReStart-layout.json", Filter = "JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_main.Document, LayoutStore.JsonOptions)); _feedback.Text = "エクスポートしました。起動先のパスなどが含まれます。"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _feedback.Text = ex.Message; }
    }
    private void Import()
    {
        var dialog = new OpenFileDialog { Title = "レイアウトをインポート", Filter = "JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var imported = LayoutStore.Read(dialog.FileName);
            if (MessageBox.Show(this, "現在のピン留めを置き換えますか？ インポート元のタイルには起動先のパスが含まれます。信頼できるファイルを使用してください。", "ReStart", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            // Preferences are local to this PC; importing a layout must not enable hooks or autostart.
            imported.Preferences = _main.Document.Preferences;
            _store.Save(imported); _main.ReplaceDocument(imported); _dataChanged = true; _feedback.Text = "レイアウトをインポートしました。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or NullReferenceException) { _feedback.Text = "読み込めませんでした: " + ex.Message; }
    }
    private void Reset()
    {
        if (MessageBox.Show(this, "現在のピン留めを初期状態に戻しますか？", "ReStart", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var document = _main.CreateInitialLayout(); document.Preferences = _main.Document.Preferences;
        _main.ReplaceDocument(document); _dataChanged = true; _feedback.Text = "初期レイアウトに戻しました。";
    }
}
