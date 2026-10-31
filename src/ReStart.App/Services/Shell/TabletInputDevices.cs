using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Reads current posture and active keyboard interfaces without reading device names, instance IDs or registry history.</summary>
internal static class TabletInputDevices
{
    internal readonly record struct KeyboardObservation(bool Active, bool UsbOrBluetooth);
    private static readonly Guid KeyboardInterface = new("884b96c3-56ef-11d1-bc8c-00a0c91405dd");
    private static readonly Guid UsbBus = new("9d7debbc-c85d-11d1-9eb4-006008c3a19a");
    private static readonly PropertyKey BusType = new(new("a45c254e-df1c-4efd-8020-67d146a850e0"), 21);
    private static readonly PropertyKey Enumerator = new(new("a45c254e-df1c-4efd-8020-67d146a850e0"), 24);

    /// <summary>Samples anonymous current state; enumeration failures are unknown, never stale attached state.</summary>
    /// <returns>Posture and current USB/Bluetooth presence only; no identifiers are retained.</returns>
    internal static TabletInputSnapshot Read() => new(ReadPosture(), ReadKeyboard(EnumerateKeyboards));

    /// <summary>Classifies a fresh provider snapshot; providers are not cached and their failures remain unknown.</summary>
    /// <param name="enumerate">Current active-interface observations; tests may supply a synthetic provider.</param>
    /// <returns>Present for a matching active interface, Absent after complete enumeration, or Unknown after a provider failure.</returns>
    internal static KeyboardPresence ReadKeyboard(Func<IEnumerable<KeyboardObservation>> enumerate)
    {
        try
        {
            return enumerate().Any(item => item.Active && item.UsbOrBluetooth) ? KeyboardPresence.Present : KeyboardPresence.Absent;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        { return KeyboardPresence.Unknown; }
    }

    private static DevicePosture ReadPosture()
    {
        if (GetSystemMetrics(0x5F) <= 0) return DevicePosture.Desktop;
        // SlateMode alone is not meaningful on desktop PCs. Require Windows to
        // report rotation/posture support rather than treating every zero as slate.
        if (!GetAutoRotationState(out var rotation) || (rotation & (0x10 | 0x20)) != 0) return DevicePosture.Unknown;
        return (rotation & 0x80) != 0 || GetSystemMetrics(0x2003) != 0 ? DevicePosture.Desktop : DevicePosture.Slate;
    }

    private static IEnumerable<KeyboardObservation> EnumerateKeyboards()
    {
        var kind = KeyboardInterface;
        var devices = SetupDiGetClassDevs(ref kind, 0, 0, 0x12); // PRESENT | DEVICEINTERFACE
        if (devices == -1) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            for (uint index = 0; index < 128; index++)
            {
                var entry = new InterfaceData { Size = (uint)Marshal.SizeOf<InterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(devices, 0, ref kind, index, ref entry))
                {
                    if (Marshal.GetLastWin32Error() == 259) yield break;
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                if ((entry.Flags & 1) == 0) continue; // Only currently active interfaces.
                var info = new DeviceData { Size = (uint)Marshal.SizeOf<DeviceData>() };
                SetupDiGetDeviceInterfaceDetail(devices, ref entry, 0, 0, out var length, ref info);
                if (length is < 8 or > 8192) throw new InvalidOperationException("Unusable keyboard interface.");
                var detail = Marshal.AllocHGlobal((int)length);
                try
                {
                    Marshal.WriteInt32(detail, nint.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(devices, ref entry, detail, length, out _, ref info))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    // The detail buffer is not decoded: only the opaque devnode is
                    // needed. Device paths/names/IDs never enter managed state.
                    if (CM_Get_DevNode_Status(out var status, out _, info.Device, 0) != 0)
                        throw new IOException("Keyboard changed during enumeration.");
                    yield return new((status & 8) != 0, IsExternalBus(info.Device));
                }
                finally
                {
                    Marshal.Copy(new byte[length], 0, detail, (int)length);
                    Marshal.FreeHGlobal(detail);
                }
            }
            throw new InvalidOperationException("Keyboard enumeration limit reached.");
        }
        finally { SetupDiDestroyDeviceInfoList(devices); }
    }

    private static bool IsExternalBus(uint device)
    {
        for (var depth = 0; depth < 12; depth++)
        {
            var buffer = new byte[128]; uint length = (uint)buffer.Length; var key = BusType;
            if (CM_Get_DevNode_Property(device, ref key, out var type, buffer, ref length, 0) == 0 && type == 13 && length == 16
                && new Guid(buffer.AsSpan(0, 16)) == UsbBus) return true;
            key = Enumerator; length = (uint)buffer.Length;
            if (CM_Get_DevNode_Property(device, ref key, out type, buffer, ref length, 0) == 0 && type == 18 && length is >= 2 and <= 128)
            {
                // Generic bus enumerator labels, never a device name or instance ID.
                var bus = Encoding.Unicode.GetString(buffer, 0, (int)length).TrimEnd('\0');
                if (bus is "BTHENUM" or "BTHLEDEVICE" or "BTHLE") return true;
            }
            if (CM_Get_Parent(out var parent, device, 0) != 0) return false;
            device = parent;
        }
        return false;
    }

    [StructLayout(LayoutKind.Sequential)] private struct InterfaceData { public uint Size; public Guid Kind; public uint Flags; public nint Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceData { public uint Size; public Guid Kind; public uint Device; public nint Reserved; }
    [StructLayout(LayoutKind.Sequential)] private readonly record struct PropertyKey(Guid Format, uint Id);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetClassDevsW")] private static extern nint SetupDiGetClassDevs(ref Guid kind, nint enumerator, nint owner, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiEnumDeviceInterfaces(nint devices, nint device, ref Guid kind, uint index, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDeviceInterfaceDetailW")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiGetDeviceInterfaceDetail(nint devices, ref InterfaceData data, nint detail, uint length, out uint required, ref DeviceData device);
    [DllImport("setupapi.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiDestroyDeviceInfoList(nint devices);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_DevNode_PropertyW")] private static extern uint CM_Get_DevNode_Property(uint device, ref PropertyKey key, out uint type, byte[] buffer, ref uint length, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint device, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_Parent(out uint parent, uint device, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetAutoRotationState(out uint state);
}
