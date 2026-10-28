using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReStart.App.Tablet.Home;

/// <summary>Address and long-lived access token the user pasted; stored per user with DPAPI, never in layouts, packages or tile context.</summary>
internal sealed record HomeAssistantConnection(string Url, string Token)
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ReStart:Tablet:HomeAssistant:v1");
    internal const string DefaultUrl = "http://homeassistant.local:8123";

    /// <summary>%LOCALAPPDATA%\ReStart\tablet\home-assistant.dat (or the data folder of a preview run).</summary>
    internal static string PathIn(string dataDirectory) => Path.Combine(dataDirectory, "tablet", "home-assistant.dat");

    /// <summary>Base URLs are plain http(s) origins with an optional path; no user info, query or fragment.</summary>
    internal static bool ValidUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;

    internal static bool ValidToken(string token) => token.Length is >= 20 and <= 4096 && !token.Any(c => char.IsControl(c) || char.IsWhiteSpace(c));

    internal static HomeAssistantConnection? Read(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 16384) return null;
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            try
            {
                var value = JsonSerializer.Deserialize<HomeAssistantConnection>(bytes);
                return value != null && ValidUrl(value.Url) && ValidToken(value.Token) ? value : null;
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        { TabletLog.Write("home-assistant", "connection unreadable: " + ex.GetType().Name); return null; }
    }

    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(this);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser)); File.Move(temporary, path, true); }
        finally { CryptographicOperations.ZeroMemory(bytes); if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static void Delete(string path) { if (File.Exists(path)) File.Delete(path); }

    /// <summary>The token is a secret: keep it out of logs and debugger displays.</summary>
    public override string ToString() => "HomeAssistantConnection(" + Url + ")";
}

/// <summary>One light (or switch) state as shown by a tile.</summary>
internal sealed record HomeEntityState(string Entity, bool On, bool Available, int? Brightness, string? Color);

/// <summary>
/// Polls a few Home Assistant entities over its REST API while a Home tile is visible and runs
/// toggles/scenes. Unreachable (away from home) and rejected-token states are reported to the
/// tile and logged once per transition, with exponential backoff instead of error spam.
/// </summary>
internal sealed class HomeAssistantClient : IDisposable
{
    private static readonly Regex EntityPattern = new("^(light|switch|scene|fan|input_boolean)\\.[a-z0-9_]{1,96}$", RegexOptions.CultureInvariant);
    private readonly string _path;
    private readonly HttpClient _http;
    private readonly object _gate = new();
    private readonly Dictionary<string, HomeEntityState> _states = new(StringComparer.Ordinal);
    private HashSet<string> _wanted = new(StringComparer.Ordinal);
    private HomeAssistantConnection? _connection;
    private bool _loaded;
    private Task? _refresh;
    private long _nextPoll, _failures;
    private string _status = "unconfigured";
    private DateTimeOffset? _updatedAt;

    /// <summary>Raised on a thread-pool thread after states or status change.</summary>
    internal event Action? Updated;

