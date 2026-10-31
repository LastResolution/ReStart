using System.Text;
using ReStart.App.Services;

namespace ReStart.Core.Tests;

public class WindowsSettingsCatalogTests
{
    [Fact]
    public void ReadsOnlyDocumentedUrisAndResolvedLabelsWithoutUsingDeepLinkCommands()
    {
        const string xml = "<PCSettings><SearchableContent><SettingIdentity><SettingPaths><Path><PolicyIds>unknown;display</PolicyIds><DeepLink>cmd.exe /c ignored</DeepLink></Path></SettingPaths></SettingIdentity>"
            + "<SettingInformation><Description>@owned-label</Description><HighKeywords>@owned-keywords</HighKeywords></SettingInformation></SearchableContent>"
            + "<SearchableContent><SettingIdentity><PolicyIds>https://example.com;not-a-setting</PolicyIds></SettingIdentity><Description>Ignored</Description></SearchableContent></PCSettings>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var entries = WindowsSettingsCatalog.ReadEntries(input, key => key switch { "@owned-label" => "Display details", "@owned-keywords" => "owned keyword", _ => key });
        var entry = Assert.Single(entries); Assert.Equal("ms-settings:display", entry.App.LaunchTarget);
        Assert.Equal("Display details", Assert.Single(WindowsSettingsCatalog.Find(entries, "owned keyword")).Name);
        Assert.DoesNotContain("cmd", entry.App.LaunchTarget, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsEntitiesAndUnresolvedResourceLabels()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE PCSettings [<!ENTITY x SYSTEM 'file:///C:/ReStart-Owned-Fixture/private.txt'>]><PCSettings>&x;</PCSettings>"));
        Assert.Throws<System.Xml.XmlException>(() => WindowsSettingsCatalog.ReadEntries(input, value => value));
        using var unresolved = new MemoryStream(Encoding.UTF8.GetBytes("<PCSettings><SearchableContent><PolicyIds>display</PolicyIds><Description>@unresolved</Description></SearchableContent></PCSettings>"));
        Assert.Empty(WindowsSettingsCatalog.ReadEntries(unresolved, value => value));
    }

    [Theory]
    [InlineData("::{26EE0668-A00A-44D7-9371-BEB064C98683}\\0\\::{A8A91A66-3A7D-4424-8D24-04E180695C7A}", true)]
    [InlineData("::{21EC2020-3AEA-1069-A2DD-08002B30309D}\\::{A8A91A66-3A7D-4424-8D24-04E180695C7A}", true)]
    [InlineData("shell:AppsFolder\\Owned.App!Main", false)]
    [InlineData("C:\\Owned\\Tool.exe", false)]
    [InlineData("::{26EE0668-A00A-44D7-9371-BEB064C98683}\\0\\cmd.exe", false)]
    [InlineData("::{26EE0668-A00A-44D7-9371-BEB064C98683};calc.exe", false)]
    public void AcceptsOnlyControlPanelNamespacePaths(string path, bool expected)
        => Assert.Equal(expected, WindowsSettingsCatalog.SafeControlPanelPath(path));
}
