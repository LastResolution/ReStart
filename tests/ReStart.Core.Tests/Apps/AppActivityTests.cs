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
}
