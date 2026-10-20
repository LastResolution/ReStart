using System.Buffers.Binary;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReStart.Core;

/// <summary>Returns an acknowledged pin result from the existing launcher process.</summary>
/// <param name="Success">Whether the primary process completed and saved the pin request.</param>
/// <param name="Message">Optional display-safe explanation of the outcome.</param>
public sealed record PinReply(bool Success, string Message = "");

/// <summary>Transfers one bounded path to the existing user/session process without launching the target or writing shared request files.</summary>
[SupportedOSPlatform("windows")]
public sealed class PinRequestChannel : IDisposable
{
    private const int MaximumBytes = 256 * 1024;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _listener;

    /// <summary>Starts a serialized current-user-only listener. The callback acknowledges only after persistence succeeds.</summary>
    /// <param name="identity">User/session instance identity shared with the primary launcher.</param>
    /// <param name="receive">Asynchronous pin handler; returns success only after persistence and marshals any UI work itself.</param>
    public PinRequestChannel(string identity, Func<string, Task<PinReply>> receive) => _listener = ListenAsync(Name(identity), receive);

    private static string Name(string identity) => "ReStart.Pin." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));

    /// <summary>Sends a path and waits for the primary process to acknowledge it; cancellation bounds cold-start and shutdown races.</summary>
    /// <param name="identity">Primary process user/session identity used to derive the pipe name.</param>
    /// <param name="path">Filesystem path to pin; the receiver performs path validation.</param>
    /// <param name="cancellationToken">Token bounding pipe connection, transfer and acknowledgement waits.</param>
    /// <returns>Acknowledgement from the primary process after handling the request.</returns>
    /// <exception cref="InvalidDataException">The payload or acknowledgement exceeds protocol bounds or contains no reply.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels connection, transfer, or acknowledgement.</exception>
    /// <exception cref="IOException">The primary process pipe cannot be opened or disconnects during the request.</exception>
    public static async Task<PinReply> SendAsync(string identity, string path, CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeClientStream(".", Name(identity), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(cancellationToken);
        await WriteAsync(pipe, JsonSerializer.Serialize(path), cancellationToken);
        return JsonSerializer.Deserialize<PinReply>(await ReadAsync(pipe, cancellationToken)) ?? throw new InvalidDataException("Missing pin acknowledgement.");
    }

    private async Task ListenAsync(string name, Func<string, Task<PinReply>> receive)
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                var path = JsonSerializer.Deserialize<string>(await ReadAsync(pipe, deadline.Token));
                var reply = path == null ? new PinReply(false, L.T(K.ErrorPinPath)) : await receive(path).WaitAsync(deadline.Token);
                await WriteAsync(pipe, JsonSerializer.Serialize(reply), deadline.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or ArgumentException) { }
        }
    }

    private static async Task<string> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > MaximumBytes) throw new InvalidDataException("Invalid pin request size.");
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return Encoding.UTF8.GetString(bytes);
    }

    private static async Task WriteAsync(Stream stream, string value, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Pin request is too large.");
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>Stops pending connections and observes background errors without blocking the UI dispatcher.</summary>
    public void Dispose()
    {
        _stop.Cancel();
        _ = _listener.ContinueWith(task => { _ = task.Exception; _stop.Dispose(); }, TaskScheduler.Default);
    }
}
