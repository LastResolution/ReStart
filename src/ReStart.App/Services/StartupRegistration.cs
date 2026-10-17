using System.Runtime.Versioning;
using Microsoft.Win32;
using ReStart.Core;

namespace ReStart.App.Services;

[SupportedOSPlatform("windows")]
internal sealed class StartupRegistration(string subKey = @"Software\Microsoft\Windows\CurrentVersion\Run", string valueName = "ReStart")
{
    public static StartupRegistration Current { get; } = new();
    internal sealed record Entry(object? Value, RegistryValueKind Kind);
    public Entry Capture()
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey);
        var value = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return new(value, value == null ? RegistryValueKind.String : key!.GetValueKind(valueName));
    }
    public bool IsEnabled => Capture().Value is string command && !string.IsNullOrWhiteSpace(command);
    public string? Command => Capture().Value as string;
    public static string BuildCommand(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"') || executable.Contains('\r') || executable.Contains('\n') ||
            !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(TextCatalog.T("自動起動には配布用の ReStart.exe を使用してください。"));
        var command = $"\"{executable}\" --background";
        if (command.Length > 260) throw new InvalidOperationException(TextCatalog.T("自動起動に登録するパスが長すぎます。短い場所に ReStart を移動してください。"));
        return command;
    }
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