    internal HomeAssistantClient(string dataDirectory)
    {
        _path = HomeAssistantConnection.PathIn(dataDirectory);
        // Connect fails fast on a foreign network (no route/ARP reply), so the tile turns "offline" promptly.
        _http = new HttpClient(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(2.5), PooledConnectionLifetime = TimeSpan.FromMinutes(2), AllowAutoRedirect = false, UseCookies = false })
        { Timeout = TimeSpan.FromSeconds(6) };
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
    }

    internal static bool ValidEntity(string? entity) => entity != null && EntityPattern.IsMatch(entity);

    internal bool IsConfigured { get { EnsureLoaded(); return _connection != null; } }
    internal string? Url => Current?.Url;
    internal HomeAssistantConnection? Current { get { EnsureLoaded(); lock (_gate) return _connection; } }

    private void EnsureLoaded()
    {
        lock (_gate)
        {
            if (_loaded) return;
            _loaded = true; _connection = HomeAssistantConnection.Read(_path);
            _status = _connection == null ? "unconfigured" : "connecting";
        }
    }

    /// <summary>Replaces the saved connection (null forgets it) and refreshes immediately.</summary>
    internal void SetConnection(HomeAssistantConnection? connection)
    {
        if (connection == null) HomeAssistantConnection.Delete(_path); else connection.Save(_path);
        lock (_gate)
        {
            _loaded = true; _connection = connection; _states.Clear(); _updatedAt = null; _failures = 0; _nextPoll = 0;
            _status = connection == null ? "unconfigured" : "connecting";
        }
        TabletLog.Write("home-assistant", connection == null ? "connection removed" : "connection saved for " + connection.Url);
        Updated?.Invoke();
    }

    /// <summary>Current snapshot for the requested entities; schedules a background refresh when due.</summary>
    internal (string Status, IReadOnlyDictionary<string, HomeEntityState> States, DateTimeOffset? UpdatedAt) Snapshot(IEnumerable<string> entities)
    {
        EnsureLoaded();
        lock (_gate)
        {
            foreach (var entity in entities) if (ValidEntity(entity) && !entity.StartsWith("scene.", StringComparison.Ordinal)) _wanted.Add(entity);
            if (_connection != null && _refresh == null && Environment.TickCount64 >= _nextPoll) _refresh = Task.Run(RefreshAsync);
            return (_status, new Dictionary<string, HomeEntityState>(_states), _updatedAt);
        }
    }

    private async Task RefreshAsync()
    {
        HomeAssistantConnection? connection; string[] wanted;
        lock (_gate) { connection = _connection; wanted = _wanted.ToArray(); }
        var status = "online"; var changed = false;
        try
        {
            if (connection == null) return;
            if (wanted.Length == 0) wanted = ["__api__"];
            var results = await Task.WhenAll(wanted.Select(entity => ReadAsync(connection, entity)));
            if (results.Any(r => r.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)) status = "unauthorized";
            else if (results.All(r => r.Status == null)) status = "offline";
            // A single dropped request keeps the last state; only HTTP errors count as an error.
            else if (results.Any(r => r.Status is { } code && code is not (HttpStatusCode.OK or HttpStatusCode.NotFound))) status = "error";
            lock (_gate)
            {
                if (connection != _connection) return; // replaced while reading
                foreach (var result in results.Where(r => r.State != null))
                {
                    if (!_states.TryGetValue(result.State!.Entity, out var previous) || previous != result.State) changed = true;
                    _states[result.State.Entity] = result.State;
                }
                if (status == "online") { _updatedAt = DateTimeOffset.Now; _failures = 0; }
                else _failures++;
                // Visible tiles poll every 8 s at home; failures back off 15 s → 2 min.
                _nextPoll = Environment.TickCount64 + (status == "online" ? 8000 : Math.Min(120000, 15000L << (int)Math.Min(3, _failures - 1)));
                if (status != _status) { TabletLog.Write("home-assistant", _status + " -> " + status); _status = status; changed = true; }
            }
        }
        finally
        {
            lock (_gate) _refresh = null;
            if (changed) Updated?.Invoke();
        }
    }

    private async Task<(HttpStatusCode? Status, HomeEntityState? State)> ReadAsync(HomeAssistantConnection connection, string entity)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint(connection, entity == "__api__" ? "api/" : "api/states/" + entity));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.Token);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode != HttpStatusCode.OK || entity == "__api__") return (response.StatusCode, null);
            if (response.Content.Headers.ContentLength > 65536) return (HttpStatusCode.OK, null);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            var root = document.RootElement;
            var state = root.TryGetProperty("state", out var value) ? value.GetString() ?? "" : "";
            int? brightness = null; string? color = null;
            if (root.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object)
            {
                if (attributes.TryGetProperty("brightness", out var b) && b.ValueKind == JsonValueKind.Number) brightness = (int)Math.Round(b.GetDouble() / 2.55);
                if (attributes.TryGetProperty("rgb_color", out var rgb) && rgb.ValueKind == JsonValueKind.Array && rgb.GetArrayLength() == 3)
                    color = "#" + string.Concat(rgb.EnumerateArray().Select(c => Math.Clamp(c.TryGetInt32(out var n) ? n : 0, 0, 255).ToString("X2")));
            }
            return (HttpStatusCode.OK, new(entity, state == "on", state is not ("unavailable" or "unknown" or ""), brightness, color));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException or InvalidOperationException) { return (null, null); }
    }

    /// <summary>Toggles a light/switch or activates a scene; optimistic so the tile reacts at once.</summary>
    internal async Task<bool> RunAsync(string entity)
    {
        if (!ValidEntity(entity)) return false;
        HomeAssistantConnection? connection;
        lock (_gate) connection = _connection;
        if (connection == null) return false;
        var domain = entity[..entity.IndexOf('.')];
        var service = domain == "scene" ? "scene/turn_on" : domain + "/toggle";
        lock (_gate)
            if (_states.TryGetValue(entity, out var current)) _states[entity] = current with { On = !current.On };
        Updated?.Invoke();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(connection, "api/services/" + service))
            { Content = new StringContent(JsonSerializer.Serialize(new { entity_id = entity }), Encoding.UTF8, "application/json") };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.Token);
            using var response = await _http.SendAsync(request);
            TabletLog.Write("home-assistant", service + " " + entity + " -> " + (int)response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        { TabletLog.Write("home-assistant", service + " " + entity + " failed: " + ex.GetType().Name); return false; }
        finally
        {
            // Scenes change several lights: re-read soon, after HA applied the transition.
            lock (_gate) _nextPoll = Environment.TickCount64 + 600;
            await Task.Delay(700);
            Updated?.Invoke();
        }
    }

    /// <summary>Checks an address and token before saving: null on success, else a short reason.</summary>
    internal async Task<string?> TestAsync(HomeAssistantConnection connection)
    {
        var (status, _) = await ReadAsync(connection, "__api__");
        return status switch
        {
            HttpStatusCode.OK => null,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Home Assistant rejected this token.",
            null => "Home Assistant is not reachable from this network right now.",
            _ => "Home Assistant answered " + (int)status + "."
        };
    }

    private static Uri Endpoint(HomeAssistantConnection connection, string relative) => new(new Uri(connection.Url.TrimEnd('/') + "/"), relative);

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        // Coming home: retry right away instead of waiting out the backoff.
        lock (_gate) { _nextPoll = 0; _failures = 0; }
        Updated?.Invoke();
    }

    public void Dispose() { NetworkChange.NetworkAddressChanged -= OnNetworkChanged; _http.Dispose(); }
}
