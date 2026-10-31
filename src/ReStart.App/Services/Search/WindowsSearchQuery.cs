using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Uses the Windows Search AQS parser on its caller's STA, with a fixed local catalog, projection and result bound.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsSearchQuery
{
    internal static string? Build(string query, bool contents, int locale)
    {
        if (IndexedFileQuery.Build(query) == null) return null;
        ISearchManager? manager = null; ISearchCatalogManager? catalog = null; ISearchQueryHelper? helper = null;
        try
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(locale);
            if (culture.IsNeutralCulture) locale = System.Globalization.CultureInfo.CreateSpecificCulture(culture.Name).LCID;
            manager = (ISearchManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new("7D096C5F-AC08-4F1F-BEB7-5C22C517CE39"), true)!)!;
            manager.GetCatalog("SystemIndex", out catalog); catalog.GetQueryHelper(out helper);
            helper.PutQueryContentLocale((uint)locale); helper.PutQueryKeywordLocale((uint)locale);
            helper.PutQuerySyntax(1); helper.PutQueryTermExpansion(1);
            helper.PutQueryContentProperties(contents ? "System.ItemNameDisplay,System.Search.Contents" : "System.ItemNameDisplay");
            helper.PutQuerySelectColumns("System.ItemUrl,System.ItemNameDisplay");
            helper.PutQueryWhereRestrictions("AND System.ItemUrl LIKE 'file:%'");
            helper.PutQuerySorting("System.Search.Rank DESC,System.DateModified DESC,System.ItemNameDisplay ASC");
            helper.PutQueryMaxResults(IndexedFileQuery.MaximumResults);
            helper.GenerateSQLFromUserQuery(query, out var sql);
            return IsSafeProjection(sql, contents) ? sql : null;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or InvalidOperationException) { return null; }
        finally
        {
            if (helper != null) Marshal.ReleaseComObject(helper);
            if (catalog != null) Marshal.ReleaseComObject(catalog);
            if (manager != null) Marshal.ReleaseComObject(manager);
        }
    }

    // AQS may explicitly name content properties even if ordinary terms search filenames.
    // Refuse those generated predicates when content matching is disabled.
    internal static bool IsSafeProjection(string sql, bool contents)
    {
        if (sql.Length > 32_768 || !Regex.IsMatch(sql, "FROM\\s+\"?SystemIndex\"?\\s+WHERE", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || !sql.Contains("System.ItemUrl LIKE 'file:%'", StringComparison.OrdinalIgnoreCase)) return false;
        if (contents) return true;
        string[] allowed = ["System.ItemUrl", "System.ItemNameDisplay", "System.FileName", "System.FileExtension", "System.ItemType",
            "System.ItemTypeText", "System.Kind", "System.Size", "System.DateModified", "System.DateCreated", "System.Search.Rank"];
        return !Regex.IsMatch(sql, "(?:CONTAINS|FREETEXT)\\s*\\(\\s*(?:\\([^)]*\\*[^)]*\\)|[\"']?\\*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            && Regex.Matches(sql, @"System\.[A-Za-z.]+").All(m => allowed.Contains(m.Value, StringComparer.OrdinalIgnoreCase));
    }

    // Vtable order and IIDs follow the public Windows SDK SearchAPI.h. Unused
    // mutating catalog members are declared for ABI alignment and never called.
    [ComImport, Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISearchManager
    {
        void GetIndexerVersionStr(out nint value);
        void GetIndexerVersion(out uint major, out uint minor);
        void GetParameter([MarshalAs(UnmanagedType.LPWStr)] string name, out nint value);
        void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string name, nint value);
        void GetProxyName(out nint value);
        void GetBypassList(out nint value);
        void SetProxy(int access, [MarshalAs(UnmanagedType.Bool)] bool local, uint port, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string bypass);
        void GetCatalog([MarshalAs(UnmanagedType.LPWStr)] string name, out ISearchCatalogManager catalog);
    }

    [ComImport, Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF50"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISearchCatalogManager
    {
        void GetName(out nint name);
        void GetParameter([MarshalAs(UnmanagedType.LPWStr)] string name, out nint value);
        void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string name, nint value);
        void GetCatalogStatus(out int status, out int reason);
        void Reset(); void Reindex();
        void ReindexMatchingURLs([MarshalAs(UnmanagedType.LPWStr)] string pattern);
        void ReindexSearchRoot([MarshalAs(UnmanagedType.LPWStr)] string root);
        void PutConnectTimeout(uint value); void GetConnectTimeout(out uint value);
        void PutDataTimeout(uint value); void GetDataTimeout(out uint value);
        void NumberOfItems(out int count); void NumberOfItemsToIndex(out int count, out int queue, out int priority);
        void URLBeingIndexed(out nint url);
        void GetURLIndexingState([MarshalAs(UnmanagedType.LPWStr)] string url, out uint state);
        void GetPersistentItemsChangedSink(out nint sink);
        void RegisterViewForNotification([MarshalAs(UnmanagedType.LPWStr)] string view, nint sink, out uint cookie);
        void GetItemsChangedSink(nint site, ref Guid iid, out nint value, out Guid signature, out Guid checkpoint, out uint number);
        void UnregisterViewForNotification(uint cookie);
        void SetExtensionClusion([MarshalAs(UnmanagedType.LPWStr)] string extension, [MarshalAs(UnmanagedType.Bool)] bool exclude);
        void EnumerateExcludedExtensions(out nint extensions);
        void GetQueryHelper(out ISearchQueryHelper helper);
    }

    [ComImport, Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISearchQueryHelper
    {
        void GetConnectionString(out nint connection);
        void PutQueryContentLocale(uint locale); void GetQueryContentLocale(out uint locale);
        void PutQueryKeywordLocale(uint locale); void GetQueryKeywordLocale(out uint locale);
        void PutQueryTermExpansion(int expansion); void GetQueryTermExpansion(out int expansion);
        void PutQuerySyntax(int syntax); void GetQuerySyntax(out int syntax);
        void PutQueryContentProperties([MarshalAs(UnmanagedType.LPWStr)] string properties); void GetQueryContentProperties(out nint properties);
        void PutQuerySelectColumns([MarshalAs(UnmanagedType.LPWStr)] string columns); void GetQuerySelectColumns(out nint columns);
        void PutQueryWhereRestrictions([MarshalAs(UnmanagedType.LPWStr)] string restrictions); void GetQueryWhereRestrictions(out nint restrictions);
        void PutQuerySorting([MarshalAs(UnmanagedType.LPWStr)] string sorting); void GetQuerySorting(out nint sorting);
        void GenerateSQLFromUserQuery([MarshalAs(UnmanagedType.LPWStr)] string query, [MarshalAs(UnmanagedType.LPWStr)] out string sql);
        void WriteProperties(uint item, uint count, nint columns, nint values, nint modified);
        void PutQueryMaxResults(int maximum); void GetQueryMaxResults(out int maximum);
    }
}
