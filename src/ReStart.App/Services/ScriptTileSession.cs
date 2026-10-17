using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Owns one isolated worker and bounded request exchanges. Callers serialize requests per session.</summary>
/// <remarks>The outer deadline bounds pipe waits; the job enforces process lifetime and resource limits.</remarks>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal sealed class ScriptTileSession(string? workerExecutable = null) : IDisposable
{
    private Process? _process;
    private WorkerJob? _job;
    private NamedPipeServerStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    public JsonElement State { get; private set; } = JsonSerializer.SerializeToElement(new { });
    public DateTimeOffset NextUpdate { get; set; }
    public string? Error { get; private set; }
    /// <summary>Returns a validated frame, or null after stopping a failed worker and recording its error.</summary>
    public async Task<ScriptTileFrame?> RenderAsync(string source, object context)
    {
        try
        {
            var cold = _process == null || _process.HasExited;
            if (cold)
            {
                Dispose();
                var pipeName = "ReStart.Tile." + Guid.NewGuid().ToString("N");
                _pipe = new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                var info = new ProcessStartInfo(workerExecutable ?? Path.Combine(AppContext.BaseDirectory, "TileHost", "ReStart.TileHost.exe")) { UseShellExecute = false, CreateNoWindow = true };
                info.ArgumentList.Add("--pipe"); info.ArgumentList.Add(pipeName);
                _process = Process.Start(info) ?? throw new InvalidOperationException(TextCatalog.T(K.ErrorScriptWorkerStart));
                _job = new(_process);
                _reader = new(_pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
                _writer = new(_pipe, new UTF8Encoding(false), 4096, leaveOpen: true);
            }
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(cold ? 10 : 1));
            var request = new ScriptTileRequest(source, JsonSerializer.SerializeToElement(context, ScriptTileProtocol.Json));
            var line = JsonSerializer.Serialize(request, ScriptTileProtocol.Json);
            if (line.Length > ScriptTileProtocol.MaxRequest) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptRequestSize));
            var exchange = ExchangeAsync(_pipe!, _reader!, _writer!, line, deadline.Token);
            // Overlapped pipe IO leaves ThreadPool workers free for the deadline timer.
            // Bound the await itself too, then close the job and pipe on timeout.
            _ = exchange.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var result = await exchange.WaitAsync(deadline.Token);
            var response = JsonSerializer.Deserialize<ScriptTileResponse>(result ?? throw new IOException(TextCatalog.T(K.ErrorScriptWorkerExit)), ScriptTileProtocol.Json) ?? throw new InvalidDataException(TextCatalog.T(K.ErrorScriptResponseMissing));
            if (response.Frame == null) throw new InvalidDataException(response.Error ?? TextCatalog.T(K.ErrorScriptFrameMissing));
            response.Frame.Validate(); State = response.Frame.State.Clone(); Error = null;
            return response.Frame;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Error = ex is OperationCanceledException ? TextCatalog.T(K.ErrorScriptDeadline) : ex.Message;
            Dispose(); return null;
        }
    }
    private static async Task<string?> ExchangeAsync(NamedPipeServerStream pipe, StreamReader reader, StreamWriter writer, string line, CancellationToken token)
    {
        if (!pipe.IsConnected) await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
        await writer.WriteLineAsync(line.AsMemory(), token).ConfigureAwait(false);
        await writer.FlushAsync(token).ConfigureAwait(false);
        return await ScriptTileProtocol.ReadLineAsync(reader, ScriptTileProtocol.MaxResponse, token).ConfigureAwait(false);
    }
    /// <summary>Stops the worker while retaining JSON state so hiding the menu does not lose the last successful state.</summary>
    public void Dispose()
    {
        _pipe?.Dispose(); _pipe = null;
        try { _writer?.Dispose(); } catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
        _writer = null;
        _reader?.Dispose(); _reader = null;
        _job?.Dispose(); _job = null;
        if (_process != null)
        {
            // The job already forbids child processes and kills its members on close.
            try { if (!_process.HasExited) _process.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            _process.Dispose(); _process = null;
        }
    }
}
