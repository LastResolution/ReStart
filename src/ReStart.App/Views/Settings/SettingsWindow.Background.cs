using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ReStart.App.Services;

namespace ReStart.App;

internal sealed partial class SettingsWindow
{
    private ComboBox _backgroundSource = null!;
    private void BuildBackgroundImageSettings(Panel content)
    {
        var store = new BackgroundImageStore(Path.GetDirectoryName(_store.FilePath)!);
        var preview = new Image { Height = 120, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 8, 0, 8) };
        void Refresh()
        {
            try
            {
                var path = store.Resolve(_draft.CustomBackgroundId);
                preview.Source = _draft.UseCustomBackground && _draft.ShowWallpaper && File.Exists(path) ? DesktopWallpaperSource.Load(path!) : null;
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException) { preview.Source = null; }
            preview.Visibility = preview.Source == null ? Visibility.Collapsed : Visibility.Visible;
        }
        _backgroundSource = Select(content, L.T(K.BackgroundSource), [L.T(K.BackgroundNone), L.T(K.BackgroundDesktop), L.T(K.BackgroundCustom)],
            !_draft.ShowWallpaper ? 0 : _draft.UseCustomBackground ? 2 : 1, index =>
            { _draft.ShowWallpaper = index != 0; _draft.UseCustomBackground = index == 2; Refresh(); });
        DataButton(content, L.T(K.BackgroundChoose), () =>
        {
            var dialog = new OpenFileDialog { Title = L.T(K.BackgroundChoose), Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                _draft.CustomBackgroundId = store.Import(dialog.FileName);
                _draft.ShowWallpaper = _draft.UseCustomBackground = true;
                _backgroundSource.SelectedIndex = 2; Refresh(); _feedback.Text = L.T(K.BackgroundSelected);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.IO.FileFormatException)
            { _feedback.Text = L.T(K.BackgroundInvalid); }
        });
        content.Children.Add(preview); Note(content, L.T(K.BackgroundHelp)); Refresh();
    }

    private bool ValidateBackgroundImage()
    {
        if (!_draft.ShowWallpaper || !_draft.UseCustomBackground || File.Exists(new BackgroundImageStore(Path.GetDirectoryName(_store.FilePath)!).Resolve(_draft.CustomBackgroundId))) return true;
        SelectSection(L.T(K.SettingsAppearance)); _feedback.Text = L.T(K.BackgroundInvalid); _backgroundSource.Focus(); return false;
    }
}
