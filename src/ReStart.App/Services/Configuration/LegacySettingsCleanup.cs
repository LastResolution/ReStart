using System.Runtime.InteropServices;

namespace ReStart.App.Services;

/// <summary>Removes only the retired private-update credential, without reading its contents.</summary>
internal static class LegacySettingsCleanup
{
    internal static void Run() => CredDelete("ReStart/GitHub/LastResolution/ReStart", 1, 0);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
}
