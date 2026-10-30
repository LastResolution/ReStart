namespace ReStart.Core;

/// <summary>Describes an app leaf or a Start-menu folder without changing the leaf's launch identity.</summary>
/// <param name="Name">OS-provided display name of the app or directory.</param>
/// <param name="Key">Stable app: or folder: key used for expansion and focus restoration.</param>
/// <param name="App">Launch entry for an app leaf; null identifies a directory.</param>
/// <param name="Children">Sorted child nodes; empty for an app leaf.</param>
/// <param name="FolderPath">Physical Start-menu relative directory path used to resolve folder pins.</param>
public sealed record AppListNode(string Name, string Key, AppEntry? App, IReadOnlyList<AppListNode> Children, string FolderPath = "")
{
    /// <summary>Gets whether this node is an expandable directory rather than an application.</summary>
    public bool IsFolder => App == null;
}

/// <summary>Describes a currently visible row and its folder indentation depth.</summary>
/// <param name="Node">App or folder displayed by this row.</param>
/// <param name="Depth">Zero-based directory nesting level used for indentation.</param>
public readonly record struct AppListRow(AppListNode Node, int Depth);

/// <summary>Builds and expands the all-apps folder tree while search continues to use the flat leaf catalog.</summary>
public static class AppListTree
{
    private sealed class Folder(string name, string key)
    {
        public string Name { get; } = name;
        public string Key { get; } = key;
        public Dictionary<string, Folder> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<AppEntry> Apps { get; } = [];
    }

    /// <summary>Merges case-insensitive folder paths and sorts sibling apps and folders together by display name.</summary>
    /// <param name="apps">Flat app catalog with relative Start-menu folder paths.</param>
    /// <param name="nameComparer">Optional display-name collation for every sibling level; null uses the current culture. The comparer must remain unchanged during this call.</param>
    /// <returns>Sorted root nodes whose app leaves preserve the original launch entries.</returns>
    public static IReadOnlyList<AppListNode> Build(IEnumerable<AppEntry> apps, IComparer<string>? nameComparer = null)
    {
        nameComparer ??= StringComparer.CurrentCultureIgnoreCase;
        var root = new Folder("", "");
        foreach (var app in apps)
        {
            var parent = root;
            foreach (var segment in app.FolderPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!parent.Folders.TryGetValue(segment, out var folder))
                    parent.Folders[segment] = folder = new(segment, parent.Key.Length == 0 ? segment : parent.Key + "\\" + segment);
                parent = folder;
            }
            parent.Apps.Add(app);
        }
        AppListNode[] Materialize(Folder folder) => folder.Folders.Values.Select(f => new AppListNode(f.Name, "folder:" + f.Key, null, Materialize(f), f.Key))
            .Concat(folder.Apps.Select(a => new AppListNode(a.Name, "app:" + a.Id, a, [])))
            .OrderBy(n => n.Name, nameComparer).ThenBy(n => n.Key, StringComparer.Ordinal).ToArray();
        return Materialize(root);
    }

    /// <summary>Returns display rows for the current expanded folder keys; collapsed folders never remove apps from the catalog.</summary>
    /// <param name="nodes">Sibling nodes to traverse in their existing display order.</param>
    /// <param name="expanded">Stable folder keys whose children are visible.</param>
    /// <param name="depth">Initial zero-based indentation level for the supplied nodes.</param>
    /// <returns>Depth-first visible rows; collapsed descendants are omitted without changing the catalog.</returns>
    public static IEnumerable<AppListRow> VisibleRows(IEnumerable<AppListNode> nodes, ISet<string> expanded, int depth = 0)
    {
        foreach (var node in nodes)
        {
            yield return new(node, depth);
            if (node.IsFolder && expanded.Contains(node.Key))
                foreach (var child in VisibleRows(node.Children, expanded, depth + 1)) yield return child;
        }
    }
}
