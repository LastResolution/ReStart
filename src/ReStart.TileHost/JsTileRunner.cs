using System.Text.Json;
using Jint;
using ReStart.Core;

namespace ReStart.TileHost;

/// <summary>Evaluates one render in a fresh, constrained JavaScript realm with JSON-only inputs and outputs.</summary>
public static class JsTileRunner
{
    /// <summary>Executes source and render(ctx), validates the returned frame, and propagates script or limit errors. Process isolation and the outer deadline remain the parent host responsibility.</summary>
    public static ScriptTileFrame Render(ScriptTileRequest request)
    {
        if (request.Source.Length > ScriptTileProtocol.MaxSource || request.Context.GetRawText().Length > 32768) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptInputSize));
        // Fresh realm per render. Only JSON crosses the boundary: never expose CLR objects or callbacks.
        using var engine = new Engine(options => options.MaxStatements(20000).LimitRecursion(32).LimitMemory(8_000_000)
            .TimeoutInterval(TimeSpan.FromMilliseconds(100)).DisableStringCompilation());
        engine.SetValue("__contextJson", request.Context.GetRawText());
        engine.Execute("const ctx = JSON.parse(__contextJson); delete globalThis.__contextJson;\n" + request.Source);
        var result = engine.Evaluate("JSON.stringify(render(ctx))").AsString();
        if (result.Length > ScriptTileProtocol.MaxResponse) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptOutputSize));
        var frame = JsonSerializer.Deserialize<ScriptTileFrame>(result, ScriptTileProtocol.Json) ?? throw new InvalidDataException(TextCatalog.T(K.ErrorScriptReturnObject));
        frame.Validate(); return frame;
    }
}
