using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReStart.Core;

public sealed class ScriptTileManifest
{
    public int Version { get; set; } = 1;
    public string Title { get; set; } = "JavaScript";
    public int RefreshSeconds { get; set; } = 5;
    public JsonElement Settings { get; set; } = JsonSerializer.SerializeToElement(new { });
    public void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(Title) || Title.Length > 80 || RefreshSeconds is < 1 or > 3600 ||
            Settings.ValueKind != JsonValueKind.Object || Settings.GetRawText().Length > 8192)
            throw new InvalidDataException("Invalid tile manifest (version 1, title 1–80, refreshSeconds 1–3600, settings object ≤8 KB).");
    }
}

public sealed class ScriptTileFrame
{
    public string Text { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Badge { get; set; } = "";
    public string Background { get; set; } = "";
    public string Foreground { get; set; } = "";
    public double? Progress { get; set; }
    public JsonElement State { get; set; } = JsonSerializer.SerializeToElement(new { });
    public void Validate()
    {
        if (Text == null || Text.Length > 256 || Detail == null || Detail.Length > 512 || Badge == null || Badge.Length > 16 ||
            Background == null || Foreground == null || !ValidColor(Background) || !ValidColor(Foreground) ||
            Progress is double p && (!double.IsFinite(p) || p is < 0 or > 1) || State.GetRawText().Length > 8192)
            throw new InvalidDataException("Invalid tile frame. Check text lengths, #RRGGBB colors, progress 0–1 and state ≤8 KB.");
    }
    private static bool ValidColor(string value) => value.Length == 0 || Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);
}

public sealed record ScriptTileRequest(string Source, JsonElement Context);
public sealed record ScriptTileResponse(ScriptTileFrame? Frame, string? Error);
public static class ScriptTileProtocol
{
    public const int MaxSource = 65536, MaxRequest = 131072, MaxResponse = 16384;
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, MaxDepth = 16 };
    public static async Task<string?> ReadLineAsync(TextReader reader, int limit, CancellationToken cancellation = default)
    {
        var line = new StringBuilder(); var buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false) != 0)
        {
            if (buffer[0] == '\n') return line.ToString();
            if (buffer[0] != '\r') line.Append(buffer[0]);
            if (line.Length > limit) throw new InvalidDataException("Tile protocol size limit exceeded.");
        }
        return line.Length == 0 ? null : line.ToString();
    }
}
