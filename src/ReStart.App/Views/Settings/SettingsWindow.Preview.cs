using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

internal sealed partial class SettingsWindow
{
    private string _baselineDraft;
    private readonly DispatcherTimer _previewTimer;
    private bool _buildingContent, _allowClose, _askingToClose;
    private TextKey _selectedSection = K.SettingsStartup;
    private ComboBox _languageSelect = null!;
    private readonly List<Action> _contentCleanup = [];
    private readonly HashSet<string> _importedBackgrounds = [];
    internal bool HasPendingChanges => JsonSerializer.Serialize(_draft) != _baselineDraft ||
        _backgroundColor.Text.Trim().Length > 0 && !ThemePresets.IsColor(_backgroundColor.Text.Trim()) || !ThemePresets.IsColor(_accentColor.Text.Trim());

    private void DraftChanged()
    {
        if (_buildingContent || _allowClose || !IsLoaded) return;
        _previewTimer.Stop(); _previewTimer.Start();
    }

    /// <summary>Flushes visual draft changes; only the display overlay changes, never startup registration or the saved document.</summary>
    internal void ApplyDraftPreview()
    {
        _previewTimer.Stop(); if (_allowClose) return;
        var language = L.Language; var focusLanguage = _languageSelect.IsKeyboardFocusWithin;
        var rawBackground = _backgroundColor.Text; var rawAccent = _accentColor.Text;
        _main.PreviewPreferences(this, _draft);
        if (language == L.Language) return;
        BuildContent();
        // Invalid in-progress RGB input is still an unsaved edit, even across a language rebuild.
        if (rawBackground.Length > 0 && !ThemePresets.IsColor(rawBackground)) _backgroundColor.Text = rawBackground;
        if (!ThemePresets.IsColor(rawAccent)) _accentColor.Text = rawAccent;
        if (focusLanguage) _languageSelect.Focus();
    }

    private void DisposeContent()
    {
        foreach (var dispose in _contentCleanup) dispose(); _contentCleanup.Clear();
    }

    private void OnClosingSettings(object? sender, CancelEventArgs e)
    {
        _previewTimer.Stop();
        if (_allowClose || !HasPendingChanges) return;
        if (_askingToClose) { e.Cancel = true; return; }
        ApplyDraftPreview();
        _askingToClose = true;
        try
        {
            var choice = UnsavedSettingsWindow.Ask(this);
            e.Cancel = choice == SettingsCloseChoice.KeepEditing || choice == SettingsCloseChoice.Save && !SaveDraft();
        }
        finally { _askingToClose = false; }
    }

    /// <summary>Resolves draft changes before an external action; cancellation leaves the editor open and unchanged.</summary>
    /// <returns>True after an explicit save/discard, or when no draft changes exist.</returns>
    private bool ResolvePendingChanges()
    {
        if (!HasPendingChanges) return true;
        switch (UnsavedSettingsWindow.Ask(this))
        {
            case SettingsCloseChoice.Save: return SaveDraft();
            case SettingsCloseChoice.Discard:
                _draft = JsonSerializer.Deserialize<Preferences>(_baselineDraft)!;
                _main.EndPreferencesPreview(this); BuildContent(); return true;
            default: return false;
        }
    }

    /// <summary>Closes after an already resolved decision, also allowing isolated fixture teardown without opening a second prompt.</summary>
    internal void DiscardAndClose() { _allowClose = true; Close(); }

    private void RequestExit()
    {
        if (!ResolvePendingChanges()) return;
        DiscardAndClose(); ((App)Application.Current).ExitApplication();
    }

    private void CleanupBackgroundImports()
    {
        var store = new BackgroundImageStore(Path.GetDirectoryName(_store.FilePath)!);
        foreach (var id in _importedBackgrounds.Where(id => id != _main.Document.Preferences.CustomBackgroundId))
            try { var path = store.Resolve(id); if (path != null) File.Delete(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
