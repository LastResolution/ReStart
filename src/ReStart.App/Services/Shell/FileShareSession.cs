using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.ApplicationModel.DataTransfer;
using ApiInformation = Windows.Foundation.Metadata.ApiInformation;
using Windows.Storage;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Keeps desktop sharing on the owner's pumping UI apartment, with one immutable file and bounded event subscriptions.</summary>
internal sealed class FileShareSession : IDisposable
{
    private readonly Window _owner;
    private readonly StorageFile _file;
    private readonly Action _completed;
    private readonly DataTransferManager _manager;
    private readonly IDataTransferManagerInterop _interop;
    private readonly List<DataPackage> _packages = [];
    private bool _disposed, _deactivated;

    // Cancellation is needed to release the Start focus guard even if the user
    // dismisses Share by clicking another application, without returning to Start.
    internal static bool IsSupported
    {
        get
        {
            try { return OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) && DataTransferManager.IsSupported() && ApiInformation.IsEventPresent("Windows.ApplicationModel.DataTransfer.DataPackage", "ShareCanceled"); }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or NotSupportedException) { return false; }
        }
    }

    internal static async Task<StorageFile> PrepareAsync(AppEntry app)
    {
        var path = AppShareTarget.Find(app) ?? throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
        // StorageFile acquisition alone can succeed for an unreadable file.
        // Recheck read access without reading bytes or resolving its association.
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { }
        return await StorageFile.GetFileFromPathAsync(path);
    }

    internal static async Task<bool> CanShareAsync(AppEntry app)
    {
        if (!IsSupported || AppShareTarget.Find(app) == null) return false;
        try { await PrepareAsync(app); return true; }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException or InvalidOperationException or NotSupportedException) { return false; }
    }

    internal FileShareSession(Window owner, StorageFile file, Action completed)
    {
        owner.Dispatcher.VerifyAccess();
        if (!IsSupported || !owner.IsVisible) throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
        _owner = owner; _file = file; _completed = completed;
        _interop = DataTransferManager.As<IDataTransferManagerInterop>();
        var iid = new Guid("a5caee9b-8708-49d1-8d36-67d25a8da00c");
        var pointer = _interop.GetForWindow(new WindowInteropHelper(owner).Handle, ref iid);
        try { _manager = WinRT.MarshalInterface<DataTransferManager>.FromAbi(pointer); }
        finally { Marshal.Release(pointer); }
        _manager.DataRequested += Requested;
        _owner.Deactivated += Deactivated;
        _owner.Activated += Activated;
    }

    internal void Show()
    {
        _owner.Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _interop.ShowShareUIForWindow(new WindowInteropHelper(_owner).Handle);
    }

    internal static void Populate(DataPackage data, StorageFile file)
    {
        data.Properties.Title = file.Name;
        data.RequestedOperation = DataPackageOperation.Copy;
        data.SetStorageItems([file], readOnly: true);
    }

    private void Requested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        if (_disposed) { args.Request.FailWithDisplayText(L.T(K.ErrorAppCommandUnavailable)); return; }
        try
        {
            // The prepared file belongs to this session; it cannot be replaced by
            // a later menu selection while Windows requests the payload.
            Populate(args.Request.Data, _file);
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) args.Request.Data.ShareCanceled += Canceled;
            args.Request.Data.ShareCompleted += Shared;
            _packages.Add(args.Request.Data);
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException)
        { args.Request.FailWithDisplayText(L.T(K.ErrorAppCommandUnavailable)); Finish(); }
    }

    private void Canceled(DataPackage sender, object args) => Finish();
    private void Shared(DataPackage sender, ShareCompletedEventArgs args) => Finish();
    private void Deactivated(object? sender, EventArgs args) => _deactivated = true;
    private void Activated(object? sender, EventArgs args) { if (_deactivated) Finish(); }
    private void Finish() => _owner.Dispatcher.BeginInvoke(() => { if (!_disposed) { Dispose(); _completed(); } });

    public void Dispose()
    {
        _owner.Dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        _owner.Deactivated -= Deactivated;
        _owner.Activated -= Activated;
        try
        {
            _manager.DataRequested -= Requested;
            foreach (var data in _packages)
            {
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) data.ShareCanceled -= Canceled;
                data.ShareCompleted -= Shared;
            }
        }
        catch (COMException) { /* Windows may have already torn down its share broker. */ }
        finally { _packages.Clear(); }
    }

    [ComImport, Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        nint GetForWindow(nint owner, ref Guid iid);
        void ShowShareUIForWindow(nint owner);
    }
}
