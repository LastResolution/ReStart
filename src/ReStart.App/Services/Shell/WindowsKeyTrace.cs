using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ReStart.App.Services;

// Deliberately closed vocabulary: this recorder cannot accept text, keys, paths,
// handles, process identifiers, window titles, or device/monitor information.
internal enum KeyTraceStage
{
    Started, Ready, Completed, LimitReached, CaptureWindows, CaptureStart, CloseOnDeactivate,
    HookInstalled, HookFailed, HookRemoved, HookHeartbeat, UiHeartbeat,
    WinDown, WinUp, InjectedWinDown, InjectedWinUp, KeyAction, ReplayWindowsDown,
    HotkeyReceived, ToggleQueued, ToggleDispatched, ToggleEntered, ToggleDismiss, TogglePresent,
    PresentEntered, SearchReturnPending, SearchHandoffPending, UsageBegin, UsageEnd,
    StartupPending, DialogActivated, LayoutBegin, LayoutEnd, TaskbarBegin, TaskbarEnd,
    BoundsBegin, BoundsEnd, ShowBegin, ShowEnd, ForegroundBefore, ForegroundAfter,
    ForegroundSetBegin, ForegroundSetEnd, ForegroundProbeBegin, ForegroundProbeEnd,
    ForegroundAttachBegin, ForegroundAttachEnd, ForegroundRetryBegin, ForegroundRetryEnd,
    ForegroundDetachBegin, ForegroundDetachEnd, ForegroundFocusBegin, ForegroundFocusEnd,
    ActivateBegin, ActivateEnd, SearchFocusBegin, SearchFocusEnd, PresentCompleted,
    VisibleChanged, Activated, Deactivated, ForegroundDismiss
}

internal enum KeyTraceDetail
{
    None, Yes, No, Left, Right, LeftWithModifier, RightWithModifier,
    Pass, Suppress, Toggle, Replay, WindowsKey, RegisteredHotkey,
    Owned, CoreWindow, XamlWindow, Taskbar, OtherWindow, NoWindow
}

/// <summary>Bounds an opt-in, enum-only input timing log without doing I/O or waiting in a hook callback.</summary>
internal sealed class WindowsKeyTrace : IDisposable
{
    internal const int MaximumEvents = 4096;
    internal const int DurationMs = 90_000;
    private readonly ConcurrentQueue<Entry> _pending = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly StreamWriter _writer;
    private readonly int _durationMs, _maximumEvents;
    private readonly Thread _writerThread;
    private readonly ManualResetEventSlim _stop = new();
    private int _count, _stopped, _limitReached;
    private int _writeFailed;
    internal bool Active => Volatile.Read(ref _stopped) == 0 && _clock.ElapsedMilliseconds < _durationMs;
    internal bool WriteFailed => Volatile.Read(ref _writeFailed) != 0;

    internal WindowsKeyTrace(string outputPath, int durationMs = DurationMs, int maximumEvents = MaximumEvents)
    {
        if (durationMs is < 1 or > DurationMs) throw new ArgumentOutOfRangeException(nameof(durationMs));
        if (maximumEvents is < 1 or > MaximumEvents) throw new ArgumentOutOfRangeException(nameof(maximumEvents));
        _durationMs = durationMs; _maximumEvents = maximumEvents;
        // CreateNew prevents accidentally overwriting an existing report or file.
        _writer = new(new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        try
        {
            _writer.WriteLine(JsonSerializer.Serialize(new { schema = 1, durationMs, maximumEvents }));
            _writer.Flush();
        }
        catch { _writer.Dispose(); throw; }
        Record(KeyTraceStage.Started);
        _writerThread = new(WriteLoop) { IsBackground = true, Name = "ReStart bounded key diagnostics" };
        _writerThread.Start();
    }

    internal void Record(KeyTraceStage stage, KeyTraceDetail detail = KeyTraceDetail.None, int delayMs = 0)
    {
        if (!Active || !Enum.IsDefined(stage) || !Enum.IsDefined(detail)) return;
        if (Interlocked.Increment(ref _count) > _maximumEvents) { Interlocked.Exchange(ref _limitReached, 1); return; }
        _pending.Enqueue(new((int)Math.Min(_clock.ElapsedMilliseconds, _durationMs), stage, detail, Math.Clamp(delayMs, 0, DurationMs)));
    }

    // Only Windows keys reach the export. Even a Win+letter shortcut is recorded
    // as a generic replay action by the caller, without its second key.
    internal void Key(uint virtualKey, bool down, bool injected, bool modifier, uint eventTime, uint now)
    {
        if (virtualKey is not (0x5B or 0x5C)) return;
        var stage = (down, injected) switch
        {
            (true, false) => KeyTraceStage.WinDown,
            (false, false) => KeyTraceStage.WinUp,
            (true, true) => KeyTraceStage.InjectedWinDown,
            _ => KeyTraceStage.InjectedWinUp
        };
        var detail = (virtualKey == 0x5B, modifier) switch
        {
            (true, false) => KeyTraceDetail.Left,
            (true, true) => KeyTraceDetail.LeftWithModifier,
            (false, false) => KeyTraceDetail.Right,
            _ => KeyTraceDetail.RightWithModifier
        };
        // Relative callback age, never raw system uptime. Subtraction wraps at
        // the same 32-bit boundary as KBDLLHOOKSTRUCT.time.
        Record(stage, detail, unchecked((int)(now - eventTime)));
    }

    private void WriteLoop()
    {
        try
        {
            do
            {
                Drain();
                _writer.Flush();
                if (!Active) break;
            } while (!_stop.Wait(200));
            Interlocked.Exchange(ref _stopped, 1);
            Drain();
            if (Volatile.Read(ref _limitReached) != 0) Write(new((int)Math.Min(_clock.ElapsedMilliseconds, _durationMs), KeyTraceStage.LimitReached, KeyTraceDetail.None, 0));
            Write(new(Math.Min((int)_clock.ElapsedMilliseconds, _durationMs), KeyTraceStage.Completed, KeyTraceDetail.None, 0));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        { Interlocked.Exchange(ref _writeFailed, 1); }
        finally
        {
            Interlocked.Exchange(ref _stopped, 1);
            try { _writer.Dispose(); }
            catch (IOException) { Interlocked.Exchange(ref _writeFailed, 1); }
        }
    }

    private void Drain() { while (_pending.TryDequeue(out var entry)) Write(entry); }
    private void Write(Entry entry) => _writer.WriteLine(JsonSerializer.Serialize(new
    {
        ms = entry.Ms,
        stage = entry.Stage.ToString(),
        detail = entry.Detail.ToString(),
        delayMs = entry.DelayMs
    }));

    public void Dispose()
    {
        Interlocked.Exchange(ref _stopped, 1);
        _stop.Set();
        _writerThread.Join(TimeSpan.FromSeconds(2));
        // Do not dispose the wait handle while a slow disk writer might use it.
    }

    private sealed record Entry(int Ms, KeyTraceStage Stage, KeyTraceDetail Detail, int DelayMs);
}
