using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private Func<AppEntry, string, string, string, CancellationToken, Task<NativeJumpReply>>? _nativeJumpFixture;

    private async Task<NativeJumpReply> SendNativeJumpAsync(AppEntry app, string operation, string key = "", string target = "", CancellationToken cancellation = default)
    {
        if (_nativeJumpFixture != null) return await _nativeJumpFixture(app, operation, key, target, cancellation);
        if (_preview) return new(false, "unsupported", "", NativeJumpSnapshot.Empty);
        var owner = await AppJumpLists.ResolveAppIdAsync(app).WaitAsync(TimeSpan.FromSeconds(5), cancellation);
        if (string.IsNullOrWhiteSpace(owner)) return new(false, "unsupported", "", NativeJumpSnapshot.Empty);
        return await NativeJumpBroker.SendAsync(new(1, operation, owner, key, target), cancellation);
    }

    private static bool JumpFailure(Exception ex) => ex is COMException or Win32Exception or IOException or UnauthorizedAccessException
        or InvalidOperationException or ArgumentException or TimeoutException or OperationCanceledException or JsonException
        or System.Security.SecurityException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException;

    private void AttachNativeJumpLists(ContextMenu menu, AppEntry app, MenuItem recent, MenuItem frequent, int position)
    {
        var pinned = new MenuItem { Header = L.T(K.JumpPinned), Tag = "jump-pinned", Visibility = Visibility.Collapsed };
        var tasks = new MenuItem { Header = L.T(K.JumpTasks), Tag = "jump-tasks", Visibility = Visibility.Collapsed };
        menu.Items.Insert(position, pinned); menu.Items.Insert(position + 3, tasks);
        CancellationTokenSource? pending = null;
        var generation = 0;
        menu.Closed += (_, _) =>
        {
            if (menu.IsOpen) return;
            generation++; pending?.Cancel(); pending?.Dispose(); pending = null;
        };
        menu.Opened += (_, _) =>
        {
            pending?.Cancel(); pending?.Dispose(); pending = new();
            var token = pending.Token; var current = ++generation;
            IReadOnlyList<JumpDestination> destinations = [];
            var snapshot = NativeJumpSnapshot.Empty;
            pinned.Visibility = tasks.Visibility = recent.Visibility = frequent.Visibility = Visibility.Collapsed;
            void Refresh()
            {
                if (current != generation || !menu.IsOpen) return;
                PopulateJumpItems(recent, frequent, app, destinations, snapshot, Accept);
                PopulateNativeJumpItems(pinned, tasks, app, snapshot, Accept);
                PopulateCustomJumpItems(menu, tasks, app, snapshot, Accept);
            }
            void Accept(NativeJumpSnapshot update)
            {
                snapshot = update with
                {
                    Tasks = Document.Preferences.ShowAppTasks ? update.Tasks : [],
                    Categories = Document.Preferences.ShowCustomJumpLists ? update.Categories : [],
                    Pinned = Document.Preferences.ShowPinnedJumpLists ? update.Pinned : [],
                    PinsSupported = Document.Preferences.ShowPinnedJumpLists && update.PinsSupported
                };
                Refresh();
            }
            async Task HistoryAsync()
            {
                try
                {
                    if (Document.Preferences.ShowJumpLists)
                        destinations = await (_jumpDestinationsFixture?.Invoke(app) ?? (_preview ? Task.FromResult<IReadOnlyList<JumpDestination>>([]) : AppJumpLists.ReadAsync(app)))
                            .WaitAsync(TimeSpan.FromSeconds(5), token);
                }
                catch (Exception ex) when (JumpFailure(ex)) { }
                Refresh();
            }
            async Task NativeAsync()
            {
                try
                {
                    if (Document.Preferences.ShowAppTasks || Document.Preferences.ShowPinnedJumpLists || Document.Preferences.ShowCustomJumpLists)
                    {
                        var reply = await SendNativeJumpAsync(app, "read", cancellation: token);
                        if (reply.Success) snapshot = reply.Snapshot with
                        {
                            Tasks = Document.Preferences.ShowAppTasks ? reply.Snapshot.Tasks : [],
                            Categories = Document.Preferences.ShowCustomJumpLists ? reply.Snapshot.Categories : [],
                            Pinned = Document.Preferences.ShowPinnedJumpLists ? reply.Snapshot.Pinned : [],
                            PinsSupported = Document.Preferences.ShowPinnedJumpLists && reply.Snapshot.PinsSupported
                        };
                    }
                }
                catch (Exception ex) when (JumpFailure(ex)) { }
                Refresh();
            }
            _ = HistoryAsync(); _ = NativeAsync();
        };
    }

    private void PopulateCustomJumpItems(ContextMenu menu, MenuItem tasks, AppEntry app, NativeJumpSnapshot snapshot, Action<NativeJumpSnapshot> changed)
    {
        foreach (var existing in menu.Items.OfType<MenuItem>().Where(row => Equals(row.Tag, "jump-custom")).ToArray()) menu.Items.Remove(existing);
        foreach (var category in snapshot.Categories ?? [])
        {
            var section = new MenuItem { Header = category.Name, Tag = "jump-custom" };
            foreach (var entry in category.Items)
            {
                var row = new MenuItem
                {
                    Header = new TextBlock { Text = entry.Name, MaxWidth = 340, TextTrimming = TextTrimming.CharacterEllipsis },
                    Tag = entry.Key,
                    ToolTip = entry.Target.Length == 0 ? entry.Name : entry.Target,
                    IsEnabled = !_preview || _nativeJumpFixture != null
                };
                row.Click += async (_, e) => { if (ReferenceEquals(e.Source, row)) await ExecuteNativeJumpAsync(app, "invoke-custom", entry.Key); };
                row.ContextMenu = NativeDestinationActions(app, entry, "invoke-custom", snapshot.PinsSupported, changed);
                section.Items.Add(row);
            }
            if (section.Items.Count > 0) menu.Items.Insert(menu.Items.IndexOf(tasks), section);
        }
    }

    private void PopulateNativeJumpItems(MenuItem pinned, MenuItem tasks, AppEntry app, NativeJumpSnapshot snapshot, Action<NativeJumpSnapshot> changed)
    {
        foreach (var section in new[] { pinned, tasks })
        {
            section.Items.Clear();
            foreach (var entry in section == tasks ? snapshot.Tasks : snapshot.Pinned)
            {
                var operation = section == tasks ? "invoke-task" : "invoke-pinned";
                var row = new MenuItem
                {
                    Tag = entry.Key,
                    Header = new TextBlock { Text = entry.Name, MaxWidth = 340, TextTrimming = TextTrimming.CharacterEllipsis },
                    ToolTip = entry.Target.Length == 0 ? entry.Name : entry.Target,
                    IsEnabled = !_preview || _nativeJumpFixture != null
                };
                row.Click += async (_, e) => { if (ReferenceEquals(e.Source, row)) await ExecuteNativeJumpAsync(app, operation, entry.Key); };
                if (section == pinned) row.ContextMenu = NativeDestinationActions(app, entry, operation, snapshot.PinsSupported, changed);
                section.Items.Add(row);
            }
            section.Visibility = section.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private ContextMenu NativeDestinationActions(AppEntry app, NativeJumpItem entry, string operation, bool pinsSupported, Action<NativeJumpSnapshot> changed)
    {
        var actions = CreateContextMenu(); var enabled = !_preview || _nativeJumpFixture != null;
        var open = new MenuItem { Header = L.T(K.MenuOpen), Tag = "jump-open", IsEnabled = enabled };
        open.Click += async (_, _) => await ExecuteNativeJumpAsync(app, operation, entry.Key); actions.Items.Add(open);
        if (entry.Target.Length > 0)
        {
            var local = new AppEntry(entry.Target, entry.Name, entry.Target, false);
            if (AppFileLocation.Find(local) != null)
            {
                foreach (var (verb, label) in new[] { ("opencontaining", entry.IsDirectory ? K.OpenFolderLocation : K.OpenFileLocation), ("properties", K.AppProperties) })
                {
                    var command = new MenuItem { Header = L.T(label), Tag = verb, IsEnabled = !_preview || _appCommandFixture != null };
                    command.Click += async (_, _) => await ExecuteAppCommandAsync(local, verb); actions.Items.Add(command);
                }
            }
        }
        if (operation == "invoke-pinned")
        {
            var unpin = new MenuItem { Header = L.T(K.UnpinFromJumpList), Tag = "jump-unpin", IsEnabled = enabled };
            unpin.Click += async (_, _) => await ExecuteNativeJumpAsync(app, "unpin", entry.Key, changed: changed); actions.Items.Add(unpin);
        }
        else if (entry.Target.Length > 0 && pinsSupported && (File.Exists(entry.Target) || Directory.Exists(entry.Target)))
        {
            var pin = new MenuItem { Header = L.T(K.PinToJumpList), Tag = "jump-pin", IsEnabled = enabled };
            pin.Click += async (_, _) => await ExecuteNativeJumpAsync(app, "pin", target: entry.Target, changed: changed); actions.Items.Add(pin);
        }
        return actions;
    }

    private async Task ExecuteNativeJumpAsync(AppEntry app, string operation, string key = "", string target = "", Action<NativeJumpSnapshot>? changed = null)
    {
        var launching = operation.StartsWith("invoke-", StringComparison.Ordinal);
        if (!_preview && launching) Dismiss();
        try
        {
            if (!_preview && operation is "invoke-pinned" or "invoke-custom")
            {
                var current = await SendNativeJumpAsync(app, "read");
                if (await AppJumpLists.OpenPinnedFileAsync(app, current, key, new System.Windows.Interop.WindowInteropHelper(this).Handle, operation == "invoke-custom")) return;
            }
            var reply = await SendNativeJumpAsync(app, operation, key, target);
            if (!reply.Success) throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
            if (!launching) { changed?.Invoke(reply.Snapshot); SetStatus(L.T(K.JumpListUpdated)); }
        }
        catch (Exception ex) when (JumpFailure(ex))
        { if (!_preview && launching) Present(); SetStatus(L.T(K.ErrorAppCommandUnavailable)); }
    }
}
