using System.Runtime.InteropServices;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>
/// Master volume and mute of the default render endpoint through Core Audio (the same
/// value as the taskbar volume flyout). The endpoint is re-resolved on <see cref="Attach"/>
/// so a newly plugged headset or the soundbar becomes the target.
/// </summary>
internal sealed class SystemAudio : IDisposable
{
    private static readonly Guid ContextId = Guid.NewGuid(); // tags our own changes so their notifications can be ignored
    private IAudioEndpointVolume? _endpoint;
    private Callback? _callback;

    /// <summary>Raised on a Core Audio thread when another app or a key changes the volume or mute.</summary>
    internal event Action? ExternalChange;

    /// <summary>Whether a render endpoint is available.</summary>
    internal bool IsAvailable => _endpoint != null;

    /// <summary>Binds to the current default render endpoint.</summary>
    internal void Attach()
    {
        Detach();
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var device) != 0 || device == null) return;
                var iid = typeof(IAudioEndpointVolume).GUID;
                if (device.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, 0, out var volume) == 0)
                {
                    _endpoint = (IAudioEndpointVolume)volume;
                    _callback = new Callback(this);
                    _endpoint.RegisterControlChangeNotify(_callback);
                }
                Marshal.ReleaseComObject(device);
            }
            finally { Marshal.ReleaseComObject(enumerator); }
        }
        catch (COMException ex) { TabletLog.Write("cc", "audio attach failed: " + ex.Message); _endpoint = null; }
    }

    /// <summary>Volume 0..1, or -1 when unavailable.</summary>
    internal float Volume => _endpoint != null && _endpoint.GetMasterVolumeLevelScalar(out var level) == 0 ? level : -1;

    /// <summary>Whether the endpoint is muted.</summary>
    internal bool Muted => _endpoint != null && _endpoint.GetMute(out var muted) == 0 && muted;

    /// <summary>Sets the volume (0..1); unmutes when raised above zero like the system flyout.</summary>
    internal void SetVolume(float value)
    {
        if (_endpoint == null) return;
        var context = ContextId;
        _endpoint.SetMasterVolumeLevelScalar(Math.Clamp(value, 0, 1), ref context);
        if (value > 0 && Muted) _endpoint.SetMute(false, ref context);
    }

    /// <summary>Sets mute.</summary>
    internal void SetMuted(bool muted)
    {
        if (_endpoint == null) return;
        var context = ContextId;
        _endpoint.SetMute(muted, ref context);
    }

    private void Detach()
    {
        if (_endpoint == null) return;
        try { if (_callback != null) _endpoint.UnregisterControlChangeNotify(_callback); }
        catch (COMException) { }
        Marshal.ReleaseComObject(_endpoint);
        _endpoint = null; _callback = null;
    }

    public void Dispose() => Detach();

    private sealed class Callback(SystemAudio owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(nint data)
        {
            // AUDIO_VOLUME_NOTIFICATION_DATA starts with the event-context GUID.
            if (data != 0 && Marshal.PtrToStructure<Guid>(data) == ContextId) return 0;
            owner.ExternalChange?.Invoke();
            return 0;
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int context, nint parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(nint data);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float decibels, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float decibels);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float decibels, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float decibels);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
