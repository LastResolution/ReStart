using System.Buffers.Binary;
using Microsoft.Win32;
using ReStart.App.Services;

namespace ReStart.Core.Tests;

public class WindowsAppUsageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 0, 0, 0, TimeSpan.Zero);
    private static byte[] Record(uint count = 7, DateTimeOffset? last = null)
    {
        var bytes = new byte[72]; BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), count);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(60), (last ?? Now.AddDays(-1)).ToFileTime()); return bytes;
    }

    [Fact]
    public void ReadsKnownLayoutAndCapsCountsWithoutRetainingSourceBytes()
    {
        Assert.Equal(new WindowsAppUsage(7, Now.AddDays(-1)), UserAssistFormat.Read(Record(), Now));
        Assert.Equal(1_000_000, UserAssistFormat.Read(Record(uint.MaxValue), Now)!.Launches);
        var bytes = Record(); var result = UserAssistFormat.Read(bytes, Now); Array.Clear(bytes);
        Assert.Equal(7, result!.Launches);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(71)]
    [InlineData(73)]
    [InlineData(1024)]
    public void RejectsUnknownRecordLayouts(int size) => Assert.Null(UserAssistFormat.Read(new byte[size], Now));

    [Fact]
    public void RejectsZeroCountsInvalidFiletimesAndFutureObservations()
    {
        Assert.Null(UserAssistFormat.Read(Record(0), Now));
        Assert.Null(UserAssistFormat.Read(Record(1, Now.AddMinutes(6)), Now));
        var bytes = Record(); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(60), long.MaxValue);
        Assert.Null(UserAssistFormat.Read(bytes, Now));
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(60), 0); Assert.Null(UserAssistFormat.Read(bytes, Now));
    }

    [Theory]
    [InlineData("Example.App_123!Main")]
    [InlineData(@"C:\Program Files\Example\Editor.exe")]
    [InlineData(@"C:\Fixture\日本語 & 'Tool'.lnk")]
    public void NameTransformationIsReversibleAndPreservesUnicode(string name)
        => Assert.Equal(name, UserAssistFormat.TransformName(UserAssistFormat.TransformName(name)));

    [Fact]
    public void CombinesCountsWithoutDoubleCountingOrRestoringExcludedApps()
    {
        var a = new AppEntry("a", "Alpha", ""); var b = new AppEntry("b", "Beta", "");
        var activity = new AppActivity(); activity.Observe([a, b], Now); activity.RecordLaunch("a", true, Now);
        var native = new Dictionary<string, WindowsAppUsage> { ["a"] = new(2, Now), ["b"] = new(3, Now.AddDays(-1)), ["unknown"] = new(999, Now) };
        Assert.Equal(new[] { b, a }, activity.Frequent([a, b], native));
        activity.HideFrequent("b"); Assert.Equal(new[] { a }, activity.Frequent([a, b], native));
        Assert.DoesNotContain("Windows", System.Text.Json.JsonSerializer.Serialize(activity), StringComparison.Ordinal);
    }

    [Fact]
    public void CandidatesUseExactIdentitiesAndKnownFolderBoundariesRatherThanExecutableBasenames()
    {
        if (!OperatingSystem.IsWindows()) return;
        var app = new AppEntry("Example.App_123!Main", "Editor", "Example.App_123!Main") { ExecutablePath = @"C:\Owned\Editor.exe", ShortcutPath = @"C:\Owned2\Editor.lnk" };
        var candidates = WindowsUsageHistory.Candidates(app, new Dictionary<string, string> { ["F38BF404-1D43-42F2-9305-67DE0B28FC23"] = @"C:\Owned" });
        Assert.Contains(@"{F38BF404-1D43-42F2-9305-67DE0B28FC23}\Editor.exe", candidates);
        Assert.DoesNotContain(@"{F38BF404-1D43-42F2-9305-67DE0B28FC23}2\Editor.lnk", candidates);
        Assert.DoesNotContain("Editor.exe", candidates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadsOnlyExactOwnedCatalogRecordsAndPreservesRegistry(bool blocked)
    {
        if (!OperatingSystem.IsWindows()) return;
        var owned = @"Software\ReStart.Tests\WindowsUsage\" + Guid.NewGuid().ToString("N");
        try
        {
            using var user = Registry.CurrentUser.CreateSubKey(owned + @"\User");
            using var machine = Registry.CurrentUser.CreateSubKey(owned + @"\Machine");
            const string explorer = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
            using (var prefs = user.CreateSubKey(explorer + @"\Advanced")) prefs.SetValue("Start_TrackProgs", 1);
            if (blocked) using (var policy = machine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer")) policy.SetValue("NoInstrumentation", 1);
            using var count = user.CreateSubKey(explorer + @"\UserAssist\{CEBFF5CD-ACE2-4F4F-9178-9926F41749EA}\Count");
            var app = new AppEntry("Owned.App_123!Main", "Owned app", "Owned.App_123!Main");
            var encoded = UserAssistFormat.TransformName(app.Id); var bytes = Record();
            count.SetValue(encoded, bytes, RegistryValueKind.Binary);
            count.SetValue("UnrelatedPrivateRecord", new byte[128], RegistryValueKind.Binary);
            var snapshot = WindowsUsageHistory.Read(user, machine, [app], new Dictionary<string, string>(), Now);
            Assert.Equal(!blocked, snapshot.Available);
            Assert.Equal(blocked ? 0 : 1, snapshot.Entries.Count);
            if (!blocked) Assert.Equal(7, snapshot.Entries[app.Id].Launches);
            Assert.Equal(bytes, (byte[])count.GetValue(encoded)!); Assert.Equal(2, count.ValueCount);
            using var prefsAfter = user.OpenSubKey(explorer + @"\Advanced"); Assert.Equal(1, prefsAfter!.GetValue("Start_TrackProgs"));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(owned, false); }
    }

    [Fact]
    public void MissingWindowsTrackingStateDoesNotReadOldRecords()
    {
        if (!OperatingSystem.IsWindows()) return;
        var owned = @"Software\ReStart.Tests\WindowsUsage\" + Guid.NewGuid().ToString("N");
        try
        {
            using var user = Registry.CurrentUser.CreateSubKey(owned + @"\User");
            using var machine = Registry.CurrentUser.CreateSubKey(owned + @"\Machine");
            Assert.False(WindowsUsageHistory.Read(user, machine, [], new Dictionary<string, string>(), Now).Available);
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(owned, false); }
    }
}
