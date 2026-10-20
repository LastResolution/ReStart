using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private ContextMenu CreateAppMenu(AppEntry app)
    {
        var menu = CreateContextMenu();
        var pin = new MenuItem();
        var extras = new MenuItem { Header = "ReStart" };
        pin.Click += (_, _) =>
        {
            if (AppPinning.IsPinned(Document, app))
            { AppPinning.Unpin(Document, app); CommitLayout(); SetStatus(L.F(K.StatusUnpinned, app.Name)); }
            else
            { PinMenuApplication(app); CommitLayout(); SetStatus(L.F(K.StatusPinned, app.Name)); }
        };
        menu.Items.Add(pin);
        AddAppShellItems(menu, app);
        menu.Items.Add(extras);
        void Refresh()
        {
            pin.Header = L.T(AppPinning.IsPinned(Document, app) ? K.UnpinTile : K.PinToStart);
            extras.Items.Clear();
            extras.Visibility = AppPinning.IsPinned(Document, app) ? Visibility.Collapsed : Visibility.Visible;
            var groups = new MenuItem { Header = L.T(K.PinToGroupMenu) };
            foreach (var group in Document.Groups.Where(g => !g.IsStandalone))
                Add(groups, group.Name.Length == 0 ? L.T(K.UnnamedGroup) : group.Name, () =>
                {
                    if (!Document.Groups.Contains(group)) return;
                    PinMenuApplication(app, group); CommitLayout(); SetStatus(L.F(K.StatusPinned, app.Name));
                });
            groups.IsEnabled = groups.Items.Count > 0;
            extras.Items.Add(groups);
            Add(extras, L.T(K.PinNewGroup), () =>
            {
                if (AppPinning.IsPinned(Document, app)) return;
                var group = new TileGroup { Name = "" };
                Document.Groups.Add(group);
                PinMenuApplication(app, group);
                ShowPinned(this, new()); CommitLayout(); RenameGroup(group);
            });
        }
        Refresh();
        menu.Opened += (_, _) => Refresh();
        return menu;
    }

    private Tile PinMenuApplication(AppEntry app, TileGroup? destination = null)
    {
        var tile = destination == null ? PinApplication(app) : AppPinning.Pin(Document, app, destination);
        if (!_catalog.Icons.ContainsKey(app.Id)) _ = RefreshPinnedIconAsync(app);
        return tile;
    }

    private async Task RefreshPinnedIconAsync(AppEntry app)
    {
        try
        {
            await _catalog.LoadIconsAsync([app]);
            if (!_exit) RenderGroups();
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { if (!_exit) SetStatus(L.T(K.ErrorCatalogLoad) + ex.Message); }
    }

    private void AddAppShellItems(ContextMenu menu, AppEntry app, int? index = null)
    {
        var more = new MenuItem { Header = L.T(K.MenuMore), IsEnabled = false };
        more.Items.Add(new MenuItem { Header = L.T(K.MenuLoading), IsEnabled = false });
        var uninstall = new MenuItem { Header = L.T(K.UninstallApp), Visibility = Visibility.Collapsed, IsEnabled = !_preview };
        uninstall.Click += async (_, _) => await ExecuteAppCommandAsync(app, "uninstall");
        var position = index ?? menu.Items.Count;
        menu.Items.Insert(position, more); menu.Items.Insert(position + 1, uninstall);
        var recent = new MenuItem { Header = L.T(K.JumpRecent), Visibility = Visibility.Collapsed };
        var frequent = new MenuItem { Header = L.T(K.JumpFrequent), Visibility = Visibility.Collapsed };
        menu.Items.Insert(position + 2, recent); menu.Items.Insert(position + 3, frequent);
        var generation = 0;
        menu.Closed += (_, _) => generation++;
        menu.Opened += async (_, _) =>
        {
            var current = ++generation;
            var verbsTask = AppShellCommands.ReadAsync(app);
            var jumpTask = Document.Preferences.ShowJumpLists ? AppJumpLists.ReadAsync(app) : Task.FromResult<IReadOnlyList<JumpDestination>>([]);
            await Task.WhenAll(verbsTask, jumpTask);
            if (current != generation || !menu.IsOpen) return;
            PopulateAppShellItems(more, uninstall, app, await verbsTask);
            PopulateJumpItems(recent, frequent, app, await jumpTask);
        };
    }

    private void PopulateJumpItems(MenuItem recent, MenuItem frequent, AppEntry app, IReadOnlyList<JumpDestination> destinations)
    {
        foreach (var menu in new[] { recent, frequent })
        {
            menu.Items.Clear();
            foreach (var destination in destinations.Where(d => d.Frequent == (menu == frequent)))
            {
                var item = new MenuItem { Header = new TextBlock { Text = destination.Name, MaxWidth = 340, TextTrimming = TextTrimming.CharacterEllipsis }, ToolTip = destination.Target, IsEnabled = !_preview || _activationFixture != null };
                item.Click += async (_, _) =>
                {
                    if (_preview) { _activationFixture?.Invoke(new(app.Id, destination.Name, destination.Target, false)); return; }
                    var owner = new WindowInteropHelper(this).Handle;
                    Dismiss();
                    try { await AppShellCommands.OpenDocumentAsync(app, destination.Target, owner); }
                    catch (Exception ex) when (ex is COMException or Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
                    { Present(); SetStatus(L.T(K.ErrorAppCommand) + ex.Message); }
                };
                menu.Items.Add(item);
            }
            menu.Visibility = menu.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void PopulateAppShellItems(MenuItem more, MenuItem uninstall, AppEntry app, IReadOnlySet<string> verbs)
    {
        more.Items.Clear();
        foreach (var (verb, label) in new[] { ("runas", K.RunAsAdministrator), ("opencontaining", K.OpenFileLocation), ("properties", K.AppProperties) })
        {
            if (!verbs.Contains(verb)) continue;
            var item = new MenuItem { Header = L.T(label), IsEnabled = !_preview };
            item.Click += async (_, _) => await ExecuteAppCommandAsync(app, verb);
            more.Items.Add(item);
        }
        more.Visibility = more.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        more.IsEnabled = more.Items.Count > 0;
        uninstall.Visibility = verbs.Contains("uninstall") ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task ExecuteAppCommandAsync(AppEntry app, string verb)
    {
        if (_preview) return;
        var owner = new WindowInteropHelper(this).Handle;
        Dismiss();
        try { await AppShellCommands.InvokeAsync(app, verb, owner); }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x800704c7)) { }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (Exception ex) when (ex is COMException or Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        { Present(); SetStatus(L.T(K.ErrorAppCommand) + ex.Message); }
    }
}
