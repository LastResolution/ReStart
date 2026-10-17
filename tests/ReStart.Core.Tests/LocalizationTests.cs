using System.Globalization;
using System.Text;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void UnsupportedSchemaCarriesVersionWithoutInspectingTranslatedText()
    {
        var error = Assert.Throws<UnsupportedLayoutVersionException>(() => new LayoutDocument { Version = 99 }.Normalize());
        Assert.Equal(99, error.Version);
    }
    [Theory]
    [InlineData("ja", "en-US", "ja")]
    [InlineData("en", "ja-JP", "en")]
    [InlineData("system", "ja-JP", "ja")]
    [InlineData("system", "de-DE", "en")]
    [InlineData("unknown", "en-US", "en")]
    public void ResolvesPreferenceAndSystemFallback(string preference, string system, string expected)
        => Assert.Equal(expected, TextCatalog.Resolve(preference, system));

    [Fact]
    public void CatalogsContainTranslationsAndValidPlaceholders()
    {
        Assert.True(TextCatalog.Keys.Count > 100);
        foreach (var key in TextCatalog.Keys)
        {
            var japanese = TextCatalog.Translate(key, "ja");
            var english = TextCatalog.Translate(key, "en");
            Assert.False(string.IsNullOrWhiteSpace(japanese));
            Assert.False(string.IsNullOrWhiteSpace(english));
            var args = Enumerable.Repeat<object>("sample", CompositeFormat.Parse(japanese).MinimumArgumentCount).ToArray();
            Assert.Equal(CompositeFormat.Parse(japanese).MinimumArgumentCount, CompositeFormat.Parse(english).MinimumArgumentCount);
            _ = string.Format(CultureInfo.InvariantCulture, english, args);
        }
        Assert.Equal("Settings", TextCatalog.Translate(TextKey.NavSettings, "en"));
        Assert.Throws<KeyNotFoundException>(() => TextCatalog.Translate((TextKey)int.MaxValue, "en"));
    }
}
