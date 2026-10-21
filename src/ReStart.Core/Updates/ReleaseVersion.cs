using System.Globalization;
using System.Text.RegularExpressions;

namespace ReStart.Core;

/// <summary>Compares release versions using numeric components and SemVer prerelease precedence, ignoring build metadata.</summary>
/// <param name="Major">Nonnegative major version component.</param>
/// <param name="Minor">Nonnegative minor version component.</param>
/// <param name="Patch">Nonnegative patch version component.</param>
/// <param name="Prerelease">SemVer prerelease identifiers without a leading hyphen; empty means a stable release.</param>
public sealed record ReleaseVersion(int Major, int Minor, int Patch, string Prerelease = "") : IComparable<ReleaseVersion>
{
    /// <summary>Parses bounded v-prefixed release tags or assembly informational versions.</summary>
    /// <param name="text">Release tag or informational version, at most 128 characters; null is invalid.</param>
    /// <returns>Parsed semantic version with build metadata discarded, or null for invalid, oversized, or unsupported input.</returns>
    public static ReleaseVersion? Parse(string? text)
    {
        if (text == null || text.Length > 128) return null;
        var match = Regex.Match(text, @"\A[vV]?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var major) || !int.TryParse(match.Groups[2].Value, out var minor)
            || !int.TryParse(match.Groups[3].Value, out var patch)) return null;
        var pre = match.Groups[4].Value;
        if (pre.Split('.').Any(p => p.Length > 1 && p.All(char.IsAsciiDigit) && p[0] == '0')) return null;
        return new(major, minor, patch, pre);
    }
    /// <summary>Compares numeric release components, then prerelease identifiers; stable releases sort last.</summary>
    /// <param name="other">Version to compare, or null.</param>
    /// <returns>Negative, zero, or positive according to semantic version precedence; null sorts before this version.</returns>
    public int CompareTo(ReleaseVersion? other)
    {
        if (other == null) return 1;
        var numeric = Major.CompareTo(other.Major);
        if (numeric == 0) numeric = Minor.CompareTo(other.Minor);
        if (numeric == 0) numeric = Patch.CompareTo(other.Patch);
        if (numeric != 0) return numeric;
        if (Prerelease.Length == 0 || other.Prerelease.Length == 0) return (Prerelease.Length == 0).CompareTo(other.Prerelease.Length == 0);
        var left = Prerelease.Split('.'); var right = other.Prerelease.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var a = left[i]; var b = right[i]; var an = a.All(char.IsAsciiDigit); var bn = b.All(char.IsAsciiDigit);
            var comparison = an && bn ? a.Length.CompareTo(b.Length) : an != bn ? (an ? -1 : 1) : 0;
            if (comparison == 0) comparison = StringComparer.Ordinal.Compare(a, b);
            if (comparison != 0) return comparison;
        }
        return left.Length.CompareTo(right.Length);
    }
    /// <summary>Formats a version without a v prefix or build metadata.</summary>
    /// <returns>Major.minor.patch with any prerelease suffix; excludes a v prefix and build metadata.</returns>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}") + (Prerelease.Length == 0 ? "" : "-" + Prerelease);
}
