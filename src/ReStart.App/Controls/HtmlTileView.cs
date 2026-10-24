using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ReStart.Core;
using ReStart.App.Services;

namespace ReStart.App.Controls;

/// <summary>Hosts a fixed-size browser tile with JSON snapshots and explicit in-memory package resources.</summary>
internal sealed class HtmlTileView : Grid, IDisposable
{
    private static readonly Dictionary<string, Task<CoreWebView2Environment>> Environments = new(StringComparer.OrdinalIgnoreCase);
    private readonly WebView2CompositionControl _browser = new() { DefaultBackgroundColor = System.Drawing.Color.Transparent, IsHitTestVisible = false, Focusable = false };
    private readonly string _dataDirectory, _host;
    private string _html = "", _css = "", _source = "", _signature = "";
    private bool _network, _disposed;
    private bool _viewportPaused;
    internal bool NeedsRecreation { get; private set; }
    internal event Action? RecoveryRequired;
    internal bool ViewportPaused => _viewportPaused;
    private long _sequence;
    private Task? _initialization;
    private CoreWebView2Environment? _environment;
    private TaskCompletionSource<ScriptTileFrame>? _reply;
    private BitmapSource? _image;
    private byte[]? _imageBytes;
    private IReadOnlyDictionary<string, ScriptTilePackages.WebAsset>? _assets;
    private Image? _documentationImage;
    private Image? _transitionImage;
    private ImageSource? _retainedPresentation;
    private int _visibilityGeneration;
    private HwndSource? _offscreenHost;
    private readonly bool _offscreenOnly;
    private CoreWebView2Controller? _headlessController;
    private CoreWebView2? Core => _disposed || NeedsRecreation ? null : _headlessController?.CoreWebView2 ?? _browser.CoreWebView2;
    internal bool BrowserReady => Core != null;
    internal bool OffscreenOnly => _offscreenOnly;
    internal bool HostObjectsDisabled => Core?.Settings.AreHostObjectsAllowed == false;

    internal HtmlTileView(string dataDirectory, string id, bool offscreen = false)
    {
        _offscreenOnly = offscreen;
        _dataDirectory = dataDirectory; _host = "tile-" + id + ".restart.invalid";
        ClipToBounds = true; if (!offscreen) Children.Add(_browser);
        Loaded += (_, _) =>
        {
            if (_disposed) return;
            if (_offscreenHost?.RootVisual == _browser)
            { _offscreenHost.RootVisual = null; _browser.Width = _browser.Height = double.NaN; Children.Insert(0, _browser); }
            ResumePresentation();
        };
        IsVisibleChanged += (_, _) => { if (_disposed) return; if (IsVisible) ResumePresentation(); else _ = PauseWhenStillHiddenAsync(); };
    }

    private void ResumePresentation()
    {
        if (_disposed || _viewportPaused || NeedsRecreation) return;
        _visibilityGeneration++;
        _browser.Visibility = Visibility.Visible;
        if (_headlessController != null) _headlessController.IsVisible = true;
        Core?.Resume();
    }

    private async Task PauseWhenStillHiddenAsync()
    {
        var generation = ++_visibilityGeneration;
        // A synchronous board redraw reparents the same browser. Let that
        // transaction finish before treating its temporary detachment as hide.
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        if (!_disposed && !IsVisible && generation == _visibilityGeneration) await SuspendAsync();
    }

