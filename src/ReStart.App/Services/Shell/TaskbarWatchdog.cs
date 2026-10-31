using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ReStart.App.Services;

/// <summary>Keeps one independent, ready recovery process alive before any temporary taskbar mutation.</summary>
/// <param name="store">Normal local recovery store, never a caller-supplied helper path.</param>
/// <param name="ownedFixture">Uses the inert diagnostic recovery entry point with the explicit fixture store; false for production.</param>
internal sealed class TaskbarWatchdog(TaskbarRecoveryStore store, bool ownedFixture = false)
{
    private Process? _process;
    private string _nonce = Guid.NewGuid().ToString("N");
    private sealed record Ready(int ParentId, long ParentStarted, string Nonce);
    private sealed class Protection : IDisposable { public void Dispose() { } }
    internal static string Profile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart");

    /// <summary>Waits on a background worker for a validated readiness handshake; a missing guard prevents native mutation.</summary>
    /// <param name="state">Verified recovery transaction belonging to the calling process.</param>
    /// <returns>A completion lease. The shared helper survives until its parent exits so crash recovery remains available.</returns>
    internal IDisposable Protect(TaskbarLeaseState state)
    {
        using var parent = Process.GetCurrentProcess();
        if (state.ParentId != parent.Id || state.ParentStarted != parent.StartTime.ToUniversalTime().Ticks)
            throw new InvalidOperationException("Unexpected taskbar owner.");
        if (_process is { HasExited: false })
        {
            if (ReadReady(state)) return new Protection();
            _process.Kill();
        }
        _process?.Dispose();
        _nonce = Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var argument in new[] { ownedFixture ? "--taskbar-watchdog-fixture" : "--taskbar-watchdog", state.ParentId.ToString(CultureInfo.InvariantCulture), state.ParentStarted.ToString(CultureInfo.InvariantCulture), _nonce })
            start.ArgumentList.Add(argument);
        if (ownedFixture) start.ArgumentList.Add(store.DirectoryPath);
        _process = Process.Start(start) ?? throw new IOException("Taskbar watchdog could not start.");
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(10) && !_process.HasExited)
        {
            if (ReadReady(state)) return new Protection();
            Thread.Sleep(50);
        }
        // This reference is only the helper started above; no user process is terminated.
        if (!_process.HasExited) _process.Kill();
        _process.Dispose(); _process = null;
        throw new IOException("Taskbar watchdog did not become ready.");
    }

    private bool ReadReady(TaskbarLeaseState state)
    {
        var path = ReadyPath(store, _nonce);
        try
        {
            TaskbarRecoveryStore.CheckLinks(path);
            if (!File.Exists(path) || new FileInfo(path).Length > 1024) return false;
            return JsonSerializer.Deserialize<Ready>(File.ReadAllBytes(path)) == new Ready(state.ParentId, state.ParentStarted, _nonce);
        }
        catch (Exception error) when (TaskbarLease.Expected(error) || error is JsonException) { return false; }
    }

    /// <summary>Runs the strict internal helper before single-instance, hooks or launcher startup.</summary>
    /// <param name="parentId">Positive parent process ID; the exact executable, session and start time are validated.</param>
    /// <param name="parentStarted">Parent UTC process-start ticks; reused process IDs do not match.</param>
    /// <param name="nonce">Exactly one N-format GUID used only for the private readiness file.</param>
    /// <returns>Zero after safe completion; one for invalid identity, readiness failure or blocked recovery.</returns>
    internal static Task<int> RunAsync(string parentId, string parentStarted, string nonce)
        => RunCoreAsync(parentId, parentStarted, nonce, new TaskbarRecoveryStore(Profile), new TaskbarNativeBackend());

    /// <summary>Runs the same identity, readiness and orphan lifecycle with explicit inert fixture dependencies.</summary>
    /// <param name="parentId">Expected parent process ID.</param>
    /// <param name="parentStarted">Expected parent UTC start ticks.</param>
    /// <param name="nonce">N-format readiness nonce.</param>
    /// <param name="store">Owned fixture recovery store or the fixed production profile.</param>
    /// <param name="backend">Identity-validating production backend or completely inert fixture backend.</param>
    /// <returns>Zero after completion, or one after invalid identity/readiness/recovery failure.</returns>
    internal static async Task<int> RunCoreAsync(string parentId, string parentStarted, string nonce, TaskbarRecoveryStore store, ITaskbarLeaseBackend backend)
    {
        if (!int.TryParse(parentId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0
            || !long.TryParse(parentStarted, NumberStyles.None, CultureInfo.InvariantCulture, out var started) || started <= 0
            || !Guid.TryParseExact(nonce, "N", out _)) return 1;
        var path = ReadyPath(store, nonce);
        var created = false;
        try
        {
            using var current = Process.GetCurrentProcess();
            using var parent = Process.GetProcessById(id);
            if (parent.Id == current.Id || parent.HasExited || parent.StartTime.ToUniversalTime().Ticks != started || parent.SessionId != current.SessionId
                || !string.Equals(parent.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) return 1;
            TaskbarRecoveryStore.CheckLinks(path); Directory.CreateDirectory(store.DirectoryPath);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Ready(id, started, nonce));
            using (var ready = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            { created = true; ready.Write(bytes); ready.Flush(flushToDisk: true); }
            await parent.WaitForExitAsync();
            for (var attempt = 0; attempt < 30; attempt++)
            {
                if (TaskbarRecovery.Run(store, backend, IsAlive, (id, started))) return 0;
                await Task.Delay(1000);
            }
            return 1;
        }
        catch (Exception error) when (TaskbarLease.Expected(error) || error is ArgumentException or System.Security.SecurityException) { return 1; }
        finally
        {
            try { if (created) { TaskbarRecoveryStore.CheckLinks(path); File.Delete(path); } }
            catch (Exception error) when (TaskbarLease.Expected(error)) { }
        }
    }

    /// <summary>Checks a process lifetime without interpreting access failures as proof of termination.</summary>
    /// <param name="id">Recovery owner's positive process ID.</param>
    /// <param name="started">Expected UTC start-time ticks.</param>
    /// <returns>False only when the identity has ended or its ID was reused; true on uncertainty.</returns>
    internal static bool IsAlive(int id, long started)
    {
        try { using var process = Process.GetProcessById(id); return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == started; }
        catch (ArgumentException) { return false; }
        catch (Exception error) when (TaskbarLease.Expected(error) || error is System.Security.SecurityException) { return true; }
    }

    private static string ReadyPath(TaskbarRecoveryStore store, string nonce) => Path.Combine(store.DirectoryPath, "taskbar-watchdog-" + nonce + ".ready");
}
