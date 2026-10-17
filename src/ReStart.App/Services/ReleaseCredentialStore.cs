using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ReStart.App.Services;

/// <summary>Stores a release-read token in the current user's Windows Credential Manager, separate from layouts and exports.</summary>
[SupportedOSPlatform("windows")]
internal sealed class ReleaseCredentialStore(string target)
{
    internal static ReleaseCredentialStore Current { get; } = new("ReStart/GitHub/LastResolution/ReStart");
    internal string? Read()
    {
        if (!CredRead(target, 1, 0, out var value))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168) return null;
            throw new Win32Exception(error);
        }
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(value);
            if (credential.BlobSize is 0 or > 2048) return null;
            var bytes = new byte[credential.BlobSize];
            try { Marshal.Copy(credential.Blob, bytes, 0, bytes.Length); return Encoding.UTF8.GetString(bytes); }
            finally { Array.Clear(bytes); }
        }
        finally { CredFree(value); }
    }
    internal void Save(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace) || Encoding.UTF8.GetByteCount(token) > 2048)
            throw new ArgumentException("Invalid credential value.", nameof(token));
        var bytes = Encoding.UTF8.GetBytes(token);
        var buffer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            var value = new Credential { Type = 1, TargetName = target, UserName = "GitHub", BlobSize = (uint)bytes.Length, Blob = buffer, Persist = 2 };
            if (!CredWrite(ref value, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.Copy(new byte[bytes.Length], 0, buffer, bytes.Length); Marshal.FreeHGlobal(buffer); Array.Clear(bytes); }
    }
    internal void Delete()
    {
        if (!CredDelete(target, 1, 0) && Marshal.GetLastWin32Error() != 1168) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags, Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        public nint Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public nint Blob;
        public uint Persist, AttributeCount;
        public nint Attributes, TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string target, uint type, uint flags, out nint value);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential value, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(nint value);
}
