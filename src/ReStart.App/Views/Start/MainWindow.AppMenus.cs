using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private Func<AppEntry, bool, Task<IReadOnlySet<string>>>? _shellCommandsFixture;
    private Func<AppEntry, Task<IReadOnlyList<JumpDestination>>>? _jumpDestinationsFixture;
    private Func<AppEntry, string, Task>? _appCommandFixture;
    private Func<AppEntry, string, Task>? _removeJumpDestinationFixture;
    private Action<K>? _menuSettingsFixture;

    private ContextMenu CreateAppMenu(AppEntry app, bool search = false, K? history = null)
    {
        var menu = CreateContextMenu();
        var open = Add(menu, L.T(K.MenuOpen), () => Launch(app));
        open.Tag = "open";
        open.IsEnabled = !_layoutEditing;
        var pin = new MenuItem { Tag = "pin" };
        var extras = new MenuItem { Header = "ReStart" };
        pin.Click += (_, _) =>
        {
            if (!CanEditLayout || Document.Groups.Where(g => !CanEditGroup(g)).Any(g => g.Tiles.Any(t => t.AppId == app.Id || t.Children.Any(c => c.AppId == app.Id)))) return;
            if (AppPinning.IsPinned(Document, app))
            { AppPinning.Unpin(Document, app); CommitLayout(); SetStatus(L.F(K.StatusUnpinned, app.Name)); }
            else
            { PinMenuApplication(app); CommitLayout(); SetStatus(L.F(K.StatusPinned, app.Name)); }
        };
        menu.Items.Add(pin);
        AddAppShellItems(menu, app, search: search, history: history);
        menu.Items.Add(extras);
        void Refresh()
        {
            open.IsEnabled = !_layoutEditing;
            pin.Header = L.T(AppPinning.IsPinned(Document, app) ? K.UnpinTile : K.PinToStart);
            pin.IsEnabled = CanEditLayout && !Document.Groups.Where(g => !CanEditGroup(g)).Any(g => g.Tiles.Any(t => t.AppId == app.Id || t.Children.Any(c => c.AppId == app.Id)));
            extras.IsEnabled = CanEditLayout;
            extras.Items.Clear();
            extras.Visibility = AppPinning.IsPinned(Document, app) ? Visibility.Collapsed : Visibility.Visible;
            var groups = new MenuItem { Header = L.T(K.PinToGroupMenu) };
            foreach (var group in Document.Groups.Where(g => !g.IsStandalone && CanEditGroup(g)))
                Add(groups, group.Name.Length == 0 ? L.T(K.UnnamedGroup) : group.Name, () =>
                {
                    if (!Document.Groups.Contains(group) || !CanEditGroup(group)) return;
                    PinMenuApplication(app, group); CommitLayout(); SetStatus(L.F(K.StatusPinned, app.Name));
                });
            groups.IsEnabled = groups.Items.Count > 0;
            extras.Items.Add(groups);
            Add(extras, L.T(K.PinNewGroup), () =>
            {
                if (!CanEditLayout || AppPinning.IsPinned(Document, app)) return;
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
        if (!CanEditLayout || destination != null && !CanEditGroup(destination)) throw new InvalidOperationException(L.T(K.LayoutLockedStatus));
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

    private void AddAppShellItems(ContextMenu menu, AppEntry app, int? index = null, bool search = false, K? history = null)
    {
        var more = new MenuItem { Header = L.T(search ? K.MenuLoading : K.MenuMore), Tag = "shell-more", IsEnabled = !search };
        more.Items.Add(new MenuItem { Header = L.T(K.MenuLoading), IsEnabled = false });
        var uninstall = new MenuItem { Header = L.T(K.UninstallApp), Tag = "uninstall", Visibility = Visibility.Collapsed, IsEnabled = !_preview || _appCommandFixture != null };
        uninstall.Click += async (_, _) => await ExecuteAppCommandAsync(app, "uninstall");
        var position = index ?? menu.Items.Count;
        menu.Items.Insert(position, more); menu.Items.Insert(position + 1, uninstall);
        var recent = new MenuItem { Header = L.T(K.JumpRecent), Visibility = Visibility.Collapsed };
        var frequent = new MenuItem { Header = L.T(K.JumpFrequent), Visibility = Visibility.Collapsed };
        menu.Items.Insert(position + 2, recent); menu.Items.Insert(position + 3, frequent);
        var generation = 0;
        var direct = new List<MenuItem>();
        // A popup's close animation can finish after this menu was reopened.
        // That late notification must not invalidate the newer open request.
        menu.Closed += (_, _) => { if (!menu.IsOpen) generation++; };
        menu.Opened += (_, _) =>
        {
            var current = ++generation;
            foreach (var item in direct) menu.Items.Remove(item);
            direct.Clear();
            more.Items.Clear(); more.Items.Add(new MenuItem { Header = L.T(K.MenuLoading), IsEnabled = false });
            more.Visibility = Visibility.Visible; more.IsEnabled = !search;
            uninstall.Visibility = recent.Visibility = frequent.Visibility = Visibility.Collapsed;
            var extended = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            _ = LoadShellAsync(current, extended);
            _ = LoadJumpsAsync(current);
        };

        async Task LoadShellAsync(int current, bool extended)
        {
            IReadOnlySet<string> verbs;
            try { verbs = await (_shellCommandsFixture?.Invoke(app, extended) ?? AppShellCommands.ReadAsync(app, extended)).WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception ex) when (ex is TimeoutException or COMException or IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
            { verbs = new HashSet<string>(); }
            if (current != generation || !menu.IsOpen) return;
            PopulateAppShellItems(more, uninstall, app, verbs, search, history);
            if (!search) return;
            more.Visibility = Visibility.Collapsed;
            foreach (var item in more.Items.OfType<MenuItem>().ToArray())
            {
                more.Items.Remove(item);
                menu.Items.Insert(menu.Items.IndexOf(more), item); direct.Add(item);
            }
        }

        async Task LoadJumpsAsync(int current)
        {
            IReadOnlyList<JumpDestination> destinations = [];
            try
            {
                if (Document.Preferences.ShowJumpLists)
                    destinations = await (_jumpDestinationsFixture?.Invoke(app) ?? AppJumpLists.ReadAsync(app)).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) when (ex is TimeoutException or COMException or IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException) { }
            if (current == generation && menu.IsOpen) PopulateJumpItems(recent, frequent, app, destinations);
        }
    }

    private void PopulateJumpItems(MenuItem recent, MenuItem frequent, AppEntry app, IReadOnlyList<JumpDestination> destinations)
    {
        foreach (var menu in new[] { recent, frequent })
        {
            menu.Items.Clear();
            foreach (var destination in destinations.Where(d => d.Frequent == (menu == frequent)))
            {
                var item = new MenuItem { Tag = destination.Target, Header = new TextBlock { Text = destination.Name, MaxWidth = 340, TextTrimming = TextTrimming.CharacterEllipsis }, ToolTip = destination.Target, IsEnabled = !_preview || _activationFixture != null };
                async Task OpenAsync()
                {
                    if (_preview) { _activationFixture?.Invoke(new(app.Id, destination.Name, destination.Target, false)); return; }
                    var owner = new WindowInteropHelper(this).Handle;
                    Dismiss();
                    try { await AppShellCommands.OpenDocumentAsync(app, destination.Target, owner); }
                    catch (Exception ex) when (ex is COMException or Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
                    { Present(); SetStatus(L.T(K.ErrorAppCommand) + ex.Message); }
                }
                item.Click += async (_, e) => { if (ReferenceEquals(e.Source, item)) await OpenAsync(); };
                var actions = CreateContextMenu();
                var open = new MenuItem { Header = L.T(K.MenuOpen), Tag = "jump-open", IsEnabled = item.IsEnabled };
                open.Click += async (_, _) => await OpenAsync(); actions.Items.Add(open);
                var local = new AppEntry(destination.Target, destination.Name, destination.Target, false);
                foreach (var (verb, label) in new[] { ("opencontaining", destination.IsDirectory ? K.OpenFolderLocation : K.OpenFileLocation), ("properties", K.AppProperties) })
                {
                    var command = new MenuItem { Header = L.T(label), Tag = verb, IsEnabled = !_preview || _appCommandFixture != null };
                    command.Click += async (_, _) => await ExecuteAppCommandAsync(local, verb); actions.Items.Add(command);
                }
                var remove = new MenuItem { Header = L.T(K.RemoveFromThisList), Tag = "jump-remove", IsEnabled = !_preview || _removeJumpDestinationFixture != null };
                remove.Click += async (_, _) =>
                {
                    if (_preview && _removeJumpDestinationFixture == null) return;
                    try
                    {
                        await (_removeJumpDestinationFixture?.Invoke(app, destination.Target) ?? AppJumpLists.RemoveAsync(app, destination.Target));
                        foreach (var category in new[] { recent, frequent })
                        {
                            foreach (var row in category.Items.OfType<MenuItem>().Where(i => Equals(i.Tag, destination.Target)).ToArray()) category.Items.Remove(row);
                            category.Visibility = category.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                        }
                    }
                    catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
                    { SetStatus(L.T(K.ErrorAppCommand) + ex.Message); }
                };
                actions.Items.Add(remove); item.ContextMenu = actions;
                menu.Items.Add(item);
            }
            menu.Visibility = menu.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void PopulateAppShellItems(MenuItem more, MenuItem uninstall, AppEntry app, IReadOnlySet<string> verbs, bool search = false, K? history = null)
    {
        more.Items.Clear();
        foreach (var (verb, label) in new[] { ("runas", K.RunAsAdministrator), ("runasuser", K.RunAsDifferentUser),
            ("taskbarpin", K.PinToTaskbar), ("taskbarunpin", K.UnpinFromTaskbar), ("appsettings", K.AppSettings),
            ("manageapps", K.ManageInstalledApps),
            ("opencontaining", verbs.Contains(AppShellCommands.FolderLocation) ? K.OpenFolderLocation : K.OpenFileLocation),
            ("copyaspath", K.CopyFullPath), ("share", K.ShareItem), ("review", K.RateAndReview), ("properties", K.AppProperties) })
        {
            if (!verbs.Contains(verb) || verb == "copyaspath" && !search) continue;
            var item = new MenuItem { Header = L.T(label), Tag = verb, IsEnabled = !_preview || _appCommandFixture != null };
            item.Click += async (_, _) => await ExecuteAppCommandAsync(app, verb);
            more.Items.Add(item);
        }
        if (history is K.RecentlyAdded or K.MostUsed)
        {
            Add(more, L.T(history == K.RecentlyAdded ? K.RemoveFromThisList : K.DontShowInThisList), () =>
            {
                if (history == K.RecentlyAdded) Activity.DismissRecent(app.Id);
                else Activity.HideFrequent(app.Id);
                SaveActivity(); RenderApps();
            }).Tag = "history-remove";
            if (history == K.RecentlyAdded)
                Add(more, L.T(K.ClearRecentList), () => { Activity.ClearRecent(); SaveActivity(); RenderApps(); }).Tag = "history-clear";
        }
        more.Visibility = more.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        more.IsEnabled = more.Items.Count > 0;
        uninstall.Visibility = verbs.Contains("uninstall") ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task ExecuteAppCommandAsync(AppEntry app, string verb)
    {
        if (_preview) { if (_appCommandFixture != null) await _appCommandFixture(app, verb); return; }
        var owner = new WindowInteropHelper(this).Handle;
        Dismiss();
        try { await AppShellCommands.InvokeAsync(app, verb, owner); }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x800704c7)) { }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (Exception ex) when (ex is COMException or Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        { Present(); SetStatus(L.T(K.ErrorAppCommand) + ex.Message); }
    }
}
