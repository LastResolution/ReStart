using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ReStart.App.Tablet.Media;

/// <summary>Snapshot of the current system media session (Spotify, Brave, ...).</summary>
/// <param name="Title">Track or video title.</param>
/// <param name="Artist">Artist, channel or empty.</param>
/// <param name="Album">Album or empty.</param>
/// <param name="SourceApp">App user model ID of the playing app.</param>
/// <param name="IsPlaying">Whether playback is running.</param>
/// <param name="Thumbnail">Cover art bytes (usually JPEG/PNG), or null.</param>
/// <param name="Position">Playback position at <paramref name="Updated"/>.</param>
/// <param name="Duration">Track length, or zero when unknown.</param>
/// <param name="Updated">When the position was sampled.</param>
internal sealed record NowPlaying(string Title, string Artist, string Album, string SourceApp, bool IsPlaying,
    byte[]? Thumbnail, TimeSpan Position, TimeSpan Duration, DateTimeOffset Updated);

/// <summary>
/// Wraps Windows' global media transport controls (the same source as the volume flyout).
/// One shared instance; <see cref="Changed"/> fires on a thread-pool thread, so UI consumers marshal.
/// </summary>
internal sealed class MediaSessions
{
    /// <summary>Process-wide instance.</summary>
    internal static MediaSessions Shared { get; } = new();

    private readonly SemaphoreSlim _refresh = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private Task? _start;

    /// <summary>Latest snapshot, or null when nothing is playing.</summary>
    internal NowPlaying? Current { get; private set; }
    /// <summary>Raised after <see cref="Current"/> changes (thread-pool thread).</summary>
    internal event Action<NowPlaying?>? Changed;

    /// <summary>Connects to the session manager once; later calls return the same task.</summary>
    internal Task StartAsync() => _start ??= StartCoreAsync();

    private async Task StartCoreAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => _ = AttachAsync();
            _manager.SessionsChanged += (_, _) => _ = AttachAsync();
            await AttachAsync();
        }
        catch (Exception ex) { TabletLog.Write("media", "start failed: " + ex.Message); }
    }

    private async Task AttachAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (!ReferenceEquals(session, _session))
        {
            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnSessionChanged;
                _session.PlaybackInfoChanged -= OnSessionChanged;
                _session.TimelinePropertiesChanged -= OnSessionChanged;
            }
            _session = session;
            if (_session != null)
            {
                _session.MediaPropertiesChanged += OnSessionChanged;
                _session.PlaybackInfoChanged += OnSessionChanged;
                _session.TimelinePropertiesChanged += OnSessionChanged;
            }
        }
        await RefreshAsync();
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        await _refresh.WaitAsync();
        try
        {
            var session = _session;
            NowPlaying? next = null;
            if (session != null)
            {
                var props = await session.TryGetMediaPropertiesAsync();
                var playback = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();
                byte[]? art = Current?.Title == props?.Title && Current?.SourceApp == session.SourceAppUserModelId ? Current?.Thumbnail : null;
                if (art == null && props?.Thumbnail != null) art = await ReadAsync(props.Thumbnail);
                if (props != null && !string.IsNullOrEmpty(props.Title))
                    next = new NowPlaying(props.Title, props.Artist ?? "", props.AlbumTitle ?? "", session.SourceAppUserModelId ?? "",
                        playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                        art, timeline.Position, timeline.EndTime - timeline.StartTime, timeline.LastUpdatedTime);
            }
            if (next == Current) return;
            Current = next;
            Changed?.Invoke(next);
        }
        catch (Exception ex) { TabletLog.Write("media", "refresh failed: " + ex.Message); }
        finally { _refresh.Release(); }
    }

    private static async Task<byte[]?> ReadAsync(IRandomAccessStreamReference reference)
    {
        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 8 * 1024 * 1024) return null;
            var buffer = new Windows.Storage.Streams.Buffer((uint)stream.Size);
            var read = await stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None);
            var bytes = new byte[read.Length];
            using var reader = DataReader.FromBuffer(read);
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception) { return null; }
    }

    /// <summary>Toggles play/pause on the current session.</summary>
    internal async Task PlayPauseAsync() { if (_session != null) await _session.TryTogglePlayPauseAsync(); }
    /// <summary>Skips to the next track.</summary>
    internal async Task NextAsync() { if (_session != null) await _session.TrySkipNextAsync(); }
    /// <summary>Goes to the previous track.</summary>
    internal async Task PreviousAsync() { if (_session != null) await _session.TrySkipPreviousAsync(); }
}
