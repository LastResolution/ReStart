using System.Runtime.Versioning;
using Microsoft.Win32;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Reads and updates one current-user Run value; alternate keys are injected only by isolated tests.</summary>
[SupportedOSPlatform("windows")]
internal sealed class StartupRegistration(string subKey = @"Software\Microsoft\Windows\CurrentVersion\Run", string valueName = "ReStart")
{
    public static StartupRegistration Current { get; } = new();
    internal sealed record Entry(object? Value, RegistryValueKind Kind);
    /// <summary>Captures the exact value and kind so a failed settings transaction can restore the previous registration.</summary>
    public Entry Capture()
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey);
        var value = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return new(value, value == null ? RegistryValueKind.String : key!.GetValueKind(valueName));
    }
    public bool IsEnabled => Capture().Value is string command && !string.IsNullOrWhiteSpace(command);
    public string? Command => Capture().Value as string;
    /// <summary>Builds a quoted executable command within the documented 260-character Run limit.</summary>
    public static string BuildCommand(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"') || executable.Contains('\r') || executable.Contains('\n') ||
            !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(TextCatalog.T(K.ErrorStartupExecutable));
        var command = $"\"{executable}\" --background";
        if (command.Length > 260) throw new InvalidOperationException(TextCatalog.T(K.ErrorStartupPathLength));
        return command;
    }
    /// <summary>Registers the current portable executable or removes only this app registration.</summary>
    public void SetEnabled(bool enabled, string? executable = null)
    {
        var command = enabled ? BuildCommand(executable ?? Environment.ProcessPath!) : null;
        Restore(new(command, RegistryValueKind.String));
    }
    public void Restore(Entry entry)
    {
        if (entry.Value == null)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(subKey, true);
            existing?.DeleteValue(valueName, false);
        }
        else
        {
            using var key = Registry.CurrentUser.CreateSubKey(subKey);
            key.SetValue(valueName, entry.Value, entry.Kind);
        }
    }
}
