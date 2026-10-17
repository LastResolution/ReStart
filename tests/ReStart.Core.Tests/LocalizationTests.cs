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
    [InlineData("system", "it-IT", "en")]
    [InlineData("unknown", "en-US", "en")]
    [InlineData("system", "de-DE", "de")]
    [InlineData("system", "fr-CA", "fr")]
    [InlineData("system", "es-MX", "es")]
    [InlineData("system", "ko-KR", "ko")]
    [InlineData("system", "zh-CN", "zh-Hans")]
    [InlineData("system", "zh-HK", "zh-Hant")]
    [InlineData("system", "zh-TW", "zh-Hant")]
    [InlineData("zh-hant", "en-US", "zh-Hant")]
    [InlineData("fr-FR", "en-US", "fr")]
    public void ResolvesPreferenceAndSystemFallback(string preference, string system, string expected)
        => Assert.Equal(expected, TextCatalog.Resolve(preference, system));

    [Fact]
    public void CatalogsContainTranslationsAndValidPlaceholders()
    {
        Assert.True(TextCatalog.Keys.Count > 100);
        foreach (var key in TextCatalog.Keys)
        {
            var english = TextCatalog.Translate(key, "en");
            Assert.False(string.IsNullOrWhiteSpace(english));
            foreach (var language in TextCatalog.SupportedLanguages)
            {
                var localized = TextCatalog.Translate(key, language);
                Assert.False(string.IsNullOrWhiteSpace(localized));
                var args = Enumerable.Repeat<object>("sample", CompositeFormat.Parse(english).MinimumArgumentCount).ToArray();
                Assert.Equal(CompositeFormat.Parse(english).MinimumArgumentCount, CompositeFormat.Parse(localized).MinimumArgumentCount);
                _ = string.Format(CultureInfo.InvariantCulture, localized, args);
            }
        }
        Assert.Equal("Settings", TextCatalog.Translate(TextKey.NavSettings, "en"));
        Assert.Throws<KeyNotFoundException>(() => TextCatalog.Translate((TextKey)int.MaxValue, "en"));
    }
}
