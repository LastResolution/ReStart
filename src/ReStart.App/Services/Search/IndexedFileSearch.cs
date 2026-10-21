using System.Runtime.InteropServices;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Queries the current user's Windows index on one serialized STA, never recursively searching disks.</summary>
internal sealed class IndexedFileSearch
{
    private readonly SemaphoreSlim _gate = new(1);
    internal async Task<(IReadOnlyList<AppEntry> Files, bool Available)> FindAsync(string query, CancellationToken token)
    {
        var sql = IndexedFileQuery.Build(query);
        if (sql == null) return ([], true);
        await _gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var result = new TaskCompletionSource<(IReadOnlyList<AppEntry>, bool)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                object? connection = null, rows = null, fields = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    connection = Activator.CreateInstance(Type.GetTypeFromProgID("ADODB.Connection", true)!);
                    dynamic db = connection!;
                    db.ConnectionTimeout = 3; db.CommandTimeout = 3;
                    db.Open("Provider=Search.CollatorDSO;Extended Properties='Application=Windows';");
                    rows = db.Execute(sql); fields = ((dynamic)rows).Fields;
                    var files = new List<AppEntry>();
                    while (!((dynamic)rows).EOF && files.Count < 20)
                    {
                        token.ThrowIfCancellationRequested();
                        var url = ReadField(fields!, 0); var name = ReadField(fields!, 1);
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile && !uri.IsUnc
                            && (File.Exists(uri.LocalPath) || Directory.Exists(uri.LocalPath)))
                            files.Add(FilePinning.FromPath(uri.LocalPath) with { Name = name.Length == 0 ? Path.GetFileName(uri.LocalPath) : name });
                        ((dynamic)rows).MoveNext();
                    }
                    result.TrySetResult((files, true));
                }
                catch (OperationCanceledException) { result.TrySetCanceled(token); }
                catch (Exception ex) when (ex is COMException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                { result.TrySetResult(([], false)); }
                finally
                {
                    Release(fields);
                    if (rows != null) { try { ((dynamic)rows).Close(); } catch (COMException) { } Release(rows); }
                    if (connection != null) { try { ((dynamic)connection).Close(); } catch (COMException) { } Release(connection); }
                }
            })
            { IsBackground = true, Name = "ReStart indexed files" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            return await result.Task;
        }
        finally { _gate.Release(); }
    }

    private static string ReadField(object fields, int index)
    {
        object? field = null;
        try { field = ((dynamic)fields).Item(index); return ((dynamic)field).Value as string ?? ""; }
        finally { Release(field); }
    }
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
