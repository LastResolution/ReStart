using ReStart.Core;
using L = ReStart.Core.TextCatalog;

namespace ReStart.Core.Tests;

public sealed class AppMenuCommandTests
{
    [Theory]
    [InlineData("OpenFileLocation", "opencontaining")]
    [InlineData("OpenFolderLocation", "opencontaining")]
    [InlineData("opencontaining", "opencontaining")]
    [InlineData("RunAs", "runas")]
    [InlineData("RUNASUSER", "runasuser")]
    [InlineData("Windows.ModernShare", "share")]
    [InlineData("Uninstall", "uninstall")]
    [InlineData("taskbarpin", "taskbarpin")]
    [InlineData("taskbarunpin", "taskbarunpin")]
    [InlineData("copyaspath", "copyaspath")]
    public void ActualWindowsCommandNamesAreRecognized(string windowsVerb, string expected) =>
        Assert.Equal(expected, AppMenuCommands.Normalize(windowsVerb));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Open file location")]
    [InlineData("delete")]
    [InlineData("cut")]
    [InlineData("SevenZipExtractHere")]
    [InlineData("opencontaining /c anything")]
    public void TranslatedLabelsAndUnrelatedExtensionCommandsAreNotInvocable(string? verb) =>
        Assert.Null(AppMenuCommands.Normalize(verb));

    [Fact]
    public void EveryAcceptedNameIsStableWhenNormalizedAgain()
    {
        foreach (var verb in new[] { "OpenFileLocation", "open", "runas", "runasuser", "taskbarpin", "taskbarunpin", "appsettings", "Windows.ModernShare", "uninstall", "properties", "copyaspath" })
        {
            var normalized = AppMenuCommands.Normalize(verb);
            Assert.Equal(normalized, AppMenuCommands.Normalize(normalized));
        }
    }

    [Fact]
    public void EachNewMenuLabelExistsInEveryLanguageWithoutChangingGlobalLanguage()
    {
        var language = L.Language;
        foreach (var tag in L.SupportedLanguages)
            foreach (var key in new[] { TextKey.MenuOpen, TextKey.OpenFolderLocation, TextKey.RunAsDifferentUser, TextKey.PinToTaskbar,
                TextKey.UnpinFromTaskbar, TextKey.AppSettings, TextKey.ShareItem, TextKey.RateAndReview, TextKey.CopyFullPath })
                Assert.False(string.IsNullOrWhiteSpace(L.Translate(key, tag)));
        Assert.Equal(language, L.Language);
    }
}
