using System.Xml;
using System.Xml.Linq;

namespace ReStart.Core;

/// <summary>Describes a bounded native tile element; it is data, never executable HTML, XAML or JavaScript.</summary>
/// <param name="Kind">Supported XML element name: text, image, group or subgroup.</param>
/// <param name="Text">Literal bounded text, or empty for non-text elements.</param>
/// <param name="Source">Untrusted image reference. Consumers must resolve it within the owning package's approved local roots.</param>
/// <param name="Style">Microsoft adaptive text style name, or empty for the default.</param>
/// <param name="Align">Text alignment hint, or empty for left alignment.</param>
/// <param name="Placement">Image placement hint, such as background or peek; empty means inline.</param>
/// <param name="Weight">Subgroup column weight, bounded to 1–100.</param>
/// <param name="Wrap">Whether text can wrap to multiple lines.</param>
/// <param name="Children">New immutable child array; consumers must not mutate its elements or replace children.</param>
public sealed record LegacyTileElement(string Kind, string Text, string Source, string Style, string Align, string Placement,
    int Weight, bool Wrap, IReadOnlyList<LegacyTileElement> Children);

/// <summary>Describes one size-specific native tile binding.</summary>
/// <param name="Size">Footprint for which Windows published this binding.</param>
/// <param name="Elements">Bounded parsed elements owned by this binding and not mutated after publication.</param>
public sealed record LegacyTileBinding(TileSize Size, IReadOnlyList<LegacyTileElement> Elements);

/// <summary>Parses the supported, inert part of legacy UWP tile XML without external entities, resource downloads or script evaluation.</summary>
public static class LegacyTileContent
{
    /// <summary>Parses adaptive and legacy size bindings into inert bounded elements.</summary>
    /// <param name="xml">One tile payload of at most 65,536 characters; null, non-tile, invalid or unsupported XML returns an empty list.</param>
    /// <param name="values">Optional current notification data. Exact {key} placeholders resolve to at most 2,048 literal characters; missing keys resolve to empty text. The dictionary is not retained or mutated.</param>
    /// <returns>New bindings for supported sizes. An absent size must fall back to the app icon rather than stretching another size.</returns>
    public static IReadOnlyList<LegacyTileBinding> Read(string? xml, IReadOnlyDictionary<string, string>? values = null)
    {
        if (xml == null || xml.Length is 0 or > 65_536) return [];
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65_536 });
            var document = XDocument.Load(reader);
            if (document.Root?.Name.LocalName != "tile") return [];
            if (document.Descendants().Take(257).Count() > 256) return [];
            var result = new List<LegacyTileBinding>();
            foreach (var binding in document.Root.Elements().Where(e => e.Name.LocalName == "visual")
                .Elements().Where(e => e.Name.LocalName == "binding").Take(8))
            {
                var template = (string?)binding.Attribute("template") ?? "";
                TileSize? size = template switch
                {
                    "TileSmall" => TileSize.Small,
                    "TileMedium" => TileSize.Medium,
                    "TileWide" => TileSize.Wide,
                    "TileLarge" => TileSize.Large,
                    _ when template.StartsWith("TileSquare71x71", StringComparison.Ordinal) => TileSize.Small,
                    _ when template.StartsWith("TileSquare150x150", StringComparison.Ordinal) => TileSize.Medium,
                    _ when template.StartsWith("TileWide310x150", StringComparison.Ordinal) => TileSize.Wide,
                    _ when template.StartsWith("TileSquare310x310", StringComparison.Ordinal) => TileSize.Large,
                    _ => null
                };
                if (size == null || result.Any(b => b.Size == size)) continue;
                var elements = binding.Elements().Select(e => ReadElement(e, 0, values)).OfType<LegacyTileElement>().ToArray();
                if (elements.Length > 0) result.Add(new(size.Value, elements));
            }
            return result;
        }
        catch (XmlException) { return []; }
    }

    private static LegacyTileElement? ReadElement(XElement element, int depth, IReadOnlyDictionary<string, string>? values)
    {
        var kind = element.Name.LocalName;
        if (depth > 4 || kind is not ("text" or "image" or "group" or "subgroup")) return null;
        string Attribute(string name)
        {
            var value = Resolve((string?)element.Attribute(name) ?? "", values);
            return value.Length > 2048 ? value[..2048] : value;
        }
        var text = kind == "text" ? string.Concat(Resolve(element.Value, values).Take(2048).Where(c => !char.IsControl(c) || c is '\n' or '\t')).Trim() : "";
        var weight = int.TryParse(Attribute("hint-weight"), out var parsed) ? Math.Clamp(parsed, 1, 100) : 1;
        var children = kind is "group" or "subgroup" ? element.Elements().Take(32).Select(e => ReadElement(e, depth + 1, values)).OfType<LegacyTileElement>().ToArray() : [];
        return new(kind, text, Attribute("src"), Attribute("hint-style"), Attribute("hint-align"), Attribute("placement"), weight,
            Attribute("hint-wrap").Equals("true", StringComparison.OrdinalIgnoreCase), children);
    }

    private static string Resolve(string value, IReadOnlyDictionary<string, string>? values)
    {
        if (value.Length > 2 && value[0] == '{' && value[^1] == '}')
            return values?.GetValueOrDefault(value[1..^1]) is { } replacement ? replacement[..Math.Min(replacement.Length, 2048)] : "";
        return value;
    }

    /// <summary>Parses a native badge without interpreting it as markup or an executable action.</summary>
    /// <param name="xml">Badge XML of at most 1,024 characters; null or invalid data returns an empty badge.</param>
    /// <returns>A numeric badge capped at 99+, an inert status dot, or empty when hidden/invalid.</returns>
    public static string ReadBadge(string? xml)
    {
        if (xml == null || xml.Length is 0 or > 1024) return "";
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 });
            var root = XDocument.Load(reader).Root;
            if (root?.Name.LocalName != "badge") return "";
            var value = (string?)root.Attribute("value");
            if (uint.TryParse(value, out var count)) return count == 0 ? "" : count > 99 ? "99+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return value is "activity" or "alert" or "available" or "away" or "busy" or "newMessage" or "paused" or "playing" or "unavailable" or "error" or "attention" ? "●" : "";
        }
        catch (XmlException) { return ""; }
    }
}
