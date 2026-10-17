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
        var menu = new ContextMenu();
        var pin = new MenuItem();
        var extras = new MenuItem { Header = "ReStart" };
        pin.Click += (_, _) =>
        {
            if (AppPinning.IsPinned(Document, app))
            { AppPinning.Unpin(Document, app); CommitLayout(); SetStatus(L.F(K.StatusUnpinned, app.Name)); }
            else
            { AppPinning.Pin(Document, app); CommitLayout(); SetStatus(L.F(K.StatusPinned, app.Name)); }
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
                    AppPinning.Pin(Document, app, group); CommitLayout(); SetStatus(L.F(K.StatusPinned, app.Name));
                });
            groups.IsEnabled = groups.Items.Count > 0;
            extras.Items.Add(groups);
            Add(extras, L.T(K.PinNewGroup), () =>
            {
                if (AppPinning.IsPinned(Document, app)) return;
                var group = new TileGroup { Name = "" };
                Document.Groups.Add(group);
                AppPinning.Pin(Document, app, group);
                ShowPinned(this, new()); CommitLayout(); RenameGroup(group);
            });
        }
        Refresh();
        menu.Opened += (_, _) => Refresh();
        return menu;
    }

    private void AddAppShellItems(ContextMenu menu, AppEntry app, int? index = null)
    {
        var more = new MenuItem { Header = L.T(K.MenuMore), IsEnabled = false };
        more.Items.Add(new MenuItem { Header = L.T(K.MenuLoading), IsEnabled = false });
        var uninstall = new MenuItem { Header = L.T(K.UninstallApp), Visibility = Visibility.Collapsed, IsEnabled = !_preview };
        uninstall.Click += async (_, _) => await ExecuteAppCommandAsync(app, "uninstall");
        var position = index ?? menu.Items.Count;
        menu.Items.Insert(position, more); menu.Items.Insert(position + 1, uninstall);
        var generation = 0;
        menu.Closed += (_, _) => generation++;
        menu.Opened += async (_, _) =>
        {
            var current = ++generation;
            var verbs = await AppShellCommands.ReadAsync(app);
            if (current == generation && menu.IsOpen) PopulateAppShellItems(more, uninstall, app, verbs);
        };
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
        Hide();
        try { await AppShellCommands.InvokeAsync(app, verb, owner); }
        catch (Exception ex) when (ex is COMException or Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        { Present(); SetStatus(L.T(K.ErrorAppCommand) + ex.Message); }
    }
}
