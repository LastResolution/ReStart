using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

/// <summary>Edits explicit, local-only connections without putting URLs or credentials in package manifests or layouts.</summary>
internal sealed class TileDataWindow : Window
{
    internal sealed record Approval(string Path, string TileId, string PackageId);
    private sealed record Editor(string Id, TileDataDefinition Definition, TileDataBinding Previous, CheckBox Enabled,
        ComboBox Access, TextBox Location, TextBox Pointer, TextBox Header, PasswordBox Secret, CheckBox Clear);
    private readonly List<Editor> _editors = [];
    private readonly string _path;
    private readonly Approval? _approval;
    private readonly ScriptTileManifest _manifest;
    private readonly TextBlock _feedback = new() { TextWrapping = TextWrapping.Wrap };
    internal Button SaveButton { get; }
    internal IReadOnlyList<TextBox> LocationInputs => _editors.Select(e => e.Location).ToArray();
    internal TileDataWindow(Window owner, ScriptTileManifest manifest, string path, Approval? approval = null, string? legacyPath = null)
    {
        _path = path; _approval = approval; _manifest = manifest; Owner = owner; ShowInTaskbar = false; Title = L.T(K.ConfigureTileData);
        Width = 620; Height = 730; MinWidth = 460; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Style = (Style)Application.Current.FindResource(typeof(Window)); ThemeWindow.Attach(this);
        var root = new DockPanel { Margin = new(24) };
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom);
        footer.Children.Add(_feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 14, 0, 0) };
        buttons.Children.Add(new Button { Content = L.T(K.CommonCancel), MinWidth = 96, IsCancel = true });
        SaveButton = new Button { Content = L.T(approval == null ? K.CommonSave : K.TileDataApprove), MinWidth = 96, IsDefault = true, Margin = new(8, 0, 0, 0) };
        SaveButton.SetResourceReference(BackgroundProperty, "AccentBrush"); SaveButton.Click += (_, _) => Save();
        SaveButton.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        buttons.Children.Add(SaveButton); footer.Children.Add(buttons); root.Children.Add(footer);
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = L.T(K.TileDataHelp), TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 18) });
        if (approval != null) body.Children.Add(new TextBlock { Text = L.T(K.TilePermissionHelp), TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 18) });
        Dictionary<string, TileDataBinding> saved;
        try { saved = ScriptDataBindings.Read(!File.Exists(path) && legacyPath != null ? legacyPath : path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or CryptographicException or System.Text.Json.JsonException)
        { saved = []; _feedback.Text = L.T(K.TileDataInvalid); }
        foreach (var (id, definition) in manifest.DataSources)
        {
            var previous = saved.GetValueOrDefault(id) ?? new TileDataBinding { Enabled = true, Access = definition.SuggestedAccess, Location = definition.SuggestedUrl };
            var section = new StackPanel { Margin = new(0, 0, 0, 24) };
            section.Children.Add(new TextBlock { Text = id, FontSize = 19, Margin = new(0, 0, 0, 8) });
            var enabled = new CheckBox { Content = L.T(K.TileDataEnable), IsChecked = previous.Enabled }; section.Children.Add(enabled);
            Label(section, K.TileDataAccess);
            var access = new ComboBox { ItemsSource = new[] { L.T(K.TileDataWeb), L.T(K.TileDataFile) }, SelectedIndex = previous.Access == "file" ? 1 : 0 };
            section.Children.Add(access);
            var location = Input(section, K.TileDataLocation, previous.Location, 4096);
            var choose = new Button { Content = L.T(K.TileDataChooseFile), HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 8, 0, 0) };
            choose.Click += (_, _) =>
            {
                var dialog = new OpenFileDialog { Filter = "JSON / XML / RSS|*.json;*.xml;*.rss;*.atom|*.*|*.*" };
                if (dialog.ShowDialog(this) == true) { location.Text = dialog.FileName; access.SelectedIndex = 1; }
            };
            section.Children.Add(choose);
            var pointer = Input(section, K.TileDataSelector, previous.JsonPointer ?? definition.JsonPointer, 256);
            pointer.IsEnabled = definition.Format == "json";
            var header = Input(section, K.TileDataHeader, previous.HeaderName, 64);
            Label(section, K.TileDataSecret);
            var secret = new PasswordBox { MaxLength = 4096 };
            AutomationProperties.SetName(secret, L.T(K.TileDataSecret)); section.Children.Add(secret);
            section.Children.Add(new TextBlock { Text = L.T(K.TileDataSecretHint), TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new(0, 5, 0, 4) });
            var clear = new CheckBox { Content = L.T(K.TileDataClearSecret) }; section.Children.Add(clear);
            void AccessChanged() { header.IsEnabled = secret.IsEnabled = clear.IsEnabled = access.SelectedIndex == 0; choose.Visibility = access.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; }
            access.SelectionChanged += (_, _) => AccessChanged(); AccessChanged();
            _editors.Add(new(id, definition, previous, enabled, access, location, pointer, header, secret, clear));
            body.Children.Add(section);
        }
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = body });
        Content = root;
    }
    private static void Label(Panel parent, K key) => parent.Children.Add(new TextBlock { Text = L.T(key), TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 5) });
    private static TextBox Input(Panel parent, K key, string value, int limit)
    {
        Label(parent, key); var input = new TextBox { Text = value, MaxLength = limit };
        AutomationProperties.SetName(input, L.T(key)); parent.Children.Add(input); return input;
    }
    private void Save()
    {
        var bindings = new Dictionary<string, TileDataBinding>();
        foreach (var editor in _editors)
        {
            var location = editor.Location.Text.Trim();
            var sameOrigin = Uri.TryCreate(location, UriKind.Absolute, out var next) && Uri.TryCreate(editor.Previous.Location, UriKind.Absolute, out var previous)
                && next.GetLeftPart(UriPartial.Authority) == previous.GetLeftPart(UriPartial.Authority);
            var secret = editor.Clear.IsChecked == true ? "" : editor.Secret.Password.Length > 0 ? editor.Secret.Password : sameOrigin ? editor.Previous.Secret : "";
            var binding = new TileDataBinding
            {
                Enabled = editor.Enabled.IsChecked == true,
                Access = editor.Access.SelectedIndex == 1 ? "file" : "http",
                Location = location,
                JsonPointer = editor.Pointer.Text.Trim(),
                HeaderName = editor.Header.Text.Trim(),
                Secret = editor.Access.SelectedIndex == 1 ? "" : secret
            };
            if (!binding.IsValid()) { _feedback.Text = L.T(K.TileDataInvalid); editor.Location.Focus(); return; }
            bindings[editor.Id] = binding;
        }
        try
        {
            ScriptDataBindings.Save(_path, bindings);
            if (_approval != null) ScriptTileGrants.ApproveData(_approval.Path, _approval.TileId, _approval.PackageId, _manifest, bindings);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or CryptographicException)
        { _feedback.Text = L.T(K.TileDataInvalid); }
    }
}
