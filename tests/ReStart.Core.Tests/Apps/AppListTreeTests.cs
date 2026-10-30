using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class AppListTreeTests
{
    [Fact]
    public void FolderPathsAreMergedWithoutChangingLeafIdentity()
    {
        var a = new AppEntry("a", "Alpha", "target-a", FolderPath: "Tools/Editors");
        var b = new AppEntry("b", "Beta", "target-b", FolderPath: "tools\\editors");
        var tree = AppListTree.Build([a, b]);
        var tools = Assert.Single(tree);
        var editors = Assert.Single(tools.Children);
        Assert.True(editors.IsFolder);
        Assert.Equal(new[] { a, b }, editors.Children.Select(n => n.App));
        Assert.Equal("target-a", editors.Children[0].App!.LaunchTarget);
    }
    [Fact]
    public void CollapsedFoldersHideRowsWithoutRemovingSearchableApps()
    {
        var app = new AppEntry("edge", "Microsoft Edge", "edge", FolderPath: "Browsers");
        var tree = AppListTree.Build([app]);
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Assert.Single(AppListTree.VisibleRows(tree, expanded));
        Assert.Same(app, Assert.Single(AppSearch.Find([app], "E")));
        expanded.Add(tree[0].Key);
        var rows = AppListTree.VisibleRows(tree, expanded).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal(1, rows[1].Depth);
        Assert.Same(app, rows[1].Node.App);
    }
    [Fact]
    public void RootAppsAndFoldersSortTogether()
    {
        var tree = AppListTree.Build([new("z", "Zulu", "z"), new("a", "Alpha", "a"), new("inside", "Leaf", "leaf", FolderPath: "Middle")]);
        Assert.Equal(new[] { "Alpha", "Middle", "Zulu" }, tree.Select(n => n.Name));
    }

    [Fact]
    public void ExplicitCollationAppliesToAppsAndFoldersAtEveryDepth()
    {
        var reverse = Comparer<string>.Create((a, b) => StringComparer.Ordinal.Compare(b, a));
        var tree = AppListTree.Build([new("a", "Alpha", "a"), new("z", "Zulu", "z"),
            new("x", "Alpha child", "x", FolderPath: "Middle"), new("y", "Zulu child", "y", FolderPath: "Middle"),
            new("nested", "Leaf", "n", FolderPath: "Middle/Nested")], reverse);
        Assert.Equal(new[] { "Zulu", "Middle", "Alpha" }, tree.Select(n => n.Name));
        Assert.Equal(new[] { "Zulu child", "Nested", "Alpha child" }, tree[1].Children.Select(n => n.Name));
    }

    [Fact]
    public void EqualCollationWeightsHaveStableIdentityOrderWithoutChangingNames()
    {
        var entries = new[] { new AppEntry("z", "Same", "z"), new AppEntry("a", "same", "a") };
        var tree = AppListTree.Build(entries, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(new[] { "a", "z" }, tree.Select(n => n.App!.Id));
        Assert.Same(entries[1], tree[0].App);
        Assert.Equal("same", tree[0].Name);
    }
    [Fact]
    public void NestedExpansionRequiresEachAncestorToBeOpen()
    {
        var tree = AppListTree.Build([new("a", "App", "a", FolderPath: "A/B")]);
        var root = tree[0]; var child = root.Children[0];
        var expanded = new HashSet<string> { child.Key };
        Assert.Single(AppListTree.VisibleRows(tree, expanded));
        expanded.Add(root.Key);
        Assert.Equal(new[] { 0, 1, 2 }, AppListTree.VisibleRows(tree, expanded).Select(r => r.Depth));
    }
    [Fact]
    public void AnAppAndDirectoryWithTheSameNameRemainDistinct()
    {
        var tree = AppListTree.Build([new("a", "Tools", "a"), new("b", "Inside", "b", FolderPath: "Tools")]);
        Assert.Equal(2, tree.Count);
        Assert.Single(tree, n => n.IsFolder);
        Assert.Single(tree, n => !n.IsFolder);
    }
    [Fact]
    public void ShortcutFolderMappingKeepsShellLaunchAndIconIdentities()
    {
        var shell = new AppEntry("stable-shell-id", "App", "stable-launch-target");
        var shortcut = new StartMenuShortcut("Tools/App.lnk", new("file-id", "App", "file-path", false, "Tools"));
        var result = Assert.Single(AppCatalogLayout.Merge([shell], [shortcut]));
        Assert.Equal("stable-shell-id", result.Id);
        Assert.Equal("stable-launch-target", result.LaunchTarget);
        Assert.True(result.IsShellApp);
        Assert.Equal("Tools", result.FolderPath);
    }
    [Fact]
    public void UserRelativeShortcutWinsOverCommonShortcut()
    {
        var user = new StartMenuShortcut("Tools/App.lnk", new("user", "App", "user-path", false, "Tools"));
        var common = new StartMenuShortcut("tools/app.LNK", new("common", "App", "common-path", false, "Tools"));
        Assert.Equal("user", Assert.Single(AppCatalogLayout.Merge([], [user, common])).Id);
    }
    [Fact]
    public void SameNamedShortcutsInDifferentFoldersAreNotDiscarded()
    {
        var a = new StartMenuShortcut("A/App.lnk", new("a", "App", "a", false, "A"));
        var b = new StartMenuShortcut("B/App.lnk", new("b", "App", "b", false, "B"));
        var result = AppCatalogLayout.Merge([new("shell", "App", "shell")], [a, b]);
        Assert.Equal(3, result.Count);
        Assert.Equal("", result.Single(s => s.Id == "shell").FolderPath);
    }
    [Fact]
    public void PackagedAppsWithoutAShortcutRemainAtTheRoot()
    {
        var app = new AppEntry("packaged", "Store app", "aumid");
        Assert.Same(app, Assert.Single(AppCatalogLayout.Merge([app], [])));
    }

    [Fact]
    public void ExplicitAppIdsDisambiguateSameNamedVersions()
    {
        var a = new StartMenuShortcut("Old/App.lnk", new("file-a", "App", "old-file", false, "Old"), AppId: "shell-a");
        var b = new StartMenuShortcut("New/App.lnk", new("file-b", "App", "new-file", false, "New"), AppId: "shell-b");
        var result = AppCatalogLayout.Merge([new("shell-b", "App", "new-launch"), new("shell-a", "App", "old-launch")], [a, b]);
        Assert.Equal(2, result.Count);
        Assert.Equal("Old", result.Single(s => s.Id == "shell-a").FolderPath);
        Assert.Equal("new-launch", result.Single(s => s.Id == "shell-b").LaunchTarget);
        Assert.Equal("New", result.Single(s => s.Id == "shell-b").FolderPath);
    }

    [Fact]
    public void LinkTargetAndArgumentsDisambiguateIdenticalAppNames()
    {
        var a = new StartMenuShortcut("A/App.lnk", new("file-a", "App", "a", false, "A"), TargetPath: @"c:\tools\app.exe", Arguments: "--profile A");
        var b = new StartMenuShortcut("B/App.lnk", new("file-b", "App", "b", false, "B"), TargetPath: @"C:\TOOLS\APP.EXE", Arguments: "--profile B");
        var result = AppCatalogLayout.Merge([new("shell-a", "App", "launch-a"), new("shell-b", "App", "launch-b")], [b, a],
            [new("shell-a", @"C:\Tools\App.exe", "--profile A"), new("shell-b", @"C:\Tools\App.exe", "--profile B")]);
        Assert.Equal(2, result.Count);
        Assert.Equal("A", result.Single(s => s.Id == "shell-a").FolderPath);
        Assert.Equal("B", result.Single(s => s.Id == "shell-b").FolderPath);
    }

    [Fact]
    public void ConflictingKnownTargetsDoNotFallBackToTheSameDisplayName()
    {
        var shortcut = new StartMenuShortcut("Other/App.lnk", new("file", "App", "file", false, "Other"), TargetPath: @"C:\Other\App.exe");
        var result = AppCatalogLayout.Merge([new("shell", "App", "shell")], [shortcut], [new("shell", @"C:\Original\App.exe")]);
        Assert.Equal(2, result.Count);
        Assert.Equal("", result.Single(s => s.Id == "shell").FolderPath);
    }

    [Fact]
    public void AppIdMatchHasPriorityOverEarlierNameFallback()
    {
        var alias = new StartMenuShortcut("Alias.lnk", new("alias", "App", "alias", false));
        var identified = new StartMenuShortcut("Tools/Actual.lnk", new("file", "Localized app", "file", false, "Tools"), AppId: "shell");
        var result = AppCatalogLayout.Merge([new("shell", "App", "launch")], [alias, identified]);
        Assert.Equal(2, result.Count);
        Assert.Equal("Tools", result.Single(s => s.Id == "shell").FolderPath);
        Assert.Contains(result, s => s.Id == "alias");
    }

    [Fact]
    public void DuplicateShellTargetsRemainAmbiguousAndArgumentsStayCaseSensitive()
    {
        var shortcut = new StartMenuShortcut("Tools/Alias.lnk", new("file", "Alias", "file", false, "Tools"), TargetPath: @"C:\App.exe", Arguments: "--key a");
        var shells = new AppEntry[] { new("one", "First", "one"), new("two", "Second", "two") };
        Assert.Equal(3, AppCatalogLayout.Merge(shells, [shortcut], [new("one", @"C:\App.exe", "--key a"), new("two", @"C:\App.exe", "--key a")]).Count);
        Assert.Equal(3, AppCatalogLayout.Merge(shells, [shortcut], [new("one", @"C:\App.exe", "--key A")]).Count);
    }
}
