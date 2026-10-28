using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReStart.App.Tablet;

/// <summary>
/// Personal settings of the tablet edition that must never live in the repository or in
/// bundled packages: %LOCALAPPDATA%\ReStart\tablet\tablet-settings.json. Read when the tablet
/// Home layout is built (first run, <c>apply-layout</c>, Reset layout); installed tiles keep
/// their own copies afterwards. Every field is optional.
/// </summary>
/// <example>
/// <code>
/// {
///   "weather": { "latitude": 52.52, "longitude": 13.41, "name": "Home" },
///   "homeAssistant": {
///     "lights": [ { "entity": "light.living_room", "name": "Living room" } ],
///     "scenesTitle": "Living room scenes",
///     "scenes": [ { "entity": "scene.living_room_relax", "name": "Relax", "icon": "relax" } ]
///   }
/// }
/// </code>
/// </example>
internal sealed class TabletSettings
{
    internal static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart", "tablet", "tablet-settings.json");

    /// <summary>Weather location, or null (the weather tiles then wait for "Configure data sources…").</summary>
    internal (double Latitude, double Longitude, string? Name)? Weather { get; private init; }
    /// <summary>Home Assistant tile settings (lights, scenesTitle, scenes) to copy into the installed package, or null.</summary>
    internal JsonObject? HomeAssistant { get; private init; }

    /// <summary>Reads the file; a missing or invalid file yields empty settings (and a log line when invalid).</summary>
    internal static TabletSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath) || new FileInfo(FilePath).Length > 256 * 1024) return new();
            var root = JsonNode.Parse(File.ReadAllText(FilePath))?.AsObject();
            if (root == null) return new();
            (double, double, string?)? weather = null;
            if (root["weather"] is JsonObject w && w["latitude"]?.GetValue<double>() is double lat && w["longitude"]?.GetValue<double>() is double lon
                && lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
                weather = (lat, lon, w["name"]?.GetValue<string>());
            var home = root["homeAssistant"] as JsonObject;
            return new() { Weather = weather, HomeAssistant = home?.DeepClone().AsObject() };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            TabletLog.Write("settings", "tablet-settings.json ignored: " + ex.Message);
            return new();
        }
    }

    /// <summary>Open-Meteo request used by the weather tiles (public endpoint, no account).</summary>
    internal static string WeatherUrl(double latitude, double longitude) =>
        "https://api.open-meteo.com/v1/forecast?latitude=" + latitude.ToString("0.####", CultureInfo.InvariantCulture)
        + "&longitude=" + longitude.ToString("0.####", CultureInfo.InvariantCulture)
        + "&current=temperature_2m,apparent_temperature,weather_code,is_day,wind_speed_10m,relative_humidity_2m"
        + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,sunrise,sunset"
        + "&hourly=temperature_2m,weather_code,precipitation_probability&forecast_hours=12&forecast_days=6&timezone=auto";
}
