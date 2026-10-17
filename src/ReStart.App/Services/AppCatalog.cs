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
    /// <summary>Gets frozen icon images safe to consume from the UI thread.</summary>
    public ConcurrentDictionary<string, ImageSource> Icons { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Gets the number of applications returned by the last completed enumeration.</summary>
    public int Count { get; private set; }

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

    /// <summary>Loads icons on a background STA thread. The optional batch callback runs there and must dispatch any UI work.</summary>
    public Task LoadIconsAsync(IEnumerable<AppEntry> apps, Action? batchReady = null)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entries = apps.ToArray();
        var thread = new Thread(() =>
        {
            try
            {
                var count = 0;
                foreach (var app in entries)
                {
                    if (!Icons.ContainsKey(app.Id))
                    {
                        var icon = GetIcon(app.IsShellApp ? "shell:AppsFolder\\" + app.LaunchTarget : app.LaunchTarget);
                        if (icon != null) Icons[app.Id] = icon;
                    }
                    if (++count % 20 == 0) batchReady?.Invoke();
                }
                completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
        })
        { IsBackground = true, Name = "ReStart Shell icons" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task;
    }

    private List<AppEntry> Enumerate()
    {
        var entries = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
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
                }
                catch (COMException) { }
                finally { Release(itemObject); }
            }
        }
        finally { Release(itemsObject); Release(folderObject); Release(shellObject); }

        // Also retain .lnk/.url shortcuts absent from AppsFolder (portable and older Win32 apps).
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) })
        {
            if (!Directory.Exists(root)) continue;
            IEnumerable<string> files;
            try { files = Directory.GetFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }); }
            catch (IOException) { continue; }
            foreach (var file in files.Where(f => Path.GetExtension(f).ToLowerInvariant() is ".lnk" or ".url"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (entries.Values.Any(a => a.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))) continue;
                entries[file] = new(file, name, file, false);
            }
        }
        Count = entries.Count;
        return entries.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }

    [StructLayout(LayoutKind.Sequential)] private struct Size { public int Width; public int Height; }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(Size size, uint flags, out nint bitmap);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, nint binding, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    internal static ImageSource? GetIcon(string path)
    {
        IShellItemImageFactory? factory = null;
        nint bitmap = 0;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, 0, ref iid, out factory) != 0) return null;
            if (factory.GetImage(new Size { Width = 64, Height = 64 }, 0x100, out bitmap) != 0) return null;
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException) { return null; }
        finally { if (bitmap != 0) Native.DeleteObject(bitmap); Release(factory); }
    }
}
