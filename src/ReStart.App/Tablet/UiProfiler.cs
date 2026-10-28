using System.Diagnostics;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReStart.App.Tablet;

/// <summary>
/// <c>--tablet-command ui-profile [seconds]</c>: logs what the UI thread spends its time on
/// (dispatcher operations grouped by target method) and how many frames WPF renders. A cheap
/// in-process profiler for "why is it busy while idle" without installing tracing tools.
/// </summary>
internal static class UiProfiler
{
    private static readonly PropertyInfo? NameProperty = typeof(DispatcherOperation).GetProperty("Name", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static void Register(Dispatcher dispatcher) => TabletCommands.Register("ui-profile", argument =>
    {
        var seconds = int.TryParse(argument, out var s) ? Math.Clamp(s, 1, 30) : 5;
        var totals = new Dictionary<string, (double Ms, int Count)>();
        var started = new Dictionary<DispatcherOperation, long>();
        var frames = 0;
        void OnStarted(object? sender, DispatcherHookEventArgs e) => started[e.Operation] = Stopwatch.GetTimestamp();
        void OnCompleted(object? sender, DispatcherHookEventArgs e)
        {
            if (!started.Remove(e.Operation, out var at)) return;
            var ms = Stopwatch.GetElapsedTime(at).TotalMilliseconds;
            var name = NameProperty?.GetValue(e.Operation) as string ?? "?";
            totals[name] = totals.TryGetValue(name, out var t) ? (t.Ms + ms, t.Count + 1) : (ms, 1);
        }
        void OnFrame(object? sender, EventArgs e) => frames++;
        dispatcher.Hooks.OperationStarted += OnStarted;
        dispatcher.Hooks.OperationCompleted += OnCompleted;
        CompositionTarget.Rendering += OnFrame;
        var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            dispatcher.Hooks.OperationStarted -= OnStarted;
            dispatcher.Hooks.OperationCompleted -= OnCompleted;
            CompositionTarget.Rendering -= OnFrame;
            var busy = totals.Values.Sum(v => v.Ms);
            TabletLog.Write("profile", $"{seconds} s: UI thread busy {busy:0} ms ({busy / (seconds * 10):0}%), {frames / (double)seconds:0} frames/s");
            foreach (var (name, (ms, count)) in totals.OrderByDescending(p => p.Value.Ms).Take(12))
                TabletLog.Write("profile", $"  {ms,7:0} ms  {count,5}x  {name}");
        };
        timer.Start();
    });
}
