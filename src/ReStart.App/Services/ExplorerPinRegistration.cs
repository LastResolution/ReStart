using Microsoft.Win32;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Manages one reversible per-user Explorer verb for files, directories and shortcuts; never changes associations or loads code into Explorer.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal sealed class ExplorerPinRegistration(string classesRoot = @"Software\Classes")
{
    internal static ExplorerPinRegistration Current { get; } = new();
    private string KeyPath => classesRoot + @"\AllFilesystemObjects\shell\ReStart.Pin";

    /// <summary>Gets whether this registration exists in the current-user classes hive.</summary>
    internal bool IsEnabled { get { using var key = Registry.CurrentUser.OpenSubKey(KeyPath + @"\command"); return key?.GetValue("") is string; } }

    /// <summary>Registers the actual executable path with separately quoted shell substitution and single-selection semantics.</summary>
    internal void SetEnabled(bool enabled, string? executable = null)
    {
        if (!enabled) { Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false); return; }
        executable ??= Environment.ProcessPath!;
        _ = StartupRegistration.BuildCommand(executable);
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue("", TextCatalog.T(TextKey.PinToReStart));
        key.SetValue("Icon", $"\"{executable}\",0");
        key.SetValue("MultiSelectModel", "Single");
        using var command = key.CreateSubKey("command");
        command.SetValue("", $"\"{executable}\" --pin \"%1\"");
    }

    /// <summary>Refreshes an already enabled verb when the portable app moves or the UI language changes.</summary>
    internal void RefreshIfEnabled() { if (IsEnabled) SetEnabled(true); }
}
