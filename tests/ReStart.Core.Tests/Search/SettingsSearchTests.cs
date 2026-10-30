using ReStart.App.Services;

namespace ReStart.Core.Tests;

public class SettingsSearchTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ja")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("ko")]
    [InlineData("zh-Hans")]
    [InlineData("zh-Hant")]
    public void ExpandedPagesAreDiscoverableInTheSelectedLanguageAndThroughEnglishAliases(string language)
    {
        var pages = new (TextKey Key, string Target)[]
        {
            (TextKey.WindowsSettingStartupapps, "startupapps"), (TextKey.WindowsSettingDefaultapps, "defaultapps"),
            (TextKey.WindowsSettingPrivacyMicrophone, "privacy-microphone"), (TextKey.WindowsSettingEaseofaccessNarrator, "easeofaccess-narrator"),
            (TextKey.WindowsSettingWindowsupdateHistory, "windowsupdate-history"), (TextKey.WindowsSettingClipboard, "clipboard"),
            (TextKey.WindowsSettingSigninoptions, "signinoptions"), (TextKey.WindowsSettingTaskbar, "taskbar")
        };
        foreach (var (key, target) in pages)
        {
            foreach (var queryLanguage in new[] { language, "en" })
            {
                var results = SettingsSearch.Find(TextCatalog.Translate(key, queryLanguage), language);
                var page = Assert.Single(results, result => result.LaunchTarget == "ms-settings:" + target);
                Assert.Equal(TextCatalog.Translate(key, language), page.Name);
                Assert.Equal("restart:setting:" + target, page.Id);
                Assert.False(page.IsShellApp);
            }
        }
        Assert.All(SettingsSearch.Find("Windows", language), result => Assert.StartsWith("ms-settings:", result.LaunchTarget));
    }
}
