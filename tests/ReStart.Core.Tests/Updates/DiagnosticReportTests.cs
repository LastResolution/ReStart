using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class DiagnosticReportTests
{
    [Fact]
    public void WhitelistContainsNoMachinePathsMessagesUrlsOrBuildMetadata()
    {
        const string secret = "PRIVATE-SENTINEL";
        var report = DiagnosticReport.Create("1.6.0+" + secret, secret, new(10, 0, 12345), new(10, 0, 0), secret, secret,
            true, false, -1, (UpdateCheckStatus)999, [DiagnosticCode.UpdatePreparationFailed, (DiagnosticCode)999]);
        Assert.DoesNotContain(secret, report); Assert.DoesNotContain("user", report, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(report); var root = json.RootElement;
        Assert.Equal("1.6.0", root.GetProperty("appVersion").GetString());
        Assert.Equal("unknown", root.GetProperty("runtime").GetString());
        Assert.Equal(0, root.GetProperty("layoutSchema").GetInt32());
        Assert.Equal("not-checked", root.GetProperty("updateStatus").GetString());
        Assert.Single(root.GetProperty("recentCodes").EnumerateArray());
        Assert.Equal(11, root.EnumerateObject().Count());
    }
    [Fact]
    public void SupportsHighContrastAndBoundsTheInMemoryErrorList()
    {
        var report = DiagnosticReport.Create("1.6.0", "win-arm64", null, null, "142.0.1.2 beta", "en", false, true,
            int.MaxValue, UpdateCheckStatus.RateLimited, Enumerable.Repeat(DiagnosticCode.UpdateCheckFailed, 100000));
        using var json = JsonDocument.Parse(report); var root = json.RootElement;
        Assert.Equal("high-contrast", root.GetProperty("theme").GetString());
        Assert.Equal("142.0.1.2", root.GetProperty("webViewVersion").GetString());
        Assert.Equal(8, root.GetProperty("recentCodes").GetArrayLength());
        Assert.Equal(10000, root.GetProperty("layoutSchema").GetInt32());
    }
}
