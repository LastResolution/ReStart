using System.Text;
using System.Text.Json;
using ReStart.Core;
using ReStart.TileHost;

Console.InputEncoding = Encoding.UTF8; Console.OutputEncoding = new UTF8Encoding(false);
try
{
    while (await ScriptTileProtocol.ReadLineAsync(Console.In, ScriptTileProtocol.MaxRequest) is string line)
    {
        ScriptTileResponse response;
        try
        {
            var request = JsonSerializer.Deserialize<ScriptTileRequest>(line, ScriptTileProtocol.Json) ?? throw new InvalidDataException("Missing request.");
            response = new(JsTileRunner.Render(request), null);
        }
        catch (Exception ex)
        {
            var message = ex.GetType().Name + ": " + ex.Message;
            response = new(null, message[..Math.Min(message.Length, 400)]);
        }
        var output = JsonSerializer.Serialize(response, ScriptTileProtocol.Json);
        if (output.Length > ScriptTileProtocol.MaxResponse) output = "{\"error\":\"Tile output exceeds 16 KB.\"}";
        Console.WriteLine(output); Console.Out.Flush();
    }
}
catch (Exception ex) when (ex is IOException or InvalidDataException) { return 1; }
return 0;
