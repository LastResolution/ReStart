using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ReStart.App.Services;

/// <summary>Contains undocumented Shell ABI calls in a bounded, hidden, disposable copy of the app.</summary>
internal static class NativeJumpBroker
{
    private static readonly SemaphoreSlim Workers = new(2);

    internal static async Task<NativeJumpReply> SendAsync(NativeJumpRequest request, CancellationToken cancellation = default)
    {
        if (!NativeJumpContract.ValidRequest(request)) throw new ArgumentException("Invalid native jump-list request.", nameof(request));
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The application executable is unavailable.");
        var start = new ProcessStartInfo(executable)
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
        start.ArgumentList.Add("--jump-list-worker");
        await Workers.WaitAsync(cancellation);
        try { return await ExchangeAsync(request, start, TimeSpan.FromSeconds(request.Operation.StartsWith("invoke-", StringComparison.Ordinal) ? 45 : 5), cancellation); }
        finally { Workers.Release(); }
    }

    internal static async Task<NativeJumpReply> ExchangeAsync(NativeJumpRequest request, ProcessStartInfo start, TimeSpan timeout, CancellationToken cancellation = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(timeout);
        using var child = new Process { StartInfo = start };
        try
        {
            if (!child.Start()) throw new IOException("The native jump-list worker did not start.");
            var output = ReadBoundedAsync(child.StandardOutput.BaseStream, NativeJumpContract.MaximumOutputBytes, deadline.Token);
            // Drain stderr without retaining potentially sensitive native messages.
            var errors = DrainAsync(child.StandardError.BaseStream, deadline.Token);
            var input = JsonSerializer.SerializeToUtf8Bytes(request, NativeJumpContract.Json);
            if (input.Length > NativeJumpContract.MaximumInputBytes) throw new InvalidDataException("The request is too large.");
            await child.StandardInput.BaseStream.WriteAsync(input, deadline.Token);
            child.StandardInput.Close();
            await child.WaitForExitAsync(deadline.Token);
            var bytes = await output; await errors;
            var reply = JsonSerializer.Deserialize<NativeJumpReply>(bytes, NativeJumpContract.Json);
            if (child.ExitCode != 0 || !NativeJumpContract.ValidReply(reply, request.AppId)) throw new InvalidDataException("The worker reply was unavailable or invalid.");
            return reply!;
        }
        finally
        {
            // Kill only this helper. A successfully launched app must survive it.
            try { if (!child.HasExited) child.Kill(); }
            catch (InvalidOperationException) { }
        }
    }

    internal static async Task<byte[]> ReadBoundedAsync(Stream source, int maximum, CancellationToken cancellation)
    {
        using var result = new MemoryStream(); var buffer = new byte[4096];
        while (true)
        {
            var count = await source.ReadAsync(buffer, cancellation);
            if (count == 0) return result.ToArray();
            if (result.Length + count > maximum) throw new InvalidDataException("The native message exceeds its limit.");
            result.Write(buffer, 0, count);
        }
    }
    private static async Task DrainAsync(Stream source, CancellationToken cancellation)
    {
        var buffer = new byte[4096];
        while (await source.ReadAsync(buffer, cancellation) != 0) { }
    }

    internal static async Task<int> RunWorkerAsync()
    {
        // Remain bounded even if the parent crashes during a blocking native
        // call. Terminate only this helper, never its launched application.
        using var watchdog = new Timer(static _ => Process.GetCurrentProcess().Kill(), null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
        NativeJumpRequest? request = null;
        NativeJumpReply reply;
        try
        {
            NativeJumpWorker.PrepareProcess();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var input = await ReadBoundedAsync(Console.OpenStandardInput(), NativeJumpContract.MaximumInputBytes, deadline.Token);
            request = JsonSerializer.Deserialize<NativeJumpRequest>(input, NativeJumpContract.Json);
            if (NativeJumpContract.ValidRequest(request))
                watchdog.Change(TimeSpan.FromSeconds(request!.Operation.StartsWith("invoke-", StringComparison.Ordinal) ? 50 : 10), Timeout.InfiniteTimeSpan);
            reply = NativeJumpContract.ValidRequest(request) ? NativeJumpWorker.Run(request!)
                : new(false, "invalid-request", "", NativeJumpSnapshot.Empty);
        }
        catch (Exception)
        { reply = new(false, "unavailable", request?.AppId ?? "", NativeJumpSnapshot.Empty); }
        var output = JsonSerializer.SerializeToUtf8Bytes(reply, NativeJumpContract.Json);
        if (output.Length > NativeJumpContract.MaximumOutputBytes)
        {
            reply = reply with { Snapshot = reply.Snapshot with { Pinned = reply.Snapshot.Pinned.Select(item => item with { Target = "" }).ToArray() } };
            output = JsonSerializer.SerializeToUtf8Bytes(reply, NativeJumpContract.Json);
        }
        if (output.Length > NativeJumpContract.MaximumOutputBytes)
            output = JsonSerializer.SerializeToUtf8Bytes(new NativeJumpReply(false, "unavailable", request?.AppId ?? "", NativeJumpSnapshot.Empty), NativeJumpContract.Json);
        try { await Console.OpenStandardOutput().WriteAsync(output); return 0; }
        catch (IOException) { return 1; }
    }
}
