using System.Runtime.InteropServices;
using System.Text;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Reads canonical shell commands on an STA without evaluating localized menu labels or registry command strings.</summary>
internal static class AppShellCommands
{
    internal const string FolderLocation = "restart:folder-location";
    internal static Task<IReadOnlySet<string>> ReadAsync(AppEntry app, bool extended = false) => OnSta<IReadOnlySet<string>>(() =>
    {
        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var context = new ShellContext(app, extended);
            commands.UnionWith(context.Commands.Keys.Where(v => AppMenuCommands.Normalize(v) != null));
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or IOException or UnauthorizedAccessException) { }
        if (AppFileLocation.Find(app) is { } location)
        {
            commands.Add("opencontaining");
            commands.Add("copyaspath");
            if (location.IsDirectory) commands.Add(FolderLocation);
            try
            {
                using var local = new ShellContext(new(app.Id, app.Name, location.Path, false), extended);
                foreach (var verb in new[] { "properties", "runasuser" })
                    if (local.Commands.ContainsKey(verb)) commands.Add(verb);
            }
            catch (Exception ex) when (ex is COMException or ArgumentException or IOException or UnauthorizedAccessException) { }
        }
        if (AppPackageCommands.Read(app) is { } package)
        {
            if (!commands.Contains("appsettings")) commands.Add("manageapps");
            if (package.StoreSigned) commands.Add("review");
        }
        return commands;
    });

    internal static Task InvokeAsync(AppEntry app, string verb, nint owner)
    {
        if (verb == null || AppMenuCommands.Normalize(verb) != verb && verb is not ("review" or "manageapps")) throw new ArgumentException("Unsupported app command.", nameof(verb));
        return OnSta(() =>
        {
            if (verb == "opencontaining" && AppFileLocation.Find(app) is { } location)
            { ExplorerLocation.Open(location.Path); return true; }
            if (verb == "copyaspath" && AppFileLocation.Find(app) is { } copied)
            { System.Windows.Clipboard.SetText("\"" + copied.Path + "\""); return true; }
            if (verb is "review" or "manageapps")
            { AppPackageCommands.Invoke(app, verb); return true; }
            if (verb is "properties" or "runasuser" && AppFileLocation.Find(app) is { } source)
            {
                using var local = new ShellContext(new(app.Id, app.Name, source.Path, false), verb == "runasuser");
                if (!local.Commands.TryGetValue(verb, out var localCommand)) throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
                local.Invoke(localCommand, owner); return true;
            }
            // Requery at the moment of invocation: a stale command ID from a
            // previous menu must never invoke a different extension command.
            using var context = new ShellContext(app, verb == "runasuser");
            if (context.Commands.TryGetValue(verb, out var command)) context.Invoke(command, owner);
            else throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
            return true;
        });
    }

    internal static Task OpenDocumentAsync(AppEntry app, string document, nint owner) => OnSta(() =>
    {
        if (!File.Exists(document) && !Directory.Exists(document)) throw new FileNotFoundException("The destination is no longer available.", document);
        using var context = new ShellContext(app);
        if (!context.Commands.TryGetValue("open", out var command)) throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
        context.Invoke(command, owner, "\"" + document + "\"");
        return true;
    });

    /// <summary>Invokes this app's canonical open/runas verb with explicit Unicode arguments and working directory.</summary>
    /// <param name="app">The installed application or local shortcut whose Shell verb is invoked.</param>
    /// <param name="arguments">Windows argument string passed verbatim to the application, without a command interpreter.</param>
    /// <param name="workingDirectory">Optional existing directory; environment variables are expanded, and empty uses Shell defaults.</param>
    /// <param name="elevated">Whether to request runas. Windows owns consent; an unsupported verb fails without a fallback launch.</param>
    /// <param name="owner">Native owner HWND for Shell consent and error UI.</param>
    /// <returns>A task completing after the Shell accepts or rejects the launch; failures propagate to the UI.</returns>
    internal static Task LaunchConfiguredAsync(AppEntry app, string arguments, string workingDirectory, bool elevated, nint owner) => OnSta(() =>
    {
        var directory = Environment.ExpandEnvironmentVariables(workingDirectory.Trim());
        if (directory.Length > 0 && !Directory.Exists(directory)) throw new DirectoryNotFoundException(L.T(K.ErrorWorkingDirectory));
        if (arguments.Length > 32760 || directory.Length > 32760 || arguments.Contains('\0')) throw new ArgumentException("Invalid launch options.");
        using var context = new ShellContext(app);
        if (!context.Commands.TryGetValue(elevated ? "runas" : "open", out var command)) throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
        context.Invoke(command, owner, arguments, directory);
        return true;
    });

    internal static Task<T> OnSta<T>(Func<T> work)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { result.SetResult(work()); }
            catch (Exception ex) { result.SetException(ex); }
        })
        { IsBackground = true, Name = "ReStart app commands" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }

    private sealed class ShellContext : IDisposable
    {
        private IShellItem? _item;
        private IContextMenu? _context;
        private nint _menu;
        internal Dictionary<string, uint> Commands { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal ShellContext(AppEntry app, bool extended = false)
        {
            try
            {
                var path = app.IsShellApp ? "shell:AppsFolder\\" + app.LaunchTarget : app.LaunchTarget;
                var itemId = typeof(IShellItem).GUID;
                Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, 0, ref itemId, out _item));
                var handler = new Guid("3981e225-f559-11d3-8e3a-00c04f6837d5"); // BHID_SFUIObject
                var contextId = typeof(IContextMenu).GUID;
                Marshal.ThrowExceptionForHR(_item.BindToHandler(0, ref handler, ref contextId, out _context));
                _menu = CreatePopupMenu();
                if (_menu == 0) throw new COMException("Unable to allocate a shell command menu.");
                Marshal.ThrowExceptionForHR(_context.QueryContextMenu(_menu, 0, 1, 0x7fff, extended ? 0x100u : 0)); // CMF_EXTENDEDVERBS
                ReadMenu(_menu, 0);
            }
            catch { Dispose(); throw; }
        }

        private void ReadMenu(nint menu, int depth)
        {
            if (depth > 8) return;
            var count = Math.Min(GetMenuItemCount(menu), 512);
            for (var i = 0; i < count; i++)
            {
                var state = GetMenuState(menu, (uint)i, 0x400); // MF_BYPOSITION
                if (state == uint.MaxValue || (state & 3) != 0) continue;
                var submenu = GetSubMenu(menu, i);
                if (submenu != 0) { ReadMenu(submenu, depth + 1); continue; }
                var id = GetMenuItemID(menu, i);
                if (id is < 1 or > 0x7fff) continue;
                var text = new StringBuilder(256);
                if (_context!.GetCommandString(id - 1, 4, 0, text, (uint)text.Capacity) >= 0 && text.Length > 0)
                    Commands.TryAdd(AppMenuCommands.Normalize(text.ToString()) ?? text.ToString(), id - 1); // GCS_VERBW
            }
        }

        internal void Invoke(uint command, nint owner, string? arguments = null, string? directory = null)
        {
            // CMINVOKECOMMANDINFOEX keeps non-ASCII document paths intact and
            // passes arguments to this app's own open verb, not a default handler.
            // Keep the STA/context alive until the Shell accepts the operation.
            // Without NOASYNC a handler can schedule work after this short-lived STA exits.
            var info = new InvokeInfo { Size = (uint)Marshal.SizeOf<InvokeInfo>(), Mask = 0x4100, Owner = owner, Verb = (nint)command, VerbW = (nint)command, ParametersW = arguments, DirectoryW = directory, Show = 1 };
            Marshal.ThrowExceptionForHR(_context!.InvokeCommand(ref info));
        }
        public void Dispose()
        {
            if (_menu != 0) { DestroyMenu(_menu); _menu = 0; }
            if (_context != null) { Marshal.FinalReleaseComObject(_context); _context = null; }
            if (_item != null) { Marshal.FinalReleaseComObject(_item); _item = null; }
        }
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(nint binding, ref Guid handler, ref Guid iid, out IContextMenu context);
    }
    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint command, uint flags, nint reserved, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, uint capacity);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct InvokeInfo
    {
        public uint Size, Mask;
        public nint Owner, Verb, Parameters, Directory;
        public int Show;
        public uint HotKey;
        public nint Icon;
        public nint Title, VerbW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ParametersW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? DirectoryW;
        public nint TitleW;
        public int PointX, PointY;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, nint binding, ref Guid iid, out IShellItem item);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern int GetMenuItemCount(nint menu);
    [DllImport("user32.dll")] private static extern nint GetSubMenu(nint menu, int position);
    [DllImport("user32.dll")] private static extern uint GetMenuItemID(nint menu, int position);
    [DllImport("user32.dll")] private static extern uint GetMenuState(nint menu, uint item, uint flags);
}
