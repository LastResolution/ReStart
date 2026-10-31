using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ReStart.App.Services;

/// <summary>Reads Windows task/pinned metadata without parsing private history databases or modifying Explorer.</summary>
internal static partial class NativeJumpWorker
{
    private const string CustomClass = "77f10cf0-3db5-4966-b520-b7c54fd35ed6";
    private const string AutomaticClass = "f0ae1542-f497-484b-a175-a20db09144ba";
    private static readonly Guid PropertyFormat = new("9f4c2855-9f79-4b39-a8d0-e1d42de1d5f3");

    internal static void PrepareProcess() => SetErrorMode(0x8003);

    internal static NativeJumpReply Run(NativeJumpRequest request)
    {
        if (!NativeJumpContract.ValidRequest(request)) return new(false, "invalid-request", request.AppId, NativeJumpSnapshot.Empty);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) || !Environment.Is64BitProcess)
            return new(false, "unsupported", request.AppId, NativeJumpSnapshot.Empty);
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("Native jump-list work requires an isolated STA.");
        var success = request.Operation switch
        {
            "read" => true,
            "pin" => PinPath(request.AppId, request.Target),
            "unpin" => Unpin(request.AppId, request.Key),
            "invoke-task" => Invoke(request.AppId, request.Key, "task"),
            "invoke-pinned" => Invoke(request.AppId, request.Key, "pinned"),
            "invoke-custom" => Invoke(request.AppId, request.Key, "custom"),
            _ => false
        };
        return new(success, success ? "" : "unavailable", request.AppId, Read(request.AppId));
    }

    private static NativeJumpSnapshot Read(string owner)
    {
        var tasks = new List<NativeJumpItem>(); var pinned = new List<NativeJumpItem>();
        var categories = new List<NativeJumpCategory>();
        var tasksSupported = false; var pinsSupported = false;
        object? root = null;
        try
        {
            root = Create(CustomClass);
            if (root is ICustomList custom && custom.SetApplicationID(owner) >= 0 && custom.GetCategoryCount(out var count) >= 0)
            {
                tasksSupported = true;
                for (uint index = 0; index < Math.Min(count, 32); index++)
                {
                    var category = new Category();
                    if (custom.GetCategory(index, 1, ref category) < 0) continue;
                    try
                    {
                        if (category.Type is not (0 or 2)) continue;
                        var name = category.Type == 0 ? Name(Marshal.PtrToStringUni(category.Data) ?? "") : "";
                        if (category.Type == 0 && (name.Length == 0 || categories.Count >= 8 || categories.Any(c => c.Name == name))) continue;
                        var iid = typeof(IItems).GUID; IItems? items = null;
                        try
                        {
                            var entries = new List<NativeJumpItem>();
                            if (custom.EnumerateCategoryDestinations(index, ref iid, out items) >= 0) Visit(items, item =>
                            {
                                if (Describe(owner, item, category.Type == 2) is not { } entry) return;
                                if (category.Type == 2) tasks.Add(entry);
                                else if (entries.Count < 8) entries.Add(entry with { Key = Key(entry.Key, name) });
                            });
                            if (category.Type == 0 && entries.Count > 0) categories.Add(new(name, entries.DistinctBy(item => item.Key).ToArray()));
                        }
                        finally { Release(items); }
                    }
                    finally { if (category.Type == 0 && category.Data != 0) Marshal.FreeCoTaskMem(category.Data); }
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException) { }
        finally { Release(root); }
        root = null;
        try
        {
            root = Create(AutomaticClass);
            if (root is IAutomaticList automatic && automatic.Initialize(owner, null, null) >= 0)
            {
                var iid = typeof(IItems).GUID; IItems? items = null;
                try
                {
                    if (automatic.GetList(0, 64, 1, ref iid, out items) >= 0)
                    { pinsSupported = true; Visit(items, item => { if (Describe(owner, item, false) is { } entry) pinned.Add(entry); }); }
                }
                finally { Release(items); }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException) { }
        finally { Release(root); }
        return new(tasksSupported, pinsSupported, tasks.DistinctBy(item => item.Key).Take(64).ToArray(), pinned.DistinctBy(item => item.Key).Take(64).ToArray(), categories.ToArray());
    }

    private static bool PinPath(string owner, string path)
    {
        var app = ReStart.Core.FilePinning.FromPath(path);
        object? root = null, item = null;
        try
        {
            root = Create(AutomaticClass);
            if (root is not IAutomaticList automatic || automatic.Initialize(owner, null, null) < 0) return false;
            var iid = typeof(IItem).GUID;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(app.LaunchTarget, 0, ref iid, out item));
            if (automatic.PinItem(item, -1) < 0) return false;
            return Read(owner).Pinned.Any(entry => entry.Target.Equals(app.LaunchTarget, StringComparison.OrdinalIgnoreCase));
        }
        finally { Release(item); Release(root); }
    }

    private static bool Unpin(string owner, string key)
    {
        object? root = null; IItems? items = null;
        try
        {
            root = Create(AutomaticClass);
            if (root is not IAutomaticList automatic || automatic.Initialize(owner, null, null) < 0) return false;
            var iid = typeof(IItems).GUID;
            if (automatic.GetList(0, 64, 1, ref iid, out items) < 0) return false;
            var changed = false;
            Visit(items, item =>
            {
                if (!changed && Describe(owner, item, false)?.Key == key) changed = automatic.PinItem(item, -2) >= 0;
            });
            return changed && Read(owner).Pinned.All(entry => entry.Key != key);
        }
        finally { Release(items); Release(root); }
    }

    private static bool Invoke(string owner, string key, string kind)
    {
        object? root = null; IItems? items = null;
        var invoked = false;
        var task = kind == "task"; var categoryName = "";
        void Select(object item)
        {
            var entry = Describe(owner, item, task);
            if (invoked || entry == null || (kind == "custom" ? Key(entry.Key, categoryName) : entry.Key) != key) return;
            if (!task && item is not ILink && item is IItem shell)
            {
                var target = Display(shell, 0x80028000);
                if (File.Exists(target) || Directory.Exists(target))
                    AppShellCommands.OpenDocumentAsync(new(owner, owner, owner), target, 0).GetAwaiter().GetResult();
                else
                {
                    var handler = new Guid("3981e225-f559-11d3-8e3a-00c04f6837d5"); // BHID_SFUIObject
                    var iid = typeof(IMenu).GUID;
                    Marshal.ThrowExceptionForHR(shell.BindToHandler(0, ref handler, ref iid, out var pointer));
                    object? context = null;
                    try { context = Marshal.GetObjectForIUnknown(pointer); InvokeLink(context); }
                    finally { Release(context); if (pointer != 0) Marshal.Release(pointer); }
                }
            }
            else InvokeLink(item);
            invoked = true;
        }
        try
        {
            if (kind == "pinned")
            {
                root = Create(AutomaticClass);
                if (root is not IAutomaticList automatic || automatic.Initialize(owner, null, null) < 0) return false;
                var iid = typeof(IItems).GUID;
                if (automatic.GetList(0, 64, 1, ref iid, out items) >= 0) Visit(items, Select);
                return invoked;
            }
            root = Create(CustomClass);
            if (root is not ICustomList custom || custom.SetApplicationID(owner) < 0 || custom.GetCategoryCount(out var count) < 0) return false;
            for (uint index = 0; index < Math.Min(count, 32) && !invoked; index++)
            {
                var category = new Category();
                if (custom.GetCategory(index, 1, ref category) < 0) continue;
                try
                {
                    if (category.Type != (task ? 2 : 0)) continue;
                    categoryName = task ? "" : Name(Marshal.PtrToStringUni(category.Data) ?? "");
                    if (!task && categoryName.Length == 0) continue;
                    var iid = typeof(IItems).GUID;
                    try { if (custom.EnumerateCategoryDestinations(index, ref iid, out items) >= 0) Visit(items, Select); }
                    finally { Release(items); items = null; }
                }
                finally { if (category.Type == 0 && category.Data != 0) Marshal.FreeCoTaskMem(category.Data); }
            }
            return invoked;
        }
        finally { Release(items); Release(root); }
    }

    private static void InvokeLink(object item)
    {
        var activation = Property(item, PropertyFormat, 5);
        if (activation.Contains('!'))
        {
            if (activation.Length > 256 || activation.Any(char.IsControl)) throw new InvalidOperationException("The task activation identity is unavailable.");
            object? manager = null;
            try
            {
                manager = Create("45ba127d-10a8-46ea-8ab7-56ea9078943c");
                Marshal.ThrowExceptionForHR(((IActivation)manager!).ActivateApplication(activation, Property(item, PropertyFormat, 20), 2, out _));
                return;
            }
            finally { Release(manager); }
        }
        // The link itself owns its installed application's arguments and working
        // directory. Never reconstruct a command line from menu text or IPC.
        if (item is not IMenu context) throw new InvalidOperationException("The task has no default Shell action.");
        var menu = CreatePopupMenu();
        if (menu == 0) throw new InvalidOperationException("The task menu could not be created.");
        try
        {
            Marshal.ThrowExceptionForHR(context.QueryContextMenu(menu, 0, 1, 0x7fff, 1));
            var command = GetMenuDefaultItem(menu, 0, 0);
            if (command is 0 or uint.MaxValue) throw new InvalidOperationException("The task's default action is unavailable.");
            var invoke = new InvokeInfo { Size = (uint)Marshal.SizeOf<InvokeInfo>(), Mask = 0x4500, Verb = (nint)(command - 1), VerbW = (nint)(command - 1), Show = 1 };
            Marshal.ThrowExceptionForHR(context.InvokeCommand(ref invoke));
        }
        finally { DestroyMenu(menu); }
    }

    private static NativeJumpItem? Describe(string owner, object item, bool task)
    {
        if (item is ILink link)
        {
            if (Property(item, PropertyFormat, 6) is "true") return null;
            var path = new StringBuilder(32768); var arguments = new StringBuilder(32768); var directory = new StringBuilder(32768);
            link.GetPath(path, path.Capacity, 0, 4); link.GetArguments(arguments, arguments.Capacity); link.GetWorkingDirectory(directory, directory.Capacity);
            var title = Property(item, new("f29f85e0-4ff9-1068-ab91-08002b27b3d9"), 2);
            var name = Name(title);
            if (name.Length == 0 && !task) name = Name(Path.GetFileName(path.ToString()));
            if (name.Length == 0) return null;
            var key = Key(owner, "link", path.ToString(), arguments.ToString(), directory.ToString(), Property(item, PropertyFormat, 5), Property(item, PropertyFormat, 20));
            return new(key, name, "", false, true);
        }
        if (task || item is not IItem shell) return null;
        var target = Display(shell, 0x80028000); var label = Name(Display(shell, 0));
        if (target.Length == 0 || label.Length == 0) return null;
        var isDirectory = Directory.Exists(target);
        var physical = isDirectory || File.Exists(target) ? target : "";
        return new(Key(owner, "item", target), label, physical, isDirectory, false);
    }

    private static string Name(string value)
    {
        if (value.StartsWith('@') && (value.StartsWith("@{", StringComparison.Ordinal) || value.Length > 3 && char.IsAsciiLetter(value[1]) && value[2] == ':'))
        {
            var resolved = new StringBuilder(1024);
            if (SHLoadIndirectString(value, resolved, (uint)resolved.Capacity, 0) >= 0) value = resolved.ToString();
        }
        return new string(value.Where(character => !char.IsControl(character)).Take(512).ToArray()).Trim();
    }
    private static string Key(params string[] values)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        foreach (var value in values) writer.Write(value);
        writer.Flush(); return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static string Property(object item, Guid format, uint id)
    {
        if (item is not IProperties properties) return "";
        var key = new PropertyKey { Format = format, Id = id };
        if (properties.GetValue(ref key, out var value) < 0) return "";
        try
        {
            return value.Type switch
            { 31 => Marshal.PtrToStringUni(value.Text) ?? "", 8 => value.Text == 0 ? "" : Marshal.PtrToStringBSTR(value.Text), 11 => value.Boolean != 0 ? "true" : "false", _ => "" };
        }
        finally { PropVariantClear(ref value); }
    }
    private static string Display(IItem item, uint kind)
    {
        if (item.GetDisplayName(kind, out var text) < 0) return "";
        try { return Marshal.PtrToStringUni(text) ?? ""; } finally { Marshal.FreeCoTaskMem(text); }
    }
    private static void Visit(IItems? items, Action<object> visit)
    {
        if (items == null || items.GetCount(out var count) < 0) return;
        for (uint index = 0; index < Math.Min(count, 64); index++)
        {
            object? item = null;
            try { var iid = new Guid("00000000-0000-0000-c000-000000000046"); if (items.GetAt(index, ref iid, out item) >= 0 && item != null) visit(item); }
            finally { Release(item); }
        }
    }
    private static object? Create(string id) => Activator.CreateInstance(Type.GetTypeFromCLSID(new(id))!);
    private static void Release(object? item) { if (item != null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item); }
}
