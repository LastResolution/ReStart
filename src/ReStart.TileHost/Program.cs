using System.Text;
using System.Text.Json;
using System.IO.Pipes;
using ReStart.Core;
using ReStart.TileHost;

try
{
    using var pipe = args is ["--pipe", var name] ? new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous) : null;
    if (pipe != null) await pipe.ConnectAsync(4000);
    else { Console.InputEncoding = Encoding.UTF8; Console.OutputEncoding = new UTF8Encoding(false); }
    using var pipeReader = pipe == null ? null : new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
    using var pipeWriter = pipe == null ? null : new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true);
    TextReader input = pipeReader is null ? Console.In : pipeReader;
    TextWriter outputWriter = pipeWriter is null ? Console.Out : pipeWriter;
    while (await ScriptTileProtocol.ReadLineAsync(input, ScriptTileProtocol.MaxRequest) is string line)
    {
        ScriptTileResponse response;
        try
        {
            var request = JsonSerializer.Deserialize<ScriptTileRequest>(line, ScriptTileProtocol.Json) ?? throw new InvalidDataException(TextCatalog.T(K.ErrorScriptRequestMissing));
            if (request.Context.ValueKind == JsonValueKind.Object && request.Context.TryGetProperty("locale", out var locale) && locale.ValueKind == JsonValueKind.String)
                TextCatalog.SetLanguage(TextCatalog.Resolve("system", locale.GetString()));
            response = new(JsTileRunner.Render(request), null);
        }
        catch (Exception ex)
        {
            var message = ex.GetType().Name + ": " + ex.Message;
            response = new(null, message[..Math.Min(message.Length, 400)]);
        }
        var output = JsonSerializer.Serialize(response, ScriptTileProtocol.Json);
        if (output.Length > ScriptTileProtocol.MaxResponse) output = JsonSerializer.Serialize(new ScriptTileResponse(null, TextCatalog.T(K.ErrorScriptOutputSize)), ScriptTileProtocol.Json);
        await outputWriter.WriteLineAsync(output); await outputWriter.FlushAsync();
    }
}
catch (Exception ex) when (ex is IOException or InvalidDataException) { return 1; }
return 0;
