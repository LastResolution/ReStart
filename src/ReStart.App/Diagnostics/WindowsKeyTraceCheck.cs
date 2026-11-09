using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

/// <summary>Exercises an owned hotkey-message route without typing into or inspecting another application.</summary>
internal static class WindowsKeyTraceCheck
{
    internal static async Task<int> RunAsync(string report)
    {
        var output = Path.ChangeExtension(report, ".trace.jsonl");
        var count = 0;
        var success = false;
        var window = new Window { ShowInTaskbar = false };
        try
        {
            using (var trace = new WindowsKeyTrace(output))
            {
                WindowsKeyDiagnostics.Current = trace;
                var handle = new WindowInteropHelper(window).EnsureHandle();
                using var integration = new StartIntegration(handle, window.Dispatcher, () =>
                {
                    count++;
                    WindowsKeyDiagnostics.Mark(KeyTraceStage.ToggleEntered);
                }, () => { }, _ => { });
                integration.Apply(new Preferences { CaptureWindowsKey = false, CaptureStartButton = false, CloseOnDeactivate = false });
                Native.SendMessageTimeout(handle, 0x0312, 0x5253, 0, 2, 1000, out _);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                await Task.Delay(1250); // Real hook-thread and UI heartbeat round trips.
            }
            var rows = File.ReadAllLines(output).Skip(1).Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
            var stages = rows.Select(row => row.GetProperty("stage").GetString()).ToArray();
            success = count == 1 && stages.Contains("HookHeartbeat") && stages.Contains("UiHeartbeat")
                && stages.Contains("HotkeyReceived") && stages.Contains("ToggleQueued") && stages.Contains("ToggleDispatched")
                && stages.Contains("ToggleEntered") && stages[^1] == "Completed"
                && rows.All(row => row.EnumerateObject().Select(p => p.Name).SequenceEqual(new[] { "ms", "stage", "detail", "delayMs" }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { success = false; }
        finally { WindowsKeyDiagnostics.Current = null; window.Close(); }
        File.WriteAllText(report, JsonSerializer.Serialize(new { success, routedToggles = count }));
        return success ? 0 : 1;
    }
}
