using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ReStart.App.Services;

namespace ReStart.Core.Tests;

public class LegacyTileTests
{
    private const string TileXml = "<tile><visual><binding template='TileMedium'><text hint-style='title'>Owned headline</text></binding><binding template='TileSmall'><text>7</text></binding></visual></tile>";

    [Theory]
    [InlineData("TileSmall", TileSize.Small)]
    [InlineData("TileMedium", TileSize.Medium)]
    [InlineData("TileWide", TileSize.Wide)]
    [InlineData("TileLarge", TileSize.Large)]
    [InlineData("TileSquare150x150Text04", TileSize.Medium)]
    [InlineData("TileWide310x150ImageAndText01", TileSize.Wide)]
    public void SelectsOnlyThePublishedSizeAndPreservesInertText(string template, TileSize size)
    {
        var binding = Assert.Single(LegacyTileContent.Read($"<tile><visual><binding template='{template}'><text>Owned &amp; literal</text></binding></visual></tile>"));
        Assert.Equal(size, binding.Size); Assert.Equal("Owned & literal", Assert.Single(binding.Elements).Text);
    }

    [Fact]
    public void RejectsEntitiesOtherNotificationKindsAndOversizedOrUnsupportedPayloads()
    {
        Assert.Empty(LegacyTileContent.Read("<!DOCTYPE tile [<!ENTITY x SYSTEM 'file:///C:/ReStart-Owned-Fixture/secret.txt'>]><tile>&x;</tile>"));
        Assert.Empty(LegacyTileContent.Read("<toast><text>Not a tile</text></toast>"));
        Assert.Empty(LegacyTileContent.Read(new string('x', 65_537)));
        Assert.Empty(LegacyTileContent.Read("<tile><visual><binding template='Unknown'><text>Ignored</text></binding></visual></tile>"));
        Assert.Empty(LegacyTileContent.Read("<tile><visual><binding template='TileMedium'><script>eval()</script></binding></visual></tile>"));
        Assert.Empty(LegacyTileContent.Read("<tile>" + string.Concat(Enumerable.Repeat("<text/>", 256)) + "</tile>"));
    }

    [Theory]
    [InlineData("0", "")]
    [InlineData("7", "7")]
    [InlineData("150", "99+")]
    [InlineData("newMessage", "●")]
    [InlineData("none", "")]
    [InlineData("unknown", "")]
    public void ParsesOnlyInertSupportedBadgeValues(string value, string expected)
        => Assert.Equal(expected, LegacyTileContent.ReadBadge($"<badge value='{value}'/>"));

    [Fact]
    public void ResolvesDataBindingsAsLiteralTextRatherThanMarkupOrCode()
    {
        var xml = "<tile><visual><binding template='TileMedium'><text>{headline}</text><text>{missing}</text></binding></visual></tile>";
        var parsed = LegacyTileContent.Read(xml, new Dictionary<string, string> { ["headline"] = "<script>literal & inert</script>" });
        Assert.Equal("<script>literal & inert</script>", parsed[0].Elements[0].Text); Assert.Equal("", parsed[0].Elements[1].Text);
    }

