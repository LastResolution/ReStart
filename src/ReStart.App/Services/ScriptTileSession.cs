using System.Diagnostics;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal sealed class ScriptTileSession(string? workerExecutable = null) : IDisposable
{
    private Process? _process;
    private WorkerJob? _job;
    public JsonElement State { get; private set; } = JsonSerializer.SerializeToElement(new { });
    public DateTimeOffset NextUpdate { get; set; }
    public string? Error { get; private set; }
    public async Task<ScriptTileFrame?> RenderAsync(string source, object context)
    {
        try
        {
            var cold = _process == null || _process.HasExited;
            if (cold)
            {
                Dispose();
                var info = new ProcessStartInfo(workerExecutable ?? Path.Combine(AppContext.BaseDirectory, "TileHost", "ReStart.TileHost.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
                _process = Process.Start(info) ?? throw new InvalidOperationException("Could not start tile worker.");
                _job = new(_process);
            }
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(cold ? 4 : 1));
            var process = _process!;
            var request = new ScriptTileRequest(source, JsonSerializer.SerializeToElement(context, ScriptTileProtocol.Json));
            var line = JsonSerializer.Serialize(request, ScriptTileProtocol.Json);
            if (line.Length > ScriptTileProtocol.MaxRequest) throw new InvalidDataException("Tile request is too large.");
            var exchange = ExchangeAsync(process, line, deadline.Token);
            // Cancellation on a Windows anonymous pipe does not necessarily interrupt an
            // outstanding read. Bound the await itself, then close the worker on timeout.
            _ = exchange.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var result = await exchange.WaitAsync(deadline.Token);
            var response = JsonSerializer.Deserialize<ScriptTileResponse>(result ?? throw new IOException("Tile worker exited."), ScriptTileProtocol.Json) ?? throw new InvalidDataException("Missing tile response.");
            if (response.Frame == null) throw new InvalidDataException(response.Error ?? "Missing tile frame.");
            response.Frame.Validate(); State = response.Frame.State.Clone(); Error = null;
            return response.Frame;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Error = ex is OperationCanceledException ? "Tile worker exceeded its deadline." : ex.Message;
            Dispose(); return null;
        }
    }
    private static async Task<string?> ExchangeAsync(Process process, string line, CancellationToken token)
    {
        await process.StandardInput.WriteLineAsync(line.AsMemory(), token).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(token).ConfigureAwait(false);
        return await ScriptTileProtocol.ReadLineAsync(process.StandardOutput, ScriptTileProtocol.MaxResponse, token).ConfigureAwait(false);
    }
    public void Dispose()
    {
        _job?.Dispose(); _job = null;
        if (_process != null)
        {
            // The job already forbids child processes and kills its members on close.
            try { if (!_process.HasExited) _process.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            _process.Dispose(); _process = null;
        }
    }
}
