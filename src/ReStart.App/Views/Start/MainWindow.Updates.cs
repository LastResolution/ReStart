using System.Net.Http;
using System.Windows;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly HttpClient _releaseHttp = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false }) { Timeout = TimeSpan.FromSeconds(15) };
    private readonly CancellationTokenSource _updateCancellation = new();
    private Task<UpdateCheckResult>? _updateTask;
    internal UpdateCheckResult? LastUpdateCheck { get; private set; }
    internal DateTimeOffset? LastUpdateCheckAt { get; private set; }
    internal event Action? UpdateCheckChanged;
    internal event Action<UpdateRelease>? StartupUpdateAvailable;
    private bool _startupUpdateNotified;
    private UpdateNotice? _updateNotice;

    private async Task CheckStartupUpdatesAsync()
    {
        var result = await CheckForUpdatesAsync();
        PresentStartupUpdate(result);
    }

    private void PresentStartupUpdate(UpdateCheckResult result)
    {
        if (_exit || UpdatePreparation == PreparationState.Ready || !Document.Preferences.CheckUpdatesOnStartup || _startupUpdateNotified || result.Status != UpdateCheckStatus.Available || result.Release == null) return;
        _startupUpdateNotified = true;
        ShowUpdateNotice(result.Release);
        StartupUpdateAvailable?.Invoke(result.Release);
    }

    private void ShowUpdateNotice(UpdateRelease release)
    {
        if (_updateNotice != null) Root.Children.Remove(_updateNotice);
        _updateNotice = new(release.Version.ToString(), AppBuildInfo.Version, ReviewAvailableUpdate, () =>
        { Root.Children.Remove(_updateNotice!); _updateNotice = null; })
        { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(24), MaxWidth = Math.Max(240, (Root.ActualWidth > 0 ? Root.ActualWidth : 488) - 48) };
        Root.Children.Add(_updateNotice);
    }

    /// <summary>Opens the in-app update choice without downloading, launching a browser or discarding an existing settings draft.</summary>
    internal void ReviewAvailableUpdate()
    {
        if (_exit) return;
        if (UpdatePreparation != PreparationState.Ready && _updateNotice != null)
        { Root.Children.Remove(_updateNotice); _updateNotice = null; }
        PresentSettings(K.SettingsUpdates);
    }

    internal Task<UpdateCheckResult> CheckForUpdatesAsync(bool? includePrereleases = null)
    {
        if (_updateTask is { IsCompleted: false }) return _updateTask;
        return _updateTask = CheckUpdatesCoreAsync(includePrereleases ?? Document.Preferences.IncludePrereleaseUpdates);
    }
    private async Task<UpdateCheckResult> CheckUpdatesCoreAsync(bool includePrereleases)
    {
        UpdateCheckResult result;
        if (_preview) return new(UpdateCheckStatus.ServiceUnavailable);
        try
        {
            result = await new ReleaseClient(_releaseHttp).CheckAsync(AppBuildInfo.Version, AppBuildInfo.Runtime, includePrereleases, _updateCancellation.Token);
        }
        catch (OperationCanceledException) { return new(UpdateCheckStatus.NetworkError); }
        LastUpdateCheck = result;
        LastUpdateCheckAt = DateTimeOffset.Now;
        if (result.Status is UpdateCheckStatus.NetworkError or UpdateCheckStatus.InvalidResponse or UpdateCheckStatus.ServiceUnavailable or UpdateCheckStatus.RateLimited)
            PortableUpdates.Record(DiagnosticCode.UpdateCheckFailed);
        if (result.Status == UpdateCheckStatus.Available && result.Release != null && _store.RecoveryMessage == null)
            SetStatus(L.F(K.UpdateAvailable, result.Release.Version));
        UpdateCheckChanged?.Invoke();
        return result;
    }
    internal static string UpdateStatusText(UpdateCheckResult result) => result.Status switch
    {
        UpdateCheckStatus.Available => L.F(K.UpdateAvailable, result.Release!.Version),
        UpdateCheckStatus.UpToDate => L.T(K.UpdateUpToDate),
        UpdateCheckStatus.NoReleases => L.T(K.UpdateNoReleases),
        UpdateCheckStatus.ServiceUnavailable => L.T(K.UpdateServiceUnavailable),
        UpdateCheckStatus.RateLimited => L.T(K.UpdateRateLimited),
        UpdateCheckStatus.NetworkError => L.T(K.UpdateNetworkError),
        _ => L.T(K.UpdateInvalidResponse)
    };
}
