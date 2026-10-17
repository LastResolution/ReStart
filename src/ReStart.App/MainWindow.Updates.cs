using System.ComponentModel;
using System.Net.Http;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly HttpClient _releaseHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    private readonly CancellationTokenSource _updateCancellation = new();
    private Task<UpdateCheckResult>? _updateTask;
    internal UpdateCheckResult? LastUpdateCheck { get; private set; }
    internal DateTimeOffset? LastUpdateCheckAt { get; private set; }
    internal event Action? UpdateCheckChanged;

    internal Task<UpdateCheckResult> CheckForUpdatesAsync(bool? includePrereleases = null)
    {
        if (_updateTask is { IsCompleted: false }) return _updateTask;
        return _updateTask = CheckUpdatesCoreAsync(includePrereleases ?? Document.Preferences.IncludePrereleaseUpdates);
    }
    private async Task<UpdateCheckResult> CheckUpdatesCoreAsync(bool includePrereleases)
    {
        UpdateCheckResult result;
        if (_preview) return new(UpdateCheckStatus.AccessRequired);
        try
        {
            var token = ReleaseCredentialStore.Current.Read();
            result = await new ReleaseClient(_releaseHttp).CheckAsync(AppBuildInfo.Version, AppBuildInfo.Runtime, includePrereleases, token, _updateCancellation.Token);
        }
        catch (Win32Exception) { result = new(UpdateCheckStatus.AccessRequired); }
        catch (OperationCanceledException) { return new(UpdateCheckStatus.NetworkError); }
        LastUpdateCheck = result;
        LastUpdateCheckAt = DateTimeOffset.Now;
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
        UpdateCheckStatus.AccessRequired => L.T(K.UpdateAccessRequired),
        UpdateCheckStatus.RateLimited => L.T(K.UpdateRateLimited),
        UpdateCheckStatus.NetworkError => L.T(K.UpdateNetworkError),
        _ => L.T(K.UpdateInvalidResponse)
    };
}
