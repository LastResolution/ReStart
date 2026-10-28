using System.IO.Pipes;
using System.Security.Principal;
using System.Windows.Threading;

namespace ReStart.App.Tablet;

/// <summary>
/// One-line commands from a second process to the running instance:
/// <c>ReStart.exe --tablet-command home|switcher|control-center|dock|close-overlays|...</c>.
/// Used for remote testing and for button bindings (e.g. the Z13 side button through G-Helper).
/// Features add commands with <see cref="Register"/>.
/// </summary>
internal static class TabletCommands
{
    private static readonly Dictionary<string, Action<string?>> Handlers = new(StringComparer.OrdinalIgnoreCase);
    private static CancellationTokenSource? _stop;

    private static string PipeName()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return $"ReStart.Tablet.{sid}.{process.SessionId}";
    }

    /// <summary>Adds or replaces a command. <paramref name="handler"/> runs on the UI thread with the optional argument.</summary>
    internal static void Register(string name, Action<string?> handler) => Handlers[name] = handler;

    /// <summary>Starts listening (primary instance only).</summary>
    internal static void Start(Dispatcher dispatcher)
    {
        if (_stop != null) return;
        Register("home", _ => TabletShell.ShowHome());
        Register("switcher", _ => TabletShell.ShowAppSwitcher());
        Register("control-center", _ => TabletShell.ShowControlCenter());
        Register("dock", _ => TabletShell.ShowDock());
        Register("close-overlays", _ => TabletShell.CloseOverlays());
        _stop = new CancellationTokenSource();
        var token = _stop.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(PipeName(), PipeDirection.In, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(pipe);
                    var line = (await reader.ReadLineAsync(token))?.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    var space = line.IndexOf(' ');
                    var name = space < 0 ? line : line[..space];
                    var argument = space < 0 ? null : line[(space + 1)..];
                    TabletLog.Write("command", line);
                    await dispatcher.InvokeAsync(() =>
                    {
                        if (Handlers.TryGetValue(name, out var handler))
                        {
                            try { handler(argument); }
                            catch (Exception ex) { TabletLog.Write("command", $"{name} failed: {ex}"); }
                        }
                        else TabletLog.Write("command", "unknown: " + name);
                    });
                }
                catch (OperationCanceledException) { break; }
                catch (IOException ex) { TabletLog.Write("command", "pipe: " + ex.Message); await Task.Delay(500, CancellationToken.None); }
            }
        }, CancellationToken.None);
    }

    /// <summary>Stops listening.</summary>
    internal static void Stop() { _stop?.Cancel(); _stop = null; }

    /// <summary>Sends one command line to the running instance (second-process side). Returns false if none is listening.</summary>
    internal static bool Send(string line)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName(), PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(2000);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            writer.WriteLine(line);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException) { return false; }
    }
}
