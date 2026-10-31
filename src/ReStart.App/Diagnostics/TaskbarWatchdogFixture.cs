using System.Diagnostics;
using ReStart.App.Services;

namespace ReStart.App;

/// <summary>Exercises real watchdog process lifetimes using a private fixture store and no native taskbar operations.</summary>
internal static class TaskbarWatchdogFixture
{
    private sealed class Backend(string directory, bool recovery) : ITaskbarLeaseBackend
    {
        public TaskbarLeaseState? Capture(TaskbarLeaseMode mode)
        {
            using var owner = Process.GetCurrentProcess();
            return new(1, Guid.NewGuid().ToString("N"), owner.Id, owner.StartTime.ToUniversalTime().Ticks, mode,
                [new(1, 1, 1, true, true, new(0, 0, 320, 200), new(0, 198, 320, 40), new(0, 160, 320, 40))]);
        }
        public bool IsCurrent(TaskbarLeaseState state) => true;
        public void Apply(TaskbarLeaseState state) { }
        public void Restore(TaskbarLeaseState state)
        {
            if (!recovery) return;
            using var output = new FileStream(Path.Combine(directory, "recovered"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            output.WriteByte(1); output.Flush(flushToDisk: true);
        }
    }

    private static bool IsFixtureDirectory(string directory)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)));
        return name.StartsWith("watchdog-", StringComparison.Ordinal) && Guid.TryParseExact(name[9..], "N", out _);
    }

    internal static Task<int> RecoverAsync(string parentId, string parentStarted, string nonce, string directory)
        => IsFixtureDirectory(directory) ? TaskbarWatchdog.RunCoreAsync(parentId, parentStarted, nonce, new TaskbarRecoveryStore(directory), new Backend(directory, true))
            : Task.FromResult(1);

    internal static async Task<int> OwnAsync(string directory)
    {
        try
        {
            directory = Path.GetFullPath(directory); TaskbarRecoveryStore.CheckLinks(directory);
            if (!IsFixtureDirectory(directory) || Directory.Exists(directory)) return 1;
            var store = new TaskbarRecoveryStore(directory);
            var watchdog = new TaskbarWatchdog(store, ownedFixture: true);
            // Deliberately leave this inert lease undisposed to model lost normal
            // cleanup. Only the independent helper can clear its durable marker.
            var lease = new TaskbarLease(store, new Backend(directory, false), watchdog.Protect, () => watchdog.IsReady);
            if (!lease.Synchronize(TaskbarLeaseMode.Hide) || !lease.Synchronize(TaskbarLeaseMode.Reveal)
                || Directory.GetFiles(directory, "taskbar-watchdog-*.ready").Length != 1) return 1;
            using (var ready = new FileStream(Path.Combine(directory, "owner-ready"), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            { ready.WriteByte(1); ready.Flush(flushToDisk: true); }
            return await Console.In.ReadLineAsync() == "exit" ? 0 : 1;
        }
        catch (Exception error) when (TaskbarLease.Expected(error) || error is ArgumentException) { return 1; }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    internal static async Task<int> VerifyAsync(string directory)
    {
        var root = Path.Combine(directory, "watchdog-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true };
        start.ArgumentList.Add("--taskbar-watchdog-owner-fixture"); start.ArgumentList.Add(root);
        using var owner = Process.Start(start) ?? throw new IOException("Owned watchdog parent did not start.");
        try
        {
            var deadline = Environment.TickCount64 + 15000;
            while (!File.Exists(Path.Combine(root, "owner-ready")) && !owner.HasExited && Environment.TickCount64 < deadline) await Task.Delay(25);
            Require(File.Exists(Path.Combine(root, "owner-ready")), "Owned watchdog readiness or guard reuse failed.");
            var store = new TaskbarRecoveryStore(root);
            var snapshot = store.Read();
            Require(snapshot?.ParentId == owner.Id, "The owned watchdog marker lost its exact parent identity.");
            await owner.StandardInput.WriteLineAsync("exit"); owner.StandardInput.Close();
            using var exit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await owner.WaitForExitAsync(exit.Token);
            Require(owner.ExitCode == 0, "The owned watchdog parent failed.");
            deadline = Environment.TickCount64 + 10000;
            while ((!File.Exists(Path.Combine(root, "recovered")) || File.Exists(store.MarkerPath)) && Environment.TickCount64 < deadline) await Task.Delay(25);
            Require(File.Exists(Path.Combine(root, "recovered")) && !File.Exists(store.MarkerPath), "The independent watchdog did not recover its orphaned owned marker.");
            return 4;
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(); await owner.WaitForExitAsync(); }
        }
    }
}