    internal void RememberPresentation()
    {
        _retainedPresentation = _documentationImage?.Source ?? _transitionImage?.Source;
        if (_retainedPresentation != null || ActualWidth < 1 || ActualHeight < 1 || ActualWidth > 4096 || ActualHeight > 4096) return;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this); bitmap.Freeze(); _retainedPresentation = bitmap;
    }

    internal ImageSource? RetainedPresentation => _retainedPresentation ?? _documentationImage?.Source ?? _transitionImage?.Source;
    internal bool HasTransitionPreview => _transitionImage != null;

    internal void ShowTransitionPreview(ImageSource? source)
    {
        if (source == null) return;
        _transitionImage = new() { Source = source, Stretch = Stretch.Fill, IsHitTestVisible = false };
        Children.Add(_transitionImage);
    }

    private async Task InitializeAsync()
    {
        if (!Environments.TryGetValue(_dataDirectory, out var environment))
            Environments[_dataDirectory] = environment = CoreWebView2Environment.CreateAsync(userDataFolder: _dataDirectory);
        var env = await environment;
        _environment = env;
        if (_disposed) return;
        var options = env.CreateCoreWebView2ControllerOptions(); options.IsInPrivateModeEnabled = true;
        if (_offscreenOnly)
        {
            var controller = await env.CreateCoreWebView2ControllerAsync(_offscreenHost!.Handle, options);
            if (_disposed) { controller.Close(); return; }
            _headlessController = controller;
            _headlessController.Bounds = new(0, 0, (int)Math.Ceiling(Width), (int)Math.Ceiling(Height));
            _headlessController.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            _headlessController.IsVisible = true;
        }
        else await _browser.EnsureCoreWebView2Async(env, options);
        if (_disposed) return;
        var core = Core!;
        core.Settings.AreHostObjectsAllowed = false; core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDefaultScriptDialogsEnabled = false; core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false; core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false; core.Settings.IsPinchZoomEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false; core.Settings.IsGeneralAutofillEnabled = false;
        core.NavigationStarting += (_, e) => { if (!Own(e.Uri)) e.Cancel = true; };
        core.FrameNavigationStarting += (_, e) => e.Cancel = true;
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.ProcessFailed += (_, e) =>
        {
            if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.BrowserProcessExited or CoreWebView2ProcessFailedKind.RenderProcessExited or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
            {
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited && Environments.TryGetValue(_dataDirectory, out var cached) && cached.IsCompletedSuccessfully && ReferenceEquals(cached.Result, env))
                    Environments.Remove(_dataDirectory);
                RequireRecovery();
            }
        };
        core.WebMessageReceived += Receive;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested += (_, e) =>
        {
            if (Own(e.Request.Uri))
            {
                var path = Uri.UnescapeDataString(new Uri(e.Request.Uri).AbsolutePath);
                var content = path switch { "/index.html" => _html, "/tile.css" => _css, "/tile.js" => _source, _ => null };
                var bytes = path == "/slideshow.png" ? _imageBytes : content == null ? null : Encoding.UTF8.GetBytes(content);
                var type = path switch { "/tile.css" => "text/css", "/tile.js" => "text/javascript", "/slideshow.png" => "image/png", _ => "text/html" };
                if (_assets?.TryGetValue(path, out var asset) == true) { bytes = asset.Bytes; type = asset.ContentType; }
                var network = _network ? " https: http: " : " ";
                var policy = "default-src 'none'; script-src 'self' 'unsafe-inline'" + network + "; style-src 'self' 'unsafe-inline'" + network
                    + "; img-src 'self' data: blob:" + network + "; font-src 'self' data:" + network + "; connect-src "
                    + (_network ? "'self' https: http: wss: ws:" : "'self'") + "; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; worker-src 'self' blob:;";
                e.Response = env.CreateWebResourceResponse(new MemoryStream(bytes ?? []), bytes == null ? 404 : 200, bytes == null ? "Not Found" : "OK",
                    "Content-Type: " + type + "; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: " + policy + "\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff");
            }
            else if (!_network || !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                e.Response = env.CreateWebResourceResponse(new MemoryStream(), 403, "Blocked", "Content-Type: text/plain");
        };
        await core.AddScriptToExecuteOnDocumentCreatedAsync(Bootstrap);
    }

    private void RequireRecovery()
    {
        if (_disposed || NeedsRecreation) return;
        NeedsRecreation = true; _signature = "";
        _reply?.TrySetException(new InvalidOperationException(TextCatalog.T(K.ErrorWebTileProcess)));
        RecoveryRequired?.Invoke();
    }

    private bool Own(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var value) && value.Scheme == "https" && value.Host == _host && value.IsDefaultPort;

    private void Receive(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Own(e.Source) || e.WebMessageAsJson.Length > 32768) return;
        try
        {
            var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson, ScriptTileProtocol.Json);
            if (!message.TryGetProperty("sequence", out var sequence) || !sequence.TryGetInt64(out var number) || number != _sequence) return;
            if (message.TryGetProperty("error", out var error)) { _reply?.TrySetException(new InvalidDataException(error.GetString())); return; }
            var state = message.GetProperty("state");
            if (state.GetRawText().Length > 8192) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptFrameInvalid));
            var text = message.TryGetProperty("text", out var value) ? value.GetString() ?? "" : "";
            var frame = new ScriptTileFrame { State = state.Clone(), Text = text.Length > 256 ? text[..256] : text };
            if (message.TryGetProperty("frame", out var native) && native.ValueKind == JsonValueKind.Object)
            {
                try { var candidate = native.Deserialize<ScriptTileFrame>(ScriptTileProtocol.Json)!; candidate.Validate(); frame = candidate; frame.State = state.Clone(); }
                catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException) { }
            }
            _reply?.TrySetResult(frame);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or InvalidDataException or KeyNotFoundException)
        { _reply?.TrySetException(new InvalidDataException(TextCatalog.T(K.ErrorScriptFrameInvalid), ex)); }
    }

    internal async Task<(ScriptTileFrame? Frame, string? Error)> RenderAsync(string html, string css, string source, bool network, object context)
    {
        var stage = "initialization";
        try
        {
            if (_disposed || _viewportPaused) return (null, null);
            if (NeedsRecreation) return (null, TextCatalog.T(K.ErrorWebTileProcess));
            _html = html; _css = css; _source = source; _network = network;
            if (_initialization == null && (_offscreenOnly || PresentationSource.FromVisual(_browser) == null))
            {
                // A hidden native host supplies WPF's presentation source during
                // cold startup and offscreen authoring. It never has WS_VISIBLE.
                var json = JsonSerializer.SerializeToElement(context, ScriptTileProtocol.Json);
                var bounds = json.GetProperty("bounds");
                var width = Math.Clamp(bounds.GetProperty("width").GetDouble(), 1, 4096);
                var height = Math.Clamp(bounds.GetProperty("height").GetDouble(), 1, 4096);
                if (_offscreenOnly) { Width = width; Height = height; }
                Children.Remove(_browser); _browser.Width = width; _browser.Height = height;
                _offscreenHost = new HwndSource(new HwndSourceParameters("ReStart tile preparation")
                { Width = (int)Math.Ceiling(width), Height = (int)Math.Ceiling(height), WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x08000080 });
                if (!_offscreenOnly)
                {
                    _offscreenHost.RootVisual = _browser;
                    _browser.Measure(new(width, height)); _browser.Arrange(new Rect(0, 0, width, height));
                }
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
            await (_initialization ??= InitializeAsync()).WaitAsync(TimeSpan.FromSeconds(15));
            if (_disposed) return (null, null);
            var core = Core!; ResumePresentation();
            if (_headlessController != null)
            {
                _headlessController.IsVisible = true;
                var input = JsonSerializer.SerializeToElement(context, ScriptTileProtocol.Json).GetProperty("bounds");
                Width = Math.Clamp(input.GetProperty("width").GetDouble(), 1, 4096); Height = Math.Clamp(input.GetProperty("height").GetDouble(), 1, 4096);
                var bounds = new System.Drawing.Rectangle(0, 0, (int)Math.Ceiling(Width), (int)Math.Ceiling(Height));
                if (_headlessController.Bounds != bounds)
                    _headlessController.Bounds = bounds;
            }
            stage = "navigation";
            var signature = html + "\0" + css + "\0" + source + "\0" + network;
            if (_signature != signature)
            {
                var loaded = new TaskCompletionSource<bool>();
                void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs e) => loaded.TrySetResult(e.IsSuccess);
                core.NavigationCompleted += Completed;
                try
                {
                    core.Navigate("https://" + _host + "/index.html");
                    if (!await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10))) throw new InvalidDataException(TextCatalog.T(K.ErrorWebTileLoad));
                    _signature = signature;
                }
                finally { core.NavigationCompleted -= Completed; }
            }
            if (!Own(core.Source)) throw new InvalidDataException(TextCatalog.T(K.ErrorWebTileLoad));
            if (_headlessController != null)
            {
                // Controller bounds change before Chromium applies its viewport.
                // Wait for the actual dimensions rather than a fixed delay, so
                // responsive layouts also receive the right size under load.
                var bounds = _headlessController.Bounds;
                var ready = $"innerWidth === {bounds.Width} && innerHeight === {bounds.Height}";
                var deadline = Environment.TickCount64 + 2000;
                while (await core.ExecuteScriptAsync(ready) != "true")
                {
                    if (_disposed) return (null, null);
                    if (Environment.TickCount64 >= deadline) throw new TimeoutException(TextCatalog.T(K.ErrorWebTileLoad));
                    await Task.Delay(16);
                }
            }
            _reply = new(); _sequence++;
            stage = "render";
            core.PostWebMessageAsJson(JsonSerializer.Serialize(new { sequence = _sequence, context }, ScriptTileProtocol.Json));
            var frame = await _reply.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (!_offscreenOnly && _transitionImage is { } transition)
            {
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                await Task.Delay(60);
                if (!_disposed && _transitionImage == transition) { Children.Remove(transition); _transitionImage = null; }
            }
            return (frame, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or InvalidDataException or TimeoutException or System.Runtime.InteropServices.COMException or WebView2RuntimeNotFoundException)
        {
            if (ex is TimeoutException or System.Runtime.InteropServices.COMException || _initialization?.IsFaulted == true) RequireRecovery();
            return (null, ex is WebView2RuntimeNotFoundException ? TextCatalog.T(K.ErrorWebTileRuntime) : ex.Message + " (" + stage + ")");
        }
    }

    internal void SetImage(BitmapSource? image)
    {
        if (_image == image) return;
        _image = image; _imageBytes = null;
        if (image == null) return;
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var memory = new MemoryStream(); encoder.Save(memory); _imageBytes = memory.ToArray();
    }
    internal void SetAssets(IReadOnlyDictionary<string, ScriptTilePackages.WebAsset> assets) => _assets = assets;

    /// <summary>Routes a wheel only to this document's scrollable region, retaining ownership at either edge.</summary>
    /// <param name="point">Pointer coordinates in the tile's device-independent pixel viewport.</param>
    /// <param name="delta">Windows wheel delta; positive values move toward the start.</param>
    /// <param name="horizontal">Whether Shift selects the horizontal axis.</param>
    /// <param name="page">Whether to move by a viewport instead of wheel lines.</param>
    /// <returns>True when a region owns the input, including at its scroll limits; false when the board may scroll.</returns>
    internal async Task<bool> ScrollAsync(Point point, int delta, bool horizontal = false, bool page = false)
    {
        if (_disposed || Core == null || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) return false;
        var input = JsonSerializer.Serialize(new { x = point.X, y = point.Y, delta, horizontal, page });
        try
        {
            // A package may mark its primary region to accept a wheel over its
            // fixed heading or footer too. No other tile or browser is consulted.
            return await Core.ExecuteScriptAsync("(" + ScrollDocument + ")(" + input + ")").WaitAsync(TimeSpan.FromSeconds(1)) == "true";
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or System.Runtime.InteropServices.COMException)
        { return true; } // Do not send a delayed or failed browser gesture to the board.
    }

    private const string ScrollDocument = """
        input => {
          const hit = document.elementFromPoint(input.x, input.y);
          if (!hit) return false;
          const wheel = new WheelEvent('wheel', {bubbles:true,cancelable:true,clientX:input.x,clientY:input.y,
            deltaX:input.horizontal ? -input.delta / 120 * 48 : 0, deltaY:input.horizontal ? 0 : -input.delta / 120 * 48});
          if (!hit.dispatchEvent(wheel)) return true;
          const eligible = node => {
            const style = getComputedStyle(node), box = node.getBoundingClientRect();
            return box.width > 0 && box.height > 0 && style.visibility !== 'hidden' &&
              (input.horizontal ? /^(auto|scroll)$/.test(style.overflowX) && node.scrollWidth > node.clientWidth + 1
                : /^(auto|scroll)$/.test(style.overflowY) && node.scrollHeight > node.clientHeight + 1);
          };
          let region = hit;
          while (region && !eligible(region)) region = region.parentElement;
          if (!region) region = [...document.querySelectorAll('[data-restart-scroll]')].find(eligible);
          if (!region) return false;
          const length = input.horizontal ? region.clientWidth : region.clientHeight;
          const amount = -input.delta / 120 * (input.page ? length * .85 : 48);
          region.scrollBy({left: input.horizontal ? amount : 0, top: input.horizontal ? 0 : amount, behavior:'instant'});
          return true;
        }
        """;

    internal async Task SuspendAsync()
    {
        if (_disposed || Core == null) return;
        _browser.Visibility = Visibility.Collapsed;
        if (_headlessController != null) _headlessController.IsVisible = false;
        try { await Core.TrySuspendAsync(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        if (!_disposed && IsVisible && !_viewportPaused) ResumePresentation();
    }

    internal async Task SetViewportPausedAsync(bool paused)
    {
        if (_disposed || _viewportPaused == paused) return;
        _viewportPaused = paused;
        if (paused) await SuspendAsync(); else ResumePresentation();
    }

    internal async Task CaptureAsync(Stream stream)
    {
        // The protocol capture also works before a hidden startup host becomes visible.
        var json = await Core!.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", "{\"format\":\"png\",\"captureBeyondViewport\":true}");
        using var document = JsonDocument.Parse(json);
        var bytes = Convert.FromBase64String(document.RootElement.GetProperty("data").GetString()!);
        await stream.WriteAsync(bytes);
    }
    internal Task<string> InspectAsync(string script) => Core!.ExecuteScriptAsync(script);
    internal uint BrowserProcessId => Core?.BrowserProcessId ?? 0;
    internal IReadOnlyList<int> OwnedProcessIds => _environment?.GetProcessInfos().Select(p => p.ProcessId).ToArray() ?? [];
    internal async Task CrashRendererForVerificationAsync()
    {
        try { await Core!.CallDevToolsProtocolMethodAsync("Page.crash", "{}").WaitAsync(TimeSpan.FromSeconds(4)); }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or System.Runtime.InteropServices.COMException) { }
    }
    internal async Task FreezeForDocumentationAsync()
    {
        if (_disposed || Core == null) return;
        using var memory = new MemoryStream();
        try { await CaptureAsync(memory).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or System.Runtime.InteropServices.COMException)
        { if (_disposed) return; throw new InvalidOperationException(TextCatalog.T(K.ErrorWebTileProcess), ex); }
        if (_disposed) return;
        memory.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = memory; image.EndInit(); image.Freeze();
        if (_documentationImage != null) Children.Remove(_documentationImage);
        _documentationImage = new() { Source = image, Stretch = Stretch.Fill, IsHitTestVisible = false };
        Children.Add(_documentationImage);
        if (_transitionImage != null) { Children.Remove(_transitionImage); _transitionImage = null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; RecoveryRequired = null; _reply?.TrySetException(new InvalidOperationException("Tile view was disposed."));
        try { _browser.Dispose(); } catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        try { _headlessController?.Close(); } catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        _headlessController = null; _offscreenHost?.Dispose(); _offscreenHost = null; _image = null; _imageBytes = null;
    }

    private const string Bootstrap = """
        (() => {
          let current = {}, state = {}, sequence = 0;
          const callbacks = new Set();
          const send = value => chrome.webview.postMessage(value);
          Object.defineProperty(window, 'ReStart', {value: Object.freeze({
            get context() { return current; },
            get imageUrl() { return current.slideshow?.available ? '/slideshow.png?v=' + sequence : null; },
            onUpdate(fn) { callbacks.add(fn); return () => callbacks.delete(fn); },
            setState(value) { state = JSON.parse(JSON.stringify(value)); }
          })});
          chrome.webview.addEventListener('message', async event => {
            const input = event.data; sequence = input.sequence; current = input.context; state = current.state;
            document.documentElement.dataset.size = current.size;
            document.documentElement.lang = current.language;
            document.documentElement.dataset.showTitle = String(current.appearance?.showTitle !== false);
            document.querySelectorAll("[data-restart-title]").forEach(element => element.hidden = current.appearance?.showTitle === false);
            document.documentElement.style.setProperty('--tile-scale', current.bounds.scale);
            try {
              const result = typeof window.render === 'function' ? await window.render(current) : undefined;
              if (result && Object.prototype.hasOwnProperty.call(result, 'state')) state = result.state;
              if (typeof window.present === 'function') await window.present(result, current);
              for (const fn of callbacks) await fn(current);
              if (current.event?.type === 'click') {
                document.dispatchEvent(new CustomEvent('restart:click', {detail: current.event}));
                const x = Number.isFinite(current.event.x) ? current.event.x : innerWidth / 2;
                const y = Number.isFinite(current.event.y) ? current.event.y : innerHeight / 2;
                document.elementFromPoint(x, y)?.dispatchEvent(new MouseEvent('click', {bubbles:true, cancelable:true, clientX:x, clientY:y}));
              }
              const frame = result && typeof result.text === 'string' && JSON.stringify(result).length < 16384 ? result : null;
              send({sequence, state: state ?? {}, text: typeof result?.text === 'string' ? result.text.slice(0,256) : document.title.slice(0,256), frame});
            } catch (error) { send({sequence, error: String(error?.stack || error).slice(0,4096)}); }
          });
          window.addEventListener('error', event => send({sequence, error: String(event.message).slice(0,4096)}));
          window.addEventListener('unhandledrejection', event => send({sequence, error: String(event.reason).slice(0,4096)}));
        })();
        """;
}
