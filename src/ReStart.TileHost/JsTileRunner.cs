using System.Text.Json;
using Jint;
using ReStart.Core;

namespace ReStart.TileHost;

public static class JsTileRunner
{
    public static ScriptTileFrame Render(ScriptTileRequest request)
    {
        if (request.Source.Length > ScriptTileProtocol.MaxSource || request.Context.GetRawText().Length > 16384) throw new InvalidDataException("Tile input is too large.");
        // Fresh realm per render. Only JSON crosses the boundary: never expose CLR objects or callbacks.
        using var engine = new Engine(options => options.MaxStatements(20000).LimitRecursion(32).LimitMemory(8_000_000)
            .TimeoutInterval(TimeSpan.FromMilliseconds(100)).DisableStringCompilation());
        engine.SetValue("__contextJson", request.Context.GetRawText());
        engine.Execute("const ctx = JSON.parse(__contextJson); delete globalThis.__contextJson;\n" + request.Source);
        var result = engine.Evaluate("JSON.stringify(render(ctx))").AsString();
        if (result.Length > ScriptTileProtocol.MaxResponse) throw new InvalidDataException("Tile output exceeds 16 KB.");
        var frame = JsonSerializer.Deserialize<ScriptTileFrame>(result, ScriptTileProtocol.Json) ?? throw new InvalidDataException("render(ctx) must return an object.");
        frame.Validate(); return frame;
    }
}
