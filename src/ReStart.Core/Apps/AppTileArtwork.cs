using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace ReStart.Core;

/// <summary>Describes inert default-tile artwork declared by one installed application.</summary>
/// <param name="Images">Package-relative raster references indexed by tile size.</param>
/// <param name="Background">Declared background color, validated by the presentation layer.</param>
/// <param name="Foreground">Desktop manifest text color, light or dark; empty uses automatic contrast.</param>
/// <param name="NamedSizes">Sizes on which the manifest requests an overlaid application name.</param>
public sealed record AppTileArtwork(IReadOnlyDictionary<TileSize, string> Images, string Background, string Foreground, IReadOnlySet<TileSize> NamedSizes)
{
    /// <summary>Reads only the selected application's visual declarations without resolving XML entities.</summary>
    /// <param name="stream">Readable manifest stream, limited to two million characters.</param>
    /// <param name="applicationId">Exact package Application Id; null selects a desktop VisualElementsManifest.</param>
    /// <returns>Declared tile artwork, or null for absent, malformed or unsupported metadata.</returns>
    public static AppTileArtwork? Read(Stream stream, string? applicationId)
    {
        try
        {
            using var reader = XmlReader.Create(stream, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 });
            var root = XDocument.Load(reader).Root;
            XElement? visual;
            var desktop = applicationId == null;
            if (desktop)
                visual = root?.Name.LocalName == "Application" && root.Name.NamespaceName is "" or "http://schemas.microsoft.com/2013/startmenu" ? root.Elements().FirstOrDefault(e => e.Name.LocalName == "VisualElements"
                    && e.Name.NamespaceName is "" or "http://schemas.microsoft.com/2013/startmenu") : null;
            else
            {
                XNamespace package = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
                XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
                visual = root?.Name == package + "Package" ? root.Element(package + "Applications")?.Elements(package + "Application")
                    .FirstOrDefault(e => (string?)e.Attribute("Id") == applicationId)?.Element(uap + "VisualElements") : null;
            }
            if (visual == null) return null;
            var images = new Dictionary<TileSize, string>();
            var tile = visual.Element(visual.Name.Namespace + "DefaultTile");
            foreach (var (size, attribute) in new[] { (TileSize.Small, desktop ? "Square70x70Logo" : "Square71x71Logo"),
                (TileSize.Medium, "Square150x150Logo"), (TileSize.Wide, "Wide310x150Logo"), (TileSize.Large, "Square310x310Logo") })
            {
                var value = (string?)(visual.Attribute(attribute) ?? tile?.Attribute(attribute));
                if (value is { Length: > 0 and <= 2048 }) images[size] = value;
            }
            if (!images.ContainsKey(TileSize.Small) && images.TryGetValue(TileSize.Medium, out var medium)) images[TileSize.Small] = medium;
            var names = new HashSet<TileSize>();
            if (desktop)
            {
                if (!string.Equals((string?)visual.Attribute("ShowNameOnSquare150x150Logo"), "off", StringComparison.OrdinalIgnoreCase)) names.Add(TileSize.Medium);
            }
            else
            {
                var declared = tile?.Element(visual.Name.Namespace + "ShowNameOnTiles")?.Elements(visual.Name.Namespace + "ShowOn")
                    .Select(e => (string?)e.Attribute("Tile")).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
                foreach (var (size, name) in new[] { (TileSize.Medium, "square150x150Logo"), (TileSize.Wide, "wide310x150Logo"), (TileSize.Large, "square310x310Logo") })
                    if (declared.Contains(name)) names.Add(size);
            }
            return images.Count == 0 ? null : new(images, (string?)visual.Attribute("BackgroundColor") ?? "", (string?)visual.Attribute("ForegroundText") ?? "", names);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or ArgumentException) { return null; }
    }

    /// <summary>Orders file-name variants by language, theme and sufficient pixel resolution without filesystem access.</summary>
    /// <param name="reference">Manifest raster reference's file name.</param>
    /// <param name="candidates">Bounded candidate file names from that reference's own directory.</param>
    /// <param name="basePixels">Manifest image width at scale 100.</param>
    /// <param name="requestedPixels">Actual rendered physical width, including display DPI.</param>
    /// <param name="light">Whether the current surface uses light styling.</param>
    /// <param name="language">Current UI culture used to choose language-qualified assets.</param>
    /// <returns>Matching file names, best first; unknown or incompatible qualifiers are excluded.</returns>
    public static IReadOnlyList<string> RankVariants(string reference, IEnumerable<string> candidates, int basePixels, int requestedPixels, bool light, string language)
    {
        var stem = Path.GetFileNameWithoutExtension(reference); var extension = Path.GetExtension(reference);
        var matches = new List<(string Name, int Language, int Theme, int Pixels)>();
        foreach (var name in candidates.Take(128))
        {
            if (name != Path.GetFileName(name) || !Path.GetExtension(name).Equals(extension, StringComparison.OrdinalIgnoreCase)) continue;
            var candidate = Path.GetFileNameWithoutExtension(name);
            if (!candidate.Equals(stem, StringComparison.OrdinalIgnoreCase) && !candidate.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase)) continue;
            var qualifiers = candidate.Length == stem.Length ? [] : candidate[(stem.Length + 1)..].Split('_');
            var pixels = basePixels; var lang = 0; var theme = 0; var valid = true; var dimension = false;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in qualifiers)
            {
                var split = part.IndexOf('-');
                if (split < 1 || !seen.Add(part[..split])) { valid = false; break; }
                var key = part[..split].ToLowerInvariant(); var value = part[(split + 1)..].ToLowerInvariant();
                switch (key)
                {
                    case "scale": case "targetsize":
                        if (dimension || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 4096) valid = false;
                        else { dimension = true; pixels = key == "scale" ? basePixels * number / 100 : number; }
                        break;
                    case "theme": if (value != (light ? "light" : "dark")) valid = false; else theme = 1; break;
                    case "language":
                        if (language.Equals(value, StringComparison.OrdinalIgnoreCase)) lang = 2;
                        else if (language.StartsWith(value + "-", StringComparison.OrdinalIgnoreCase)) lang = 1;
                        else valid = false;
                        break;
                    case "contrast": if (value != "standard") valid = false; break;
                    case "altform": if (value != "colorful") valid = false; break;
                    default: valid = false; break;
                }
            }
            if (valid && pixels > 0) matches.Add((name, lang, theme, pixels));
        }
        return matches.OrderByDescending(m => m.Language).ThenByDescending(m => m.Theme)
            .ThenBy(m => m.Pixels >= requestedPixels ? 0 : 1).ThenBy(m => m.Pixels >= requestedPixels ? m.Pixels : -m.Pixels)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).Select(m => m.Name).ToArray();
    }
}
