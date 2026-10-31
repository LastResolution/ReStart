using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;

namespace ReStart.App;

/// <summary>Runs an explicit, bounded keyboard check without initializing the launcher, hooks or settings.</summary>
internal static class KeyboardObservationWindow
{
    /// <summary>Shows current anonymous connection state and writes a bounded report in an explicitly selected diagnostic directory.</summary>
    /// <param name="directory">Owned report directory. Linked paths are refused; no user profile or device identifiers are collected.</param>
    /// <returns>Zero after a normal close or five-minute expiry, or one if the report cannot be written.</returns>
    internal static async Task<int> RunAsync(string directory)
    {
        using var stop = new CancellationTokenSource();
        var heading = new TextBlock { Text = "Keyboard connection check", FontSize = 24, Margin = new(0, 0, 0, 18) };
        var state = new TextBlock { Text = "Reading…", FontSize = 28, Margin = new(0, 0, 0, 16) };
        var hint = new TextBlock { Text = "Connect, disconnect, then reconnect your keyboard.\nOnly connection state is recorded. No typing is captured.\nThis window closes automatically after five minutes.", TextWrapping = TextWrapping.Wrap };
        var close = new Button { Content = "Close", Padding = new(24, 8, 24, 8), HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 24, 0, 0) };
        var content = new StackPanel { Margin = new(28) };
        content.Children.Add(heading); content.Children.Add(state); content.Children.Add(hint); content.Children.Add(close);
        var window = new Window { Title = "ReStart — Keyboard connection check", Width = 530, Height = 340, Content = content, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        close.Click += (_, _) => window.Close(); window.Closed += (_, _) => stop.Cancel();
        var watch = Stopwatch.StartNew();
        var session = Guid.NewGuid().ToString("N");
        var changes = new List<Observation>();
        var samples = 0;
        try
        {
            directory = Path.GetFullPath(directory);
            TaskbarRecoveryStore.CheckLinks(directory); Directory.CreateDirectory(directory);
            var report = Path.Combine(directory, "keyboard-state.json");
            TaskbarRecoveryStore.CheckLinks(report);
            window.Show();
            while (!stop.IsCancellationRequested && watch.Elapsed < TimeSpan.FromMinutes(5))
            {
                var input = await Task.Run(TabletInputDevices.Read);
                var keyboard = input.Keyboard.ToString();
                if (changes.Count == 0 || changes[^1].Keyboard != keyboard)
                    changes.Add(new((int)watch.Elapsed.TotalMilliseconds, keyboard));
                samples++;
                state.Text = input.Keyboard switch { Core.KeyboardPresence.Present => "Keyboard connected", Core.KeyboardPresence.Absent => "No external keyboard detected", _ => "Unable to determine connection" };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, session, samples, keyboard, changes });
                TaskbarRecoveryStore.CheckLinks(report);
                var temporary = Path.Combine(directory, "keyboard-" + session + ".tmp");
                TaskbarRecoveryStore.CheckLinks(temporary);
                using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { output.Write(bytes); output.Flush(flushToDisk: true); }
                File.Move(temporary, report, overwrite: true);
                await Task.Delay(1000, stop.Token);
            }
            return 0;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { return 0; }
        catch (Exception error) when (TaskbarLease.Expected(error) || error is ArgumentException) { return 1; }
        finally { window.Close(); }
    }

    private sealed record Observation(int ElapsedMs, string Keyboard);
}
