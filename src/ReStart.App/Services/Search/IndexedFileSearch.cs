using System.Runtime.InteropServices;
using System.Reflection;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Queries the current user's Windows index on one serialized STA, never recursively searching disks.</summary>
internal sealed partial class IndexedFileSearch
{
    private static readonly SemaphoreSlim Gate = new(1);
    internal int LastFailureCode { get; private set; }
    internal async Task<(IReadOnlyList<AppEntry> Files, bool Available)> FindAsync(string query, CancellationToken token, bool includeContents = false, bool windowsSyntax = false, int locale = 1033)
    {
        var sql = IndexedFileQuery.Build(query, includeContents);
        if (sql == null) return ([], true);
        await Gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            LastFailureCode = 0;
            var result = new TaskCompletionSource<(IReadOnlyList<AppEntry>, bool)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                object? connection = null, rows = null, fields = null;
                (IReadOnlyList<AppEntry>, bool) outcome = ([], false);
                var cancelled = false;
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (windowsSyntax) sql = WindowsSearchQuery.Build(query, includeContents, locale) ?? sql;
                    connection = Activator.CreateInstance(Type.GetTypeFromProgID("ADODB.Connection", true)!);
                    Set(connection!, "ConnectionTimeout", 3); Set(connection!, "CommandTimeout", 3);
                    Call(connection!, "Open", "Provider=Search.CollatorDSO;Extended Properties='Application=Windows';");
                    rows = Call(connection!, "Execute", sql)!; fields = Get(rows, "Fields");
                    var files = new List<AppEntry>();
                    while (!(bool)Get(rows, "EOF")! && files.Count < IndexedFileQuery.MaximumResults)
                    {
                        token.ThrowIfCancellationRequested();
                        var url = ReadField(fields!, 0); var name = ReadField(fields!, 1);
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile && !uri.IsUnc
                            && (File.Exists(uri.LocalPath) || Directory.Exists(uri.LocalPath)))
                            files.Add(FilePinning.FromPath(uri.LocalPath) with { Name = name.Length == 0 ? Path.GetFileName(uri.LocalPath) : name });
                        Call(rows, "MoveNext");
                    }
                    outcome = (files, true);
                }
                catch (OperationCanceledException) { cancelled = true; }
                catch (Exception ex) when (InteropFailure(ex)) { LastFailureCode = (ex is TargetInvocationException { InnerException: { } inner } ? inner : ex).HResult; }
                finally
                {
                    Release(fields);
                    Close(rows); Close(connection);
                }
                // Complete only after releasing apartment-owned wrappers. A continuation
                // must not start another query while the old recordset is being closed.
                if (cancelled) result.TrySetCanceled(token); else result.TrySetResult(outcome);
            })
            { IsBackground = true, Name = "ReStart indexed files" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            return await result.Task;
        }
        finally { Gate.Release(); }
    }

    private static string ReadField(object fields, int index)
    {
        object? field = null;
        try { field = Get(fields, "Item", index); return field == null ? "" : Get(field, "Value") as string ?? ""; }
        finally { Release(field); }
    }
    // Reflection uses IDispatch directly, avoiding dynamic call-site caches retaining
    // an apartment's COM wrapper after explicit release.
    private static object? Get(object value, string name, params object[] arguments) => value.GetType().InvokeMember(name, BindingFlags.GetProperty, null, value, arguments);
    private static void Set(object value, string name, object argument) => value.GetType().InvokeMember(name, BindingFlags.SetProperty, null, value, [argument]);
    private static object? Call(object value, string name, params object[] arguments) => value.GetType().InvokeMember(name, BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding, null, value, arguments);
    private static bool InteropFailure(Exception ex) => ex is COMException or InvalidComObjectException or InvalidOperationException or TargetInvocationException or MissingMethodException or ArgumentException
        or InvalidCastException or IOException or UnauthorizedAccessException;
    private static void Close(object? value)
    {
        if (value == null) return;
        try { Call(value, "Close"); }
        catch (Exception ex) when (InteropFailure(ex)) { }
        finally { Release(value); }
    }
    private static void Release(object? value)
    {
        try { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        catch (InvalidComObjectException) { }
    }
}
