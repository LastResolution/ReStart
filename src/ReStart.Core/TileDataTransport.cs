using System.Net;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace ReStart.Core;

/// <summary>Reads bounded, explicitly configured inputs outside JavaScript; cookies, ambient credentials and automatic redirects are disabled.</summary>
public sealed class TileDataTransport : IDisposable
{
    /// <summary>Maximum decoded HTTP or local-file payload before parsing.</summary>
    public const int MaxResponseBytes = 256 * 1024;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _requests = new(4);
    /// <summary>Creates a shared transport. An injected handler supports deterministic offline verification.</summary>
    /// <param name="handler">Optional caller-supplied HTTP handler for deterministic reads; null creates the restricted default handler.</param>
    public TileDataTransport(HttpMessageHandler? handler = null)
    {
        _http = new(handler ?? new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All
        })
        { Timeout = Timeout.InfiniteTimeSpan };
    }
    internal sealed record Result(JsonElement Value, string? ETag = null, DateTimeOffset? Modified = null, bool NotModified = false);
    internal sealed class Failure(string code, int? status = null) : Exception(code)
    { internal TileDataError Error { get; } = new(code, status); }

    internal async Task<Result> ReadAsync(TileDataDefinition definition, TileDataBinding binding, int budget,
        string? etag, DateTimeOffset? modified, CancellationToken cancellation)
    {
        await _requests.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(8));
            if (binding.Access == "file")
            {
                var info = new FileInfo(binding.Location);
                if (!info.Exists || (info.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new Failure("file");
                if (info.Length > MaxResponseBytes) throw new Failure("tooLarge");
                await using var file = new FileStream(binding.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true);
                var data = await ReadBoundedAsync(file, deadline.Token).ConfigureAwait(false);
                return new(Parse(data, definition, binding, budget));
            }
            var original = new Uri(binding.Location); var address = original;
            for (var redirects = 0; redirects <= 3; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, address);
                request.Headers.UserAgent.ParseAdd("ReStart-LiveTiles/1.0");
                if (binding.Secret.Length > 0) request.Headers.TryAddWithoutValidation(binding.HeaderName, binding.Secret);
                if (etag != null) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
                if (modified != null) request.Headers.IfModifiedSince = modified;
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                {
                    if (response.Headers.Location == null || redirects == 3) throw new Failure("redirect");
                    if (!Uri.TryCreate(address, response.Headers.Location, out var next)) throw new Failure("redirect");
                    if (!TileDataBinding.ValidHttpAddress(next.AbsoluteUri) || next.GetLeftPart(UriPartial.Authority) != original.GetLeftPart(UriPartial.Authority))
                        throw new Failure("redirect");
                    address = next; continue;
                }
                if (response.StatusCode == HttpStatusCode.NotModified) return new(default, etag, modified, true);
                if (!response.IsSuccessStatusCode) throw new Failure("http", (int)response.StatusCode);
                if (response.Content.Headers.ContentLength > MaxResponseBytes) throw new Failure("tooLarge");
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
                var bytes = await ReadBoundedAsync(stream, deadline.Token).ConfigureAwait(false);
                return new(Parse(bytes, definition, binding, budget), response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
            }
            throw new Failure("redirect");
        }
        finally { _requests.Release(); }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, CancellationToken cancellation)
    {
        using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = await input.ReadAsync(buffer, cancellation).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count > MaxResponseBytes) throw new Failure("tooLarge");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static JsonElement Parse(byte[] bytes, TileDataDefinition definition, TileDataBinding binding, int budget)
    {
        if (definition.Format == "feed") return ParseFeed(bytes, budget);
        var jsonBytes = bytes.AsMemory();
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) jsonBytes = jsonBytes[3..];
        using var document = JsonDocument.Parse(jsonBytes, new JsonDocumentOptions { MaxDepth = 12 });
        var value = document.RootElement;
        var pointer = binding.JsonPointer ?? definition.JsonPointer;
        if (pointer.Length > 0)
            foreach (var raw in pointer[1..].Split('/'))
            {
                var part = raw.Replace("~1", "/").Replace("~0", "~");
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(part, out var property)) value = property;
                else if (value.ValueKind == JsonValueKind.Array && int.TryParse(part, out var index) && index >= 0 && index < value.GetArrayLength()
                    && part == index.ToString(System.Globalization.CultureInfo.InvariantCulture)) value = value[index];
                else throw new Failure("selector");
            }
        // Compact before measuring; whitespace in a server response must not consume the worker budget.
        var compact = JsonSerializer.SerializeToElement(value);
        if (compact.GetRawText().Length > budget) throw new Failure("tooLarge");
        return compact;
    }

    private static JsonElement ParseFeed(byte[] bytes, int budget)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxResponseBytes,
            MaxCharactersFromEntities = 0
        });
        var root = XDocument.Load(reader).Root ?? throw new Failure("format");
        var atom = root.Name.LocalName == "feed";
        var container = atom ? root : root.Name.LocalName == "rss" ? root.Elements().FirstOrDefault(e => e.Name.LocalName == "channel") : null;
        if (container == null) throw new Failure("format");
        string Text(XElement? element, int limit)
        {
            var text = WebUtility.HtmlDecode(element?.Value ?? "");
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text[..Math.Min(text.Length, limit)];
        }
        XElement? Field(XElement element, string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        var title = Text(Field(container, "title"), 160);
        var items = new List<object>();
        foreach (var item in container.Elements().Where(e => e.Name.LocalName == (atom ? "entry" : "item")).Take(20))
        {
            var link = atom ? item.Elements().FirstOrDefault(e => e.Name.LocalName == "link" && (e.Attribute("rel") == null || (string?)e.Attribute("rel") == "alternate"))?.Attribute("href")?.Value
                : Field(item, "link")?.Value;
            if (link == null || link.Length > 1024 || !Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) link = "";
            items.Add(new
            {
                title = Text(Field(item, "title"), 160),
                summary = Text(Field(item, atom ? "summary" : "description"), 256),
                url = link,
                published = Text(Field(item, atom ? "updated" : "pubDate"), 80)
            });
            if (JsonSerializer.SerializeToElement(new { title, items }).GetRawText().Length > budget) { items.RemoveAt(items.Count - 1); break; }
        }
        return JsonSerializer.SerializeToElement(new { title, items });
    }

    /// <summary>Releases HTTP connections after all data providers have been stopped.</summary>
    public void Dispose() { _http.Dispose(); }
}
