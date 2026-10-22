using System.Net.Http;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    internal enum PreparationState { Idle, Downloading, Ready, Cancelled, Failed }
    internal PreparationState UpdatePreparation { get; private set; }
    internal long UpdateBytes { get; private set; }
    internal PortableUpdates.Prepared? PreparedUpdate { get; private set; }
    private CancellationTokenSource? _updateDownloadCancellation;
    internal bool CanInstallUpdates => !_preview && !_store.IsReadOnly && PortableUpdates.SupportsInstallation(AppContext.BaseDirectory);
    internal void CancelUpdateDownload() => _updateDownloadCancellation?.Cancel();

    /// <summary>Stages the explicitly selected release asynchronously; cancellation/failure never mutates the installation.</summary>
    /// <returns>A task completing after the UI reaches Ready, Cancelled or Failed; preview/ineligible calls do nothing.</returns>
    internal async Task PrepareUpdateAsync()
    {
        if (!CanInstallUpdates || UpdatePreparation == PreparationState.Downloading || LastUpdateCheck is not { Status: UpdateCheckStatus.Available, Release: { } release }) return;
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
        }
        catch (OperationCanceledException) { UpdatePreparation = cancellation.IsCancellationRequested ? PreparationState.Cancelled : PreparationState.Failed; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException or System.ComponentModel.Win32Exception or BadImageFormatException)
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

    /// <summary>Saves the layout and hands off a prepared update without exiting the process itself.</summary>
    /// <returns>True only after helper readiness/acceptance; the caller then closes its dialog and exits normally.</returns>
    internal async Task<bool> BeginPreparedUpdateAsync()
    {
        if (!CanInstallUpdates || PreparedUpdate == null || UpdatePreparation != PreparationState.Ready || !TrySaveLayout()) return false;
        try { return await PortableUpdates.StartAsync(PreparedUpdate); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { PortableUpdates.Record(DiagnosticCode.UpdatePreparationFailed); return false; }
    }
}
