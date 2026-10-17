namespace ReStart.Core;

/// <summary>Describes an app leaf or a Start-menu folder without changing the leaf's launch identity.</summary>
public sealed record AppListNode(string Name, string Key, AppEntry? App, IReadOnlyList<AppListNode> Children, string FolderPath = "")
{
    /// <summary>Gets whether this node is an expandable directory rather than an application.</summary>
    public bool IsFolder => App == null;
    /// <summary>Gets the root alphabet section, based on the directory or app's OS-provided name.</summary>
    public string Section => AppEntry.SectionFor(Name);
}

/// <summary>Describes a currently visible row and its folder indentation depth.</summary>
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
    public static IReadOnlyList<AppListNode> Build(IEnumerable<AppEntry> apps)
    {
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
        static AppListNode[] Materialize(Folder folder) => folder.Folders.Values.Select(f => new AppListNode(f.Name, "folder:" + f.Key, null, Materialize(f), f.Key))
            .Concat(folder.Apps.Select(a => new AppListNode(a.Name, "app:" + a.Id, a, [])))
            .OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(n => n.Key, StringComparer.Ordinal).ToArray();
        return Materialize(root);
    }

    /// <summary>Returns display rows for the current expanded folder keys; collapsed folders never remove apps from the catalog.</summary>
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
