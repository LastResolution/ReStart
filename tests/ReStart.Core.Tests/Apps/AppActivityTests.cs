namespace ReStart.Core.Tests;

public class AppActivityTests
{
    private static readonly AppEntry A = new("a", "Alpha", ""), B = new("b", "Beta", "");
    [Fact]
    public void BaselinesExistingAppsAndExpiresNewDiscoveries()
    {
        var history = new AppActivity(); var now = DateTimeOffset.UtcNow;
        Assert.True(history.Observe([A], now)); Assert.Empty(history.Recent([A], now));
        Assert.False(history.Observe([], now));
        history.Observe([A, B], now.AddHours(1));
        Assert.Equal([B], history.Recent([A, B], now.AddHours(2)));
        Assert.Empty(history.Recent([A, B], now.AddDays(8)));
        Assert.Empty(history.Recent([A, B], now));
    }
    [Fact]
    public void RecordsOnlyOptedInCatalogAppsAndClearsWithoutRediscovery()
    {
        var history = new AppActivity(); var now = DateTimeOffset.UtcNow;
        history.Observe([A, B], now);
        history.RecordLaunch("a", false, now); history.RecordLaunch("unknown", true, now);
        Assert.Empty(history.Frequent([A, B]));
        history.RecordLaunch("a", true, now); history.RecordLaunch("b", true, now.AddSeconds(1));
        Assert.Equal([B, A], history.Frequent([A, B]));
        history.RecordLaunch("a", true, now.AddSeconds(2)); Assert.Equal(A, history.Frequent([A, B])[0]);
        history.Observe([B], now); Assert.False(history.Entries.ContainsKey("a"));
        history.Clear(); Assert.Empty(history.Frequent([B])); Assert.False(history.Observe([B], now.AddDays(1)));
        Assert.Empty(history.Recent([B], now.AddDays(1)));
    }

    [Fact]
    public void RecentDismissalDoesNotUninstallOrChangeFrequencyAndSurvivesRediscovery()
    {
        var history = new AppActivity(); var now = DateTimeOffset.UtcNow;
        history.Observe([A], now); history.Observe([A, B], now.AddMinutes(1));
        history.RecordLaunch("b", true, now.AddMinutes(2));
        Assert.True(history.DismissRecent("B")); Assert.False(history.DismissRecent("b"));
        Assert.False(history.DismissRecent("unknown"));
        Assert.False(history.Observe([A, B], now.AddMinutes(3)));
        Assert.Empty(history.Recent([A, B], now.AddMinutes(4)));
        Assert.Equal([B], history.Frequent([A, B])); Assert.Equal(2, history.Entries.Count);
    }

    [Fact]
    public void ClearingRecentListClearsAllObservationsRatherThanOnlyThreeVisibleItems()
    {
        var history = new AppActivity(); var now = DateTimeOffset.UtcNow;
        var apps = Enumerable.Range(0, 5).Select(i => new AppEntry($"app{i}", $"App {i}", "")).ToArray();
        history.Observe([A], now); history.Observe(apps.Append(A).ToArray(), now.AddMinutes(1));
        history.RecordLaunch(apps[0].Id, true, now.AddMinutes(2));
        Assert.Equal(3, history.Recent(apps, now.AddMinutes(3)).Count);
        Assert.True(history.ClearRecent()); Assert.False(history.ClearRecent());
        Assert.Empty(history.Recent(apps, now.AddMinutes(3)));
        Assert.Equal([apps[0]], history.Frequent(apps));
    }

    [Fact]
    public void MostUsedExclusionPersistsAcrossLaunchesSerializationAndClearUntilReinstalled()
    {
        var history = new AppActivity(); var now = DateTimeOffset.UtcNow;
        history.Observe([A], now); history.Observe([A, B], now.AddMinutes(1));
        history.RecordLaunch("b", true, now);
        Assert.True(history.HideFrequent("B")); Assert.False(history.HideFrequent("b")); Assert.False(history.HideFrequent("unknown"));
        history = System.Text.Json.JsonSerializer.Deserialize<AppActivity>(System.Text.Json.JsonSerializer.Serialize(history))!;
        history.RecordLaunch("b", true, now.AddMinutes(2));
        Assert.Empty(history.Frequent([A, B])); Assert.Equal([B], history.Recent([A, B], now.AddMinutes(3)));
        history.Clear(); history.RecordLaunch("b", true, now.AddMinutes(4)); Assert.Empty(history.Frequent([A, B]));
        history.Observe([A], now.AddMinutes(5)); history.Observe([A, B], now.AddMinutes(6));
        history.RecordLaunch("b", true, now.AddMinutes(7)); Assert.Equal([B], history.Frequent([A, B]));
    }
}
