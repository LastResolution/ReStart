using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ReStart.App.Services;

namespace ReStart.App;

public partial class MainWindow
{
    internal async Task<NativeJumpSmokeResult> VerifyNativeJumpListsAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Native jump-list checks require preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var probe = Environment.GetEnvironmentVariable("RESTART_TEST_SHELL_PROBE");
        if (probe == null || !File.Exists(probe)) throw new InvalidOperationException("The owned shell probe was not configured.");
        var marker = Path.Combine(directory, "Task sample 日本語, & (one).txt"); File.WriteAllText(marker, "owned native task fixture");
        var owner = "ReStart.JumpFixture." + Guid.NewGuid().ToString("N");
        var checks = 0; var faults = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        async Task Until(Func<bool> predicate)
        {
            var deadline = Environment.TickCount64 + 5000;
            while (!predicate() && Environment.TickCount64 < deadline) await Task.Delay(25);
            Check(predicate(), "The owned native task did not publish its marker.");
        }
        IDisposable? fixture = NativeJumpWorker.CreateOwnedFixture(owner, probe, marker, directory);
        try
        {
            var snapshot = await NativeJumpBroker.SendAsync(new(1, "read", owner));
            Check(snapshot.Success && snapshot.Snapshot.TasksSupported && snapshot.Snapshot.PinsSupported, "The Windows task/pin interfaces did not initialize in the worker.");
            var task = snapshot.Snapshot.Tasks.Single();
            Check(task.Name == "Owned sample action" && task.IsLink && task.Target.Length == 0, "The native task's title, type or private argument boundary is incorrect.");
            Check((await NativeJumpBroker.SendAsync(new(1, "invoke-task", owner, task.Key))).Success, "The owned registered task did not execute.");
            await Until(() => File.Exists(marker + ".opened"));
            Check(JsonSerializer.Deserialize<string[]>(File.ReadAllText(marker + ".opened")) is ["--jump-fixture", var received] && received == marker,
                "A native task changed Unicode arguments or interpreted shell metacharacters.");
            Check(Path.GetFullPath(File.ReadAllText(marker + ".cwd")).TrimEnd('\\').Equals(Path.GetFullPath(directory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase), "A native task lost its registered working directory.");
            var pinned = await NativeJumpBroker.SendAsync(new(1, "pin", owner, Target: marker));
            Check(pinned.Success && pinned.Snapshot.Pinned.Single().Target.Equals(marker, StringComparison.OrdinalIgnoreCase), "Native pinning did not persist in the Windows list.");
            var reread = await NativeJumpBroker.SendAsync(new(1, "read", owner));
            Check(reread.Snapshot.Pinned.Length == 1, "A new worker did not observe the Windows pin.");
            Check((await NativeJumpBroker.SendAsync(new(1, "unpin", owner, pinned.Snapshot.Pinned[0].Key))).Success, "Native unpinning failed.");
            Check((await NativeJumpBroker.SendAsync(new(1, "read", owner))).Snapshot.Pinned.Length == 0 && File.ReadAllText(marker) == "owned native task fixture", "Unpinning retained the item or changed its file.");
            File.Delete(marker + ".opened"); File.Delete(marker + ".cwd");
            Check(!(await NativeJumpBroker.SendAsync(new(1, "invoke-task", owner, new string('0', 64)))).Success && !File.Exists(marker + ".opened"), "An unknown key executed a native task.");
            fixture.Dispose(); fixture = null;
            fixture = NativeJumpWorker.CreateOwnedFixture(owner, probe, marker, directory, "--jump-fixture-2");
            Check(!(await NativeJumpBroker.SendAsync(new(1, "invoke-task", owner, task.Key))).Success && !File.Exists(marker + ".opened"), "A stale task key executed a replacement task at the same index.");
            var changed = await NativeJumpBroker.SendAsync(new(1, "read", owner));
            Check(changed.Snapshot.Tasks.Single().Key != task.Key && (await NativeJumpBroker.SendAsync(new(1, "invoke-task", owner, changed.Snapshot.Tasks[0].Key))).Success,
                "A replacement task did not receive its own validated identity.");
            await Until(() => File.Exists(marker + ".opened"));
            Check(JsonSerializer.Deserialize<string[]>(File.ReadAllText(marker + ".opened")) is ["--jump-fixture-2", var second] && second == marker, "The replacement native task invoked stale arguments.");
            foreach (var fault in new[] { "crash", "malformed", "oversized", "timeout" })
            {
                var start = new ProcessStartInfo(probe)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(false),
                    StandardOutputEncoding = new UTF8Encoding(false)
                };
                start.ArgumentList.Add("--jump-worker-fault"); start.ArgumentList.Add(fault);
                var timer = Stopwatch.StartNew(); var failed = false;
                try { await NativeJumpBroker.ExchangeAsync(new(1, "read", owner), start, TimeSpan.FromMilliseconds(700)); }
                catch (Exception ex) when (ex is IOException or JsonException or OperationCanceledException) { failed = true; }
                Check(failed && timer.Elapsed < TimeSpan.FromSeconds(4), "A failed native helper was accepted or blocked past its deadline."); faults++;
            }
            Check((await NativeJumpBroker.SendAsync(new(1, "read", owner))).Success, "A failed helper prevented the next valid request.");
            foreach (var invalid in new[] { new NativeJumpRequest(2, "read", owner), new(1, "shell", owner), new(1, "read", "bad\nowner"), new(1, "read", owner, Target: marker), new(1, "invoke-task", owner, "bad"), new(1, "pin", owner) })
                Check(!NativeJumpContract.ValidRequest(invalid), "The private protocol accepted an invalid operation or identity.");
            checks += await VerifyNativeJumpMenusAsync(directory, snapshotPath);
            return new(checks, faults, true, true);
        }
        finally
        {
            try
            {
                var state = await NativeJumpBroker.SendAsync(new(1, "read", owner));
                foreach (var item in state.Snapshot.Pinned.Take(4)) await NativeJumpBroker.SendAsync(new(1, "unpin", owner, item.Key));
            }
            finally { fixture?.Dispose(); }
        }
    }
}

internal sealed record NativeJumpSmokeResult(int Checks, int FaultCases, bool TaskInvocation, bool WindowsPinRoundTrip);
