using System.Net.Http;
using System.Windows;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    internal enum PreparationState { Idle, Downloading, Ready, Cancelled, Failed }
    internal PreparationState UpdatePreparation { get; private set; }
    internal long UpdateBytes { get; private set; }
    internal PortableUpdates.Prepared? PreparedUpdate { get; private set; }
    internal event Action? PreparedUpdateAvailable;
    private bool _readyNoticeExpanded = true, _updateRestarting;
    private CancellationTokenSource? _updateDownloadCancellation;
    internal bool CanInstallUpdates => !_preview && PortableUpdates.SupportsInstallation(AppContext.BaseDirectory);
    internal void CancelUpdateDownload() => _updateDownloadCancellation?.Cancel();

    /// <summary>Stages the explicitly selected release asynchronously; cancellation/failure never mutates the installation.</summary>
    /// <returns>A task completing after the UI reaches Ready, Cancelled or Failed; preview/ineligible calls do nothing.</returns>
    internal async Task PrepareUpdateAsync()
    {
        if (!CanInstallUpdates || UpdatePreparation is PreparationState.Downloading or PreparationState.Ready || LastUpdateCheck is not { Status: UpdateCheckStatus.Available, Release: { } release }) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_updateCancellation.Token);
        _updateDownloadCancellation = cancellation;
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false }) { Timeout = TimeSpan.FromMinutes(10) };
        UpdatePreparation = PreparationState.Downloading; UpdateBytes = 0; PreparedUpdate = null; UpdateCheckChanged?.Invoke();
        var directory = PortableUpdates.Workspace(Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(PortableUpdates.CacheRoot);
            // A write probe fails before a network download when the installation is protected.
            var probe = Path.Combine(AppContext.BaseDirectory, ".restart-probe-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            File.Delete(probe);
            var manifest = await new UpdateDownload(http).PrepareAsync(release, AppBuildInfo.Runtime, directory,
                new Progress<long>(bytes => { if (bytes - UpdateBytes < 1024 * 1024) return; UpdateBytes = bytes; UpdateCheckChanged?.Invoke(); }), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            PreparedUpdate = await Task.Run(() => PortableUpdates.FinalizePreparation(directory, manifest, AppContext.BaseDirectory), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            UpdatePreparation = PreparationState.Ready;
            PresentPreparedUpdate();
        }
        catch (OperationCanceledException) { UpdatePreparation = cancellation.IsCancellationRequested ? PreparationState.Cancelled : PreparationState.Failed; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException or System.ComponentModel.Win32Exception or BadImageFormatException)
        { UpdatePreparation = PreparationState.Failed; }
        finally
        {
            _updateDownloadCancellation = null;
            if (UpdatePreparation != PreparationState.Ready && Directory.Exists(directory))
                try { Directory.Delete(directory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            if (UpdatePreparation == PreparationState.Failed) PortableUpdates.Record(DiagnosticCode.UpdatePreparationFailed);
            UpdateCheckChanged?.Invoke();
        }
    }

    private async Task InitializeUpdateStateAsync()
    {
        await RestorePreparedUpdateAsync();
        if (!_exit && UpdatePreparation != PreparationState.Ready && Document.Preferences.CheckUpdatesOnStartup) await CheckStartupUpdatesAsync();
    }

    /// <summary>Revalidates a prior explicit preparation after ordinary startup, without applying it.</summary>
    /// <returns>A task completing after the ready indicator is restored or the cache is rejected.</returns>
    private async Task RestorePreparedUpdateAsync()
    {
        if (!CanInstallUpdates) return;
        var prepared = await Task.Run(() => PortableUpdates.RestorePrepared(AppContext.BaseDirectory));
        if (_exit || prepared == null || UpdatePreparation != PreparationState.Idle) return;
        PreparedUpdate = prepared; UpdatePreparation = PreparationState.Ready;
        PresentPreparedUpdate(); UpdateCheckChanged?.Invoke();
    }

    private void PresentPreparedUpdate()
    {
        _readyNoticeExpanded = true; RefreshPreparedUpdateNotice(); PreparedUpdateAvailable?.Invoke();
    }

    private void RefreshPreparedUpdateNotice()
    {
        if (PreparedUpdate == null || UpdatePreparation != PreparationState.Ready) return;
        if (_updateNotice != null) ContentArea.Children.Remove(_updateNotice);
        _updateNotice = new(PreparedUpdate.Version, AppBuildInfo.Version, RequestPreparedUpdate,
            () => { _readyNoticeExpanded = false; RefreshPreparedUpdateNotice(); }, prepared: true, compact: !_readyNoticeExpanded)
        { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(24), MaxWidth = Math.Max(240, (ContentArea.ActualWidth > 0 ? ContentArea.ActualWidth : 488) - 48) };
        ContentArea.Children.Add(_updateNotice);
    }

    /// <summary>Shows explicit restart consent or brings an open settings editor to its Updates section.</summary>
    internal async void RequestPreparedUpdate()
    {
        if (_updateRestarting || PreparedUpdate == null || UpdatePreparation != PreparationState.Ready) return;
        if (_activeSettings?.IsVisible == true) { _activeSettings.Activate(); _activeSettings.SelectSection(L.T(K.SettingsUpdates)); return; }
        if (!IsPresenting) Present();
        var accepted = false; WithDialog(() => accepted = ConfirmationWindow.Ask(this, L.T(K.UpdateConfirmRestart)));
        if (!accepted) return;
        _updateRestarting = true;
        try
        {
            if (await BeginPreparedUpdateAsync()) ((App)Application.Current).ExitApplication();
            else SetStatus(L.T(K.UpdatePrepareFailed));
        }
        finally { _updateRestarting = false; }
    }

    /// <summary>Saves a writable layout, preserves a protected layout unchanged, and hands off a prepared update without exiting the process itself.</summary>
    /// <returns>True only after helper readiness/acceptance; the caller then closes its dialog and exits normally.</returns>
    internal async Task<bool> BeginPreparedUpdateAsync()
    {
        if (!CanInstallUpdates || PreparedUpdate == null || UpdatePreparation != PreparationState.Ready || !SaveLayoutBeforeUpdate()) return false;
        try { return await PortableUpdates.StartAsync(PreparedUpdate); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { PortableUpdates.Record(DiagnosticCode.UpdatePreparationFailed); return false; }
    }

    // A newer producer or schema protects settings, not distribution files.
    // Keep normal save failures blocking restart; never rewrite a protected layout.
    private bool SaveLayoutBeforeUpdate() => _store.IsReadOnly || TrySaveLayout();
}
