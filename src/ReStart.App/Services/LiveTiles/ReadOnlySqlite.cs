using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReStart.App.Services;

/// <summary>Owns a short-lived read-only system SQLite connection. No write, backup, extension-loading or recovery operations are exposed.</summary>
[SupportedOSPlatform("windows")]
internal sealed class ReadOnlySqlite : IDisposable
{
    private nint _database;
    private readonly Progress _progress;
    private readonly Authorizer _authorizer;
    private readonly CancellationToken _token;
    private readonly long _deadline;
    internal ReadOnlySqlite(string path, CancellationToken token)
    {
        _token = token; _deadline = Environment.TickCount64 + 1000;
        _progress = _ => token.IsCancellationRequested || Environment.TickCount64 > _deadline ? 1 : 0;
        _authorizer = (_, action, first, second, database, _) => action switch
        {
            21 => 0, // SELECT
            20 when Marshal.PtrToStringUTF8(database) == "main" && Marshal.PtrToStringUTF8(first) is "Notification" or "NotificationHandler" or "NotificationData" => 0,
            31 when (Marshal.PtrToStringUTF8(second) ?? "").ToLowerInvariant() is "length" or "lower" or "coalesce" => 0,
            _ => 1
        };
        try
        {
            if (!File.Exists(path) || Open(path, out _database, 1 | 0x8000, 0) != 0 || DatabaseReadOnly(_database, "main") != 1)
                throw new IOException("Native tile data is unavailable.");
            BusyTimeout(_database, 100);
            Limit(_database, 0, 262_144); Limit(_database, 1, 16_384); Limit(_database, 2, 32); Limit(_database, 3, 32);
            ProgressHandler(_database, 1000, _progress, 0);
            if (SetAuthorizer(_database, _authorizer, 0) != 0) throw new IOException("Native tile data is unavailable.");
        }
        catch { Dispose(); throw; }
    }

    internal void Query(string sql, string appId, long now, long boot, Action<Row> read, int maximumRows = 16)
    {
        nint statement = 0;
        try
        {
            _token.ThrowIfCancellationRequested();
            if (Prepare(_database, sql, -1, out statement, 0) != 0 || StatementReadOnly(statement) != 1
                || BindText(statement, 1, appId, -1, -1) != 0 || BindInt64(statement, 2, now) != 0 || BindInt64(statement, 3, boot) != 0)
                throw new IOException("Native tile query is unavailable.");
            var count = 0; int step;
            while ((step = Step(statement)) == 100)
            {
                _token.ThrowIfCancellationRequested();
                if (++count > Math.Clamp(maximumRows, 1, 64) || Environment.TickCount64 > _deadline) throw new IOException("Native tile query exceeded its bound.");
                read(new(statement));
            }
            if (step != 101) { _token.ThrowIfCancellationRequested(); throw new IOException("Native tile query is unavailable."); }
        }
        finally { if (statement != 0) Finalize(statement); }
    }

    internal readonly record struct Row(nint Statement)
    {
        internal long Integer(int column) => ColumnInteger(Statement, column);
        internal string Text(int column, int maximumBytes = 65_536)
        {
            var length = ColumnBytes(Statement, column);
            if (length is <= 0 || length > maximumBytes) return "";
            var pointer = ColumnBlob(Statement, column);
            if (pointer == 0) return "";
            var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
            try
            {
                if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return new System.Text.UnicodeEncoding(false, true, true).GetString(bytes.AsSpan(2));
                if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return new System.Text.UnicodeEncoding(true, true, true).GetString(bytes.AsSpan(2));
                return new System.Text.UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            }
            catch (System.Text.DecoderFallbackException) { return ""; }
        }
    }

    public void Dispose()
    {
        if (_database != 0) { Close(_database); _database = 0; }
        GC.KeepAlive(_progress); GC.KeepAlive(_authorizer);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Progress(nint context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Authorizer(nint context, int action, nint first, nint second, nint database, nint trigger);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint database, int flags, nint vfs);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close_v2", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int Close(nint database);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_db_readonly", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DatabaseReadOnly(nint database, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int BusyTimeout(nint database, int milliseconds);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_limit", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int Limit(nint database, int category, int limit);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_progress_handler", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void ProgressHandler(nint database, int operations, Progress callback, nint context);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_set_authorizer", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SetAuthorizer(nint database, Authorizer callback, nint context);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int Prepare(nint database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out nint statement, nint tail);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_stmt_readonly", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int StatementReadOnly(nint statement);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int BindText(nint statement, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, int length, nint destroy);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int BindInt64(nint statement, int index, long value);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int Step(nint statement);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int Finalize(nint statement);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ColumnBytes(nint statement, int column);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint ColumnBlob(nint statement, int column);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern long ColumnInteger(nint statement, int column);
}
