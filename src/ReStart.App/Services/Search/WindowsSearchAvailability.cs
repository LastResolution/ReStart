using Microsoft.Win32;

namespace ReStart.App.Services;

/// <summary>Checks only the fixed Windows Search protocol registration; it never enumerates accounts or launches another application.</summary>
internal static class WindowsSearchAvailability
{
    internal static bool IsRegistered()
    {
        try { using var key = Registry.ClassesRoot.OpenSubKey("ms-search", false); return key != null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return false; }
    }
}
