using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.Core;
using System.Collections.Concurrent;

namespace ReStart.App.Services;

/// <summary>Enumerates AppsFolder and local shortcuts on dedicated STA threads without altering the shell.</summary>
public sealed class AppCatalog
{
    private readonly IconCache? _cache;
    /// <summary>Creates a catalog with an optional disposable pixel cache outside the portable application directory.</summary>
    public AppCatalog(string? cacheDirectory = null) { if (cacheDirectory != null) _cache = new(cacheDirectory); }
    internal int IconCacheHits => _cache?.Hits ?? 0;
    internal int IconCacheMisses => _cache?.Misses ?? 0;
    /// <summary>Gets frozen icon images safe to consume from the UI thread.</summary>
    public ConcurrentDictionary<string, ImageSource> Icons { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Gets frozen icons for logical Start-menu folder paths.</summary>
    public ConcurrentDictionary<string, ImageSource> FolderIcons { get; } = new(StringComparer.OrdinalIgnoreCase);
    private volatile ConcurrentDictionary<string, string> _folderIconPaths = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Gets the number of Start-menu folder paths found during the last enumeration.</summary>
    public int FolderCount => _folderIconPaths.Count;
    /// <summary>Gets the number of applications returned by the last completed enumeration.</summary>
    public int Count { get; private set; }

    /// <summary>Gets the actual directory behind a displayed Start-menu folder without treating its children as the pin target.</summary>
    internal AppEntry? FolderEntry(string displayPath)
    {
        if (!_folderIconPaths.TryGetValue(displayPath, out var path) || !Directory.Exists(path)) return null;
        var entry = FilePinning.FromPath(path) with { Name = displayPath.Split('\\').Last() };
        if (FolderIcons.TryGetValue(displayPath, out var icon)) Icons[entry.Id] = icon;
        return entry;
    }

    /// <summary>Enumerates real app names and launch identities on a background STA thread.</summary>
    public Task<List<AppEntry>> LoadAsync()
    {
        var completion = new TaskCompletionSource<List<AppEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Enumerate()); }
            catch (Exception ex) { completion.SetException(ex); }
        })
        { IsBackground = true, Name = "ReStart Shell catalogue" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    /// <summary>Loads frozen icons with up to four independent STA workers. Progress callbacks must dispatch UI work.</summary>
    public async Task LoadIconsAsync(IEnumerable<AppEntry> apps, Action? batchReady = null)
    {
        var pending = new ConcurrentQueue<(string Key, string Target, bool Folder)>(apps.Where(a => !Icons.ContainsKey(a.Id))
            .Select(a => (a.Id, a.IsShellApp ? "shell:AppsFolder\\" + a.LaunchTarget : a.LaunchTarget, false))
            .Concat(_folderIconPaths.Where(f => !FolderIcons.ContainsKey(f.Key)).Select(f => (f.Key, f.Value, true))));
        var count = 0;
        var workers = Enumerable.Range(0, Math.Min(4, pending.Count)).Select(_ =>
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                var apartment = CoInitializeEx(0, 2);
                try
                {
                    while (pending.TryDequeue(out var item))
                    {
                        var icon = _cache == null ? GetIcon(item.Target) : _cache.Load(item.Target, GetIcon);
                        if (icon != null) (item.Folder ? FolderIcons : Icons)[item.Key] = icon;
                        if (Interlocked.Increment(ref count) % 20 == 0) batchReady?.Invoke();
                    }
                    completion.SetResult();
                }
                catch (Exception ex) { completion.SetException(ex); }
                finally { if (apartment >= 0) CoUninitialize(); }
            })
            { IsBackground = true, Name = "ReStart Shell icons" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            return completion.Task;
        }).ToArray();
        await Task.WhenAll(workers);
        _cache?.Trim();
    }

    private List<AppEntry> Enumerate()
    {
        var folderIconPaths = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
        var shellLinks = new List<ShellAppLink>();
        object? shellObject = null, folderObject = null, itemsObject = null;
        try
        {
            shellObject = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            dynamic shell = shellObject!;
            folderObject = shell.NameSpace("shell:AppsFolder");
            dynamic folder = folderObject!;
            itemsObject = folder.Items();
            dynamic items = itemsObject;
            int count = items.Count;
            for (var i = 0; i < count; i++)
            {
                object? itemObject = null;
                try
                {
                    itemObject = items.Item(i);
                    dynamic item = itemObject;
                    string name = item.Name;
                    string path = item.Path;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path)) continue;
                    var app = new AppEntry(path, name, path);
                    entries.TryAdd(path, app);
                    shellLinks.Add(new(path, ShellProperty(itemObject, "System.Link.TargetParsingPath"), ShellProperty(itemObject, "System.Link.Arguments")));
                }
                catch (COMException) { }
                finally { Release(itemObject); }
            }
        }
        finally { Release(itemsObject); Release(folderObject); Release(shellObject); }

        // Retain physical folder structure and user-over-common precedence, while
        // preserving matching AppsFolder IDs so existing pinned icons still resolve.
        var shortcuts = new List<StartMenuShortcut>();
        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var metadata = new ShortcutMetadata();
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) })
        {
            if (!Directory.Exists(root)) continue;
            IEnumerable<string> files;
            try { files = Directory.GetFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }); }
            catch (IOException) { continue; }
            foreach (var file in files.Where(f => Path.GetExtension(f).ToLowerInvariant() is ".lnk" or ".url").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(root, file);
                var physical = root;
                var folders = new List<string>();
                foreach (var segment in (Path.GetDirectoryName(relative) ?? "").Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    physical = Path.Combine(physical, segment);
                    if (!displayNames.TryGetValue(physical, out var display)) displayNames[physical] = display = ShellDisplayName(physical, segment);
                    folders.Add(display);
                    folderIconPaths.TryAdd(string.Join('\\', folders), physical);
                }
                var name = ShellDisplayName(file, Path.GetFileNameWithoutExtension(file));
                if (name == Path.GetFileName(file)) name = Path.GetFileNameWithoutExtension(name);
                var identity = metadata.Read(file);
                shortcuts.Add(new(relative, new(file, name, file, false, string.Join('\\', folders)), identity.AppId, identity.TargetPath, identity.Arguments));
            }
        }
        var merged = AppCatalogLayout.Merge(entries.Values, shortcuts, shellLinks).ToList();
        Count = merged.Count;
        _folderIconPaths = folderIconPaths;
        return merged;
    }

    private static string ShellProperty(object item, string property)
    {
        try { return ((dynamic)item).ExtendedProperty(property) as string ?? ""; }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { return ""; }
    }

    // Optional identity reads never resolve, execute or modify the shortcut.
    // Cache parent COM folders only for this STA enumeration and release them all.
    private sealed class ShortcutMetadata : IDisposable
    {
        private readonly object? _shell;
        private readonly Dictionary<string, object?> _folders = new(StringComparer.OrdinalIgnoreCase);
        internal ShortcutMetadata()
        {
            try { _shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!); }
            catch (COMException) { }
        }
        internal ShellAppLink Read(string path)
        {
            object? item = null;
            try
            {
                if (_shell == null) return new("", "");
                var parent = Path.GetDirectoryName(path)!;
                if (!_folders.TryGetValue(parent, out var folder)) _folders[parent] = folder = ((dynamic)_shell).NameSpace(parent);
                if (folder == null) return new("", "");
                item = ((dynamic)folder).ParseName(Path.GetFileName(path));
                return item == null ? new("", "") : new(ShellProperty(item, "System.AppUserModel.ID"),
                    ShellProperty(item, "System.Link.TargetParsingPath"), ShellProperty(item, "System.Link.Arguments"));
            }
            catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { return new("", ""); }
            finally { Release(item); }
        }
        public void Dispose()
        {
            foreach (var folder in _folders.Values) Release(folder);
            _folders.Clear();
            Release(_shell);
        }
    }

    private static string ShellDisplayName(string path, string fallback)
    {
        // DISPLAYNAME requests no HICON. Avoid the API's MAX_PATH limit and retain
        // the actual name when a legacy shortcut cannot be resolved by the shell.
        if (path.Length >= 260) return fallback;
        return SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x200) != 0 && !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : fallback;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(string path, uint attributes, out ShellFileInfo info, uint size, uint flags);

    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }

    [StructLayout(LayoutKind.Sequential)] private struct Size { public int Width; public int Height; }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(Size size, uint flags, out nint bitmap);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, nint binding, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);

    internal static ImageSource? GetIcon(string path)
        => GetFactoryIcon(path) ?? GetFileIcon(path);

    /// <summary>Uses the legacy full-icon path when an image factory cannot supply a filesystem icon.</summary>
    internal static ImageSource? GetFileIcon(string path)
    {
        if (path.Length >= 260 || !File.Exists(path) && !Directory.Exists(path)) return null;
        SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100);
        if (info.Icon == 0) return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze(); return image;
        }
        finally { DestroyIcon(info.Icon); }
    }

    private static ImageSource? GetFactoryIcon(string path)
    {
        IShellItemImageFactory? factory = null;
        nint bitmap = 0;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, 0, ref iid, out factory) != 0) return null;
            // ICONONLY avoids folder/document thumbnail canvases whose actual
            // icon can occupy only a few pixels. SCALEUP keeps legacy icons usable.
            const uint iconOnly = 0x4, scaleUp = 0x100;
            if (factory.GetImage(new Size { Width = 64, Height = 64 }, iconOnly | scaleUp, out bitmap) != 0) return null;
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException) { return null; }
        finally { if (bitmap != 0) Native.DeleteObject(bitmap); Release(factory); }
    }
}
