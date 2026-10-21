using System.Text;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Builds bounded preview input from synthetic metrics and user-entered JSON; never reads connections or host files.</summary>
internal static class TileStudioContext
{
    internal static string Sample(ScriptTileManifest manifest)
    {
        var data = manifest.DataSources.ToDictionary(p => p.Key, p => new
        {
            status = "ready",
            value = new
            {
                text = "Sample data",
                detail = "Preview only",
                progress = 0.65,
                temperature_2m = 21,
                weather_code = 0,
                wind_speed_10m = 8,
                title = "Sample feed",
                items = new[] { new { title = "A new idea takes shape", summary = "This is offline sample content." }, new { title = "Make the desktop yours", summary = "Click the preview to test interactions." } }
            },
            updatedAt = "2026-01-01T12:00:00Z",
            checkedAt = "2026-01-01T12:00:00Z",
            stale = false
        });
        return JsonSerializer.Serialize(new
        {
            system = new SystemMetricsSnapshot(DateTimeOffset.UnixEpoch, 37.2, 8, 16UL * 1024 * 1024 * 1024, 8UL * 1024 * 1024 * 1024, 50, 3600,
                [new("C:\\", 512L * 1024 * 1024 * 1024, 256L * 1024 * 1024 * 1024, 50)], 65536, 8192, 74, false),
            data,
            slideshow = new { available = false, count = 0, index = 0 }
        }, new JsonSerializerOptions(ScriptTileProtocol.Json) { WriteIndented = true });
    }

    internal static JsonElement Build(ScriptTileManifest manifest, TileSize size, double scale, string samples, JsonElement state, bool clicked, DateTimeOffset now,
        string theme = "dark", bool highContrast = false, bool reducedMotion = true, string accent = "#0078D7", bool preview = false, bool showTitle = true)
    {
        if (Encoding.UTF8.GetByteCount(samples) > 16384) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptRequestSize));
        var sample = JsonSerializer.Deserialize<JsonElement>(samples, ScriptTileProtocol.Json);
        if (sample.ValueKind != JsonValueKind.Object) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptRequestSize));
        object? Field(string name, string capability) => manifest.Capabilities.Contains(capability) && sample.TryGetProperty(name, out var value) ? value : null;
        var (width, height) = TileLayout.Dimensions(size);
        return JsonSerializer.SerializeToElement(new
        {
            now = now.ToString("O"),
            locale = TextCatalog.Culture.Name,
            language = TextCatalog.Language,
            time = new { unixMs = now.ToUnixTimeMilliseconds(), monotonicMs = Environment.TickCount64, utc = now.UtcDateTime.ToString("O"), local = now.ToString("O"), offsetMinutes = now.Offset.TotalMinutes, timeZoneId = TimeZoneInfo.Local.Id, preview },
            calendar = new { shortDate = now.ToString("d", TextCatalog.Culture), weekday = now.ToString("dddd", TextCatalog.Culture), month = now.ToString("MMMM", TextCatalog.Culture), day = now.Day },
            @event = clicked ? new { type = "click", unixMs = now.ToUnixTimeMilliseconds() } : null,
            size = size.ToString().ToLowerInvariant(),
            bounds = new { width = width * 52 * scale - 4, height = height * 52 * scale - 4, scale },
            system = Field("system", "system.metrics"),
            data = Field("data", "data.sources"),
            slideshow = Field("slideshow", "images.slideshow"),
            settings = manifest.Settings,
            state,
            title = manifest.LocalizedTitles.GetValueOrDefault(TextCatalog.Language, manifest.Title),
            appearance = new { showTitle },
            theme = new { mode = theme, highContrast, reducedMotion, accent, customColor = (string?)null }
        }, ScriptTileProtocol.Json);
    }
}
