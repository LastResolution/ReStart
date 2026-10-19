using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReStart.Core;

/// <summary>Defines version 1 of a local JavaScript tile package; the only script entry is tile.js.</summary>
public sealed class ScriptTileManifest
{
    /// <summary>Gets or sets the package schema version; only version 1 is accepted.</summary>
    public int Version { get; set; } = 1;
    /// <summary>Gets or sets the initial tile label, limited to 80 characters.</summary>
    public string Title { get; set; } = "JavaScript";
    /// <summary>Gets or sets optional initial display titles keyed by supported language tag.</summary>
    public Dictionary<string, string> LocalizedTitles { get; set; } = [];
    /// <summary>Gets or sets explicitly requested host capabilities; system.metrics exposes aggregate read-only metrics.</summary>
    public string[] Capabilities { get; set; } = [];
    /// <summary>Gets or sets at most four named host-managed data inputs; connections and credentials are never imported.</summary>
    public Dictionary<string, TileDataDefinition> DataSources { get; set; } = [];
    /// <summary>Gets or sets the visible update interval, from 1 to 3600 seconds.</summary>
    public int RefreshSeconds { get; set; } = 5;
    /// <summary>Gets or sets the JSON object passed as ctx.settings, limited to 8,192 UTF-16 code units of raw JSON.</summary>
    public JsonElement Settings { get; set; } = JsonSerializer.SerializeToElement(new { });
    /// <summary>Rejects an invalid schema or values outside the documented tile API limits.</summary>
    public void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(Title) || Title.Length > 80 || RefreshSeconds is < 1 or > 3600 ||
            Settings.ValueKind != JsonValueKind.Object || Settings.GetRawText().Length > 8192 || LocalizedTitles == null || LocalizedTitles.Count > 16
            || LocalizedTitles.Any(p => !TextCatalog.SupportedLanguages.Contains(p.Key) || string.IsNullOrWhiteSpace(p.Value) || p.Value.Length > 80)
            || Capabilities == null || Capabilities.Length > 3 || Capabilities.Distinct(StringComparer.Ordinal).Count() != Capabilities.Length
            || Capabilities.Any(c => c is not ("system.metrics" or "images.slideshow" or "data.sources"))
            || DataSources == null || DataSources.Count > 4 || DataSources.Any(p => !Regex.IsMatch(p.Key, "^[a-z][a-z0-9_-]{0,31}$", RegexOptions.CultureInvariant) || p.Value == null || !p.Value.IsValid())
            || DataSources.Count > 0 && !Capabilities.Contains("data.sources"))
            throw new InvalidDataException(TextCatalog.T(K.ErrorScriptManifestInvalid));
    }
}

/// <summary>Contains native display data and JSON state returned by render(ctx), never markup or host objects.</summary>
public sealed class ScriptTileFrame
{
    /// <summary>Gets or sets the main text, limited to 256 characters.</summary>
    public string Text { get; set; } = "";
    /// <summary>Gets or sets supporting text, limited to 512 characters and hidden on small tiles.</summary>
    public string Detail { get; set; } = "";
    /// <summary>Gets or sets badge text, limited to 16 characters.</summary>
    public string Badge { get; set; } = "";
    /// <summary>Gets or sets a short heading above the main value, limited to 48 characters.</summary>
    public string Kicker { get; set; } = "";
    /// <summary>Gets or sets an optional second #RRGGBB gradient stop.</summary>
    public string BackgroundEnd { get; set; } = "";
    /// <summary>Gets or sets a #RRGGBB accent for native decoration.</summary>
    public string Accent { get; set; } = "";
    /// <summary>Gets or sets a native decoration kind: empty, ring, bars, sparkline, or dots.</summary>
    public string Visual { get; set; } = "";
    /// <summary>Gets or sets a native arrangement: auto/overlay, centered, split, stacked, or caption.</summary>
    public string Layout { get; set; } = "auto";
    /// <summary>Gets or sets an opaque host image reference: empty or current. Paths and URLs are never accepted.</summary>
    public string Image { get; set; } = "";
    /// <summary>Gets or sets at most 32 finite chart samples, each between zero and one.</summary>
    public double[] Series { get; set; } = [];
    /// <summary>Gets or sets an optional #RRGGBB background color.</summary>
    public string Background { get; set; } = "";
    /// <summary>Gets or sets an optional #RRGGBB foreground color.</summary>
    public string Foreground { get; set; } = "";
    /// <summary>Gets or sets an optional finite progress fraction from zero to one.</summary>
    public double? Progress { get; set; }
    /// <summary>Gets or sets JSON state for the next successful render, limited to 8,192 UTF-16 code units of raw JSON.</summary>
    public JsonElement State { get; set; } = JsonSerializer.SerializeToElement(new { });
    /// <summary>Rejects an invalid schema or values outside the documented tile API limits.</summary>
    public void Validate()
    {
        if (Text == null || Text.Length > 256 || Detail == null || Detail.Length > 512 || Badge == null || Badge.Length > 16 ||
            Background == null || Foreground == null || !ValidColor(Background) || !ValidColor(Foreground) || Kicker == null || Kicker.Length > 48
            || !ValidColor(BackgroundEnd) || !ValidColor(Accent) || Visual is not ("" or "ring" or "bars" or "sparkline" or "dots")
            || Layout is not ("auto" or "overlay" or "centered" or "split" or "stacked" or "caption")
            || Image is not ("" or "current") || Series == null || Series.Length > 32 || Series.Any(value => !double.IsFinite(value) || value is < 0 or > 1) ||
            Progress is double p && (!double.IsFinite(p) || p is < 0 or > 1) || State.GetRawText().Length > 8192)
            throw new InvalidDataException(TextCatalog.T(K.ErrorScriptFrameInvalid));
    }
    private static bool ValidColor(string? value) => value != null && (value.Length == 0 || Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant));
}

/// <summary>Contains script source and JSON-only context sent to an isolated worker.</summary>
public sealed record ScriptTileRequest(string Source, JsonElement Context);
/// <summary>Contains either a validated frame or an error message returned by the worker.</summary>
public sealed record ScriptTileResponse(ScriptTileFrame? Frame, string? Error);
/// <summary>Defines the bounded JSON-lines protocol shared by the UI and tile worker.</summary>
public static class ScriptTileProtocol
{
    /// <summary>Maximum source characters, serialized request characters, and response characters respectively.</summary>
    public const int MaxSource = 65536, MaxRequest = 131072, MaxResponse = 16384;
    /// <summary>Gets the camel-case, depth-bounded serializer settings used only by the tile protocol.</summary>
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, MaxDepth = 16 };
    /// <summary>Reads one line with a strict character limit; returns null at clean EOF. The caller must enforce an outer deadline for blocking pipes.</summary>
    public static async Task<string?> ReadLineAsync(TextReader reader, int limit, CancellationToken cancellation = default)
    {
        var line = new StringBuilder(); var buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false) != 0)
        {
            if (buffer[0] == '\n') return line.ToString();
            if (buffer[0] != '\r') line.Append(buffer[0]);
            if (line.Length > limit) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptProtocolSize));
        }
        return line.Length == 0 ? null : line.ToString();
    }
}
