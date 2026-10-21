using System.Reflection;
using System.Text.Json;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Formats source as data in an offline browser; it never evaluates the user's JavaScript or loads remote plugins.</summary>
internal sealed class TileSourceFormatter(string browserDataDirectory) : IDisposable
{
    private HtmlTileView? _view;

    /// <summary>Formats one whole document and maps the caret through the transformation.</summary>
    /// <param name="source">Unexecuted source text, limited to 256 KiB of UTF-8.</param>
    /// <param name="parser">Fixed parser identifier: babel, html, css or json.</param>
    /// <param name="caret">UTF-16 caret offset within the input; clamped to the source length.</param>
    /// <returns>Formatted source and its corresponding UTF-16 caret position; does not save files or mutate an editor.</returns>
    /// <exception cref="InvalidDataException">Input is oversized, syntax is invalid or the offline formatter cannot load.</exception>
    internal async Task<(string Text, int Caret)> FormatAsync(string source, string parser, int caret)
    {
        if (parser is not ("babel" or "html" or "css" or "json") || System.Text.Encoding.UTF8.GetByteCount(source) > 262144)
            throw new InvalidDataException(TextCatalog.T(K.ErrorScriptRequestSize));
        if (_view == null)
        {
            _view = new(browserDataDirectory, Guid.NewGuid().ToString("N"), offscreen: true);
            var assets = new Dictionary<string, ScriptTilePackages.WebAsset>();
            foreach (var name in new[] { "standalone", "babel", "estree", "html", "postcss" })
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ReStart.Formatter." + name + ".js")
                    ?? throw new InvalidDataException(TextCatalog.T(K.ErrorScriptSourceMissing));
                using var buffer = new MemoryStream(); stream.CopyTo(buffer);
                assets["/assets/" + name + ".js"] = new(buffer.ToArray(), "text/javascript");
            }
            _view.SetAssets(assets);
        }
        const string html = "<html><head><title>Offline formatter</title></head><body><script src='/assets/standalone.js'></script><script src='/assets/babel.js'></script><script src='/assets/estree.js'></script><script src='/assets/html.js'></script><script src='/assets/postcss.js'></script><script src='/tile.js'></script></body></html>";
        const string script = "async function render(ctx){window.formattedResult=await prettier.formatWithCursor(ctx.sourceText,{parser:ctx.parser,plugins:prettierPlugins,cursorOffset:ctx.caret,tabWidth:4,useTabs:false,printWidth:90,endOfLine:'lf'});return {text:'Formatted',state:{}}}";
        var (frame, error) = await _view.RenderAsync(html, "", script, false, new
        { sourceText = source, parser, caret = Math.Clamp(caret, 0, source.Length), state = new { }, bounds = new { width = 120, height = 100, scale = 1 } });
        if (frame == null) throw new InvalidDataException(error ?? TextCatalog.T(K.ErrorScript));
        var json = JsonSerializer.Deserialize<string>(await _view.InspectAsync("JSON.stringify(window.formattedResult)"))!;
        using var result = JsonDocument.Parse(json);
        return (result.RootElement.GetProperty("formatted").GetString()!, result.RootElement.GetProperty("cursorOffset").GetInt32());
    }

    public void Dispose() { _view?.Dispose(); _view = null; }
}