    [Fact]
    public void ReadsOwnedNotificationDataOnlyForTheMatchedTile()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new NativeDatabase(); var now = DateTimeOffset.UtcNow;
        fixture.Add("Owned.App_123!Main", "tile", TileXml.Replace("Owned headline", "{headline}"), now.AddSeconds(-1));
        fixture.Execute("INSERT INTO NotificationData VALUES(1,'headline','Owned dynamic headline'); INSERT INTO NotificationData VALUES(999,'headline','Unrelated data');");
        var result = LegacyNotificationStore.Read(fixture.Path, ["Owned.App_123!Main"], now);
        Assert.True(result.Available);
        Assert.Equal("Owned dynamic headline", result.Apps["Owned.App_123!Main"].Bindings.Single(b => b.Size == TileSize.Medium).Elements[0].Text);
    }

    [Fact]
    public void NativeReaderBindsAnIdentityInsteadOfInterpretingItAsSql()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new NativeDatabase(); var now = DateTimeOffset.UtcNow;
        const string identity = "Owned' OR 1=1 --!Main";
        fixture.Add(identity, "tile", TileXml, now); fixture.Add("Other.App_123!Main", "tile", TileXml, now);
        using var reader = new ReadOnlySqlite(fixture.Path, default); var rows = 0;
        reader.Query("SELECT n.Payload FROM Notification n JOIN NotificationHandler h ON h.RecordId=n.HandlerId WHERE h.PrimaryId=?1 AND ?2>0 AND ?3>0 LIMIT 16",
            identity, 1, 1, _ => rows++);
        Assert.Equal(1, rows);
    }

    [Fact]
    public void ReadsOnlyRequestedActiveTilesAndBadgesWithoutModifyingTheOwnedDatabase()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new NativeDatabase(); var now = DateTimeOffset.UtcNow;
        fixture.Add("Owned.App_123!Main", "tile", TileXml, now.AddSeconds(-5));
        fixture.Add("Owned.App_123!Main", "badge", "<badge value='12'/>", now.AddSeconds(-4));
        fixture.Add("Owned.App_123!Main", "toast", "<toast>Never read</toast>", now.AddSeconds(-3));
        fixture.Add("Other.App_123!Main", "tile", TileXml.Replace("Owned", "Unrelated"), now);
        fixture.Add("Owned.App_123!Main", "tile", TileXml.Replace("Owned", "Expired"), now.AddSeconds(-2), now.AddSeconds(-1));
        var original = fixture.Bytes();
        var result = LegacyNotificationStore.Read(fixture.Path, ["Owned.App_123!Main"], now);
        Assert.True(result.Available); var app = Assert.Single(result.Apps).Value;
        Assert.Equal("12", app.Badge); Assert.Equal(2, app.Bindings.Count);
        Assert.Equal("Owned headline", app.Bindings.Single(b => b.Size == TileSize.Medium).Elements[0].Text);
        Assert.Equal(original, fixture.Bytes());
        Assert.False(File.Exists(fixture.Path + ".backup"));
    }

    [Fact]
    public void UsesBoundIdentitiesAndSeesCommittedWalUpdates()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new NativeDatabase(); fixture.Execute("PRAGMA journal_mode=WAL");
        var now = DateTimeOffset.UtcNow; const string identity = "Owned.App_123!Main";
        fixture.Add(identity, "tile", TileXml, now.AddSeconds(-2));
        fixture.Add("Other.App_123!Main", "tile", TileXml, now.AddSeconds(-2));
        var first = LegacyNotificationStore.Read(fixture.Path, [identity], now); Assert.True(first.Available); Assert.Single(first.Apps);
        fixture.Add(identity, "tile", TileXml.Replace("Owned headline", "Updated headline"), now.AddSeconds(-1));
        var second = LegacyNotificationStore.Read(fixture.Path, [identity], now);
        Assert.Equal("Updated headline", second.Apps[identity].Bindings.Single(b => b.Size == TileSize.Medium).Elements[0].Text);
        Assert.Empty(LegacyNotificationStore.Read(fixture.Path, ["Owned' OR 1=1 --!Main"], now).Apps);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void RejectsWritesMissingSchemasCorruptDatabasesAndCancellation()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new NativeDatabase();
        using (var reader = new ReadOnlySqlite(fixture.Path, default))
            Assert.Throws<IOException>(() => reader.Query("DELETE FROM Notification", "Owned.App_123!Main", 0, 0, _ => { }));
        Assert.False(LegacyNotificationStore.Read(fixture.Path + ".missing", ["Owned.App_123!Main"], DateTimeOffset.UtcNow).Available);
        Assert.False(File.Exists(fixture.Path + ".missing"));
        var broken = System.IO.Path.Combine(fixture.Directory, "broken.db"); File.WriteAllText(broken, "Owned invalid SQLite fixture.");
        Assert.False(LegacyNotificationStore.Read(broken, ["Owned.App_123!Main"], DateTimeOffset.UtcNow).Available);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => LegacyNotificationStore.Read(fixture.Path, ["Owned.App_123!Main"], DateTimeOffset.UtcNow, cancellation.Token));
    }

    [Fact]
    public void LockedDatabaseReturnsUnavailableWithinTheReadBudget()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new NativeDatabase(); fixture.Execute("BEGIN EXCLUSIVE");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Assert.False(LegacyNotificationStore.Read(fixture.Path, ["Owned.App_123!Main"], DateTimeOffset.UtcNow).Available);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3)); fixture.Execute("ROLLBACK");
    }

    [SupportedOSPlatform("windows")]
    private sealed class NativeDatabase : IDisposable
    {
        private nint _database;
        internal string Directory { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReStart.Tests", "NativeTiles", Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Directory, "owned.db");
        private int _id;
        internal NativeDatabase()
        {
            System.IO.Directory.CreateDirectory(Directory);
            Assert.Equal(0, Open(Path, out _database, 6, 0));
            Execute("CREATE TABLE NotificationHandler(RecordId INTEGER PRIMARY KEY,PrimaryId TEXT UNIQUE);"
                + "CREATE TABLE Notification(\"Order\" INTEGER PRIMARY KEY,HandlerId INTEGER,Type TEXT,Payload BLOB,PayloadType TEXT,ArrivalTime INTEGER,ExpiryTime INTEGER,ExpiresOnReboot TEXT,Id INTEGER);"
                + "CREATE TABLE NotificationData(NotificationId INTEGER,Key TEXT,Value TEXT);");
        }
        internal void Add(string app, string type, string payload, DateTimeOffset arrival, DateTimeOffset? expiry = null)
        {
            static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
            Execute("INSERT OR IGNORE INTO NotificationHandler(PrimaryId) VALUES(" + Quote(app) + ");"
                + "INSERT INTO Notification VALUES(" + ++_id + ",(SELECT RecordId FROM NotificationHandler WHERE PrimaryId=" + Quote(app) + "),"
                + Quote(type) + ",CAST(" + Quote(payload) + " AS BLOB),'Xml'," + arrival.ToFileTime() + "," + (expiry?.ToFileTime() ?? 0) + ",'FALSE'," + _id + ");");
        }
        internal void Execute(string sql) => Assert.Equal(0, Exec(_database, sql, 0, 0, 0));
        internal byte[] Bytes()
        {
            using var source = File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream(); source.CopyTo(memory); return memory.ToArray();
        }
        public void Dispose()
        {
            if (_database != 0) { Close(_database); _database = 0; }
            var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReStart.Tests", "NativeTiles")) + System.IO.Path.DirectorySeparatorChar;
            if (!System.IO.Path.GetFullPath(Directory).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid fixture cleanup path.");
            System.IO.Directory.Delete(Directory, true);
        }
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint database, int flags, nint vfs);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int Exec(nint database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, nint callback, nint context, nint error);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close_v2", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int Close(nint database);
    }
}
