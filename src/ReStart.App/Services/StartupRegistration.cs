using Microsoft.Win32;

namespace ReStart.App.Services;

internal static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("ReStart", $"\"{Environment.ProcessPath}\" --background");
        else key.DeleteValue("ReStart", false);
    }
}
