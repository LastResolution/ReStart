using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Reads OS-provided settings keywords and Control Panel identities without executing .settingcontent-ms files or arbitrary DeepLink commands.</summary>
internal static class WindowsSettingsCatalog
{
    internal sealed record Entry(AppEntry App, string Keywords);
    // URI facts from Microsoft's launch-settings reference, reviewed 2026-10-31.
    // https://learn.microsoft.com/windows/apps/develop/launch/launch-settings
    private const string DocumentedUris = "about activation apps-volume appsfeatures appsfeatures-app appsforwebsites assignedaccess autoplay backup batterysaver batterysaver-settings batterysaver-usagedetails bluetooth camera clipboard colors connecteddevices controlcenter crossdevice dateandtime defaultapps defaultbrowsersettings delivery-optimization delivery-optimization-activity delivery-optimization-advanced developers deviceencryption devices-touch devices-touchpad devicestyping-hwkbtextsuggestions deviceusage disksandvolumes display display-advanced display-advancedgraphics display-advancedgraphics-default easeofaccess-audio easeofaccess-closedcaptioning easeofaccess-colorfilter easeofaccess-colorfilter-adaptivecolorlink easeofaccess-colorfilter-bluelightlink easeofaccess-cursor easeofaccess-display easeofaccess-eyecontrol easeofaccess-hearingaids easeofaccess-highcontrast easeofaccess-keyboard easeofaccess-magnifier easeofaccess-mouse easeofaccess-mousepointer easeofaccess-narrator easeofaccess-narrator-isautostartenabled easeofaccess-speechrecognition easeofaccess-visualeffects emailandaccounts energyrecommendations extras family-group findmydevice fonts gaming-fullscreen gaming-gamebar gaming-gamedvr gaming-gamemode gaming-trueplay holographic-audio holographic-headset holographic-management holographic-startupandesktop keyboard keyboard-advanced lockscreen maps maps-downloadmaps mobile-devices mobile-devices-addphone mobile-devices-addphone-direct mousetouchpad multitasking multitasking-sgupdate network-advancedsettings network-airplanemode network-cellular network-dialup network-directaccess network-ethernet network-mobilehotspot network-proxy network-status network-vpn network-wifi network-wifisettings nightlight notifications optionalfeatures otherusers pen pen-button personalization personalization-background personalization-colors personalization-glance personalization-lighting personalization-navbar personalization-start personalization-start-places personalization-textinput personalization-textinput-copilot-hardwarekey personalization-touchkeyboard phone powersleep presence printers privacy privacy-accessoryapps privacy-accountinfo privacy-activityhistory privacy-advertisingid privacy-appdiagnostics privacy-automaticfiledownloads privacy-backgroundapps privacy-backgroundspatialperception privacy-broadfilesystemaccess privacy-calendar privacy-callhistory privacy-contacts privacy-customdevices privacy-documents privacy-downloadsfolder privacy-email privacy-eyetracker privacy-feedback privacy-general privacy-graphicscaptureprogrammatic privacy-graphicscapturewithoutborder privacy-holographic-environment privacy-location privacy-messaging privacy-microphone privacy-motion privacy-musiclibrary privacy-notifications privacy-phonecalls privacy-pictures privacy-radios privacy-speech privacy-speechtyping privacy-tasks privacy-videos privacy-voiceactivation privacy-webcam project provisioning proximity quiethours quietmomentsgame quietmomentspresentation quietmomentsscheduled recovery regionformatting regionlanguage regionlanguage-bpmfime regionlanguage-cangjieime regionlanguage-chsime-pinyin regionlanguage-chsime-pinyin-domainlexicon regionlanguage-chsime-pinyin-keyconfig regionlanguage-chsime-pinyin-udp regionlanguage-chsime-wubi regionlanguage-chsime-wubi-udp regionlanguage-jpnime regionlanguage-korime regionlanguage-quickime remotedesktop savelocations screenrotation search search-moredetails search-permissions signinoptions signinoptions-dynamiclock signinoptions-launchfaceenrollment signinoptions-launchfingerprintenrollment signinoptions-launchsecuritykeyenrollment sound sound-defaultinputproperties sound-defaultoutputproperties sound-devices sound-properties speech startupapps storagepolicies storagerecommendations storagesense sync tabletmode taskbar themes troubleshoot typing usb videoplayback wheel wifi-provisioning windowsanywhere windowsdefender windowsinsider windowsinsider-optin windowsupdate windowsupdate-action windowsupdate-activehours windowsupdate-history windowsupdate-optionalupdates windowsupdate-options windowsupdate-restartoptions windowsupdate-seekerondemand workplace workplace-provisioning workplace-repairtoken yourinfo";
    private static readonly HashSet<string> AllowedUris = DocumentedUris.Split(' ').ToHashSet(StringComparer.Ordinal);

    [SupportedOSPlatform("windows")]
    internal static Task<IReadOnlyList<Entry>> ReadAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<Entry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            IReadOnlyList<Entry> result;
            try { result = Read(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or InvalidOperationException or TargetInvocationException)
            { result = []; }
            completion.TrySetResult(result);
        })
        { IsBackground = true, Name = "ReStart Windows settings catalog" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }

    internal static IReadOnlyList<AppEntry> Find(IReadOnlyList<Entry> entries, string query)
    {
        var byId = entries.DistinctBy(e => e.App.Id).ToDictionary(e => e.App.Id);
        return AppSearch.Find(entries.Select(e => e.App), query, 12)
            .Concat(AppSearch.Find(entries.Select(e => e.App with { Name = e.Keywords }), query, 12).Select(e => byId[e.Id].App))
            .DistinctBy(e => e.Id).Take(12).ToArray();
    }

    internal static IReadOnlyList<Entry> ReadEntries(Stream input, Func<string, string> resource)
    {
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
        var document = XDocument.Load(reader); var result = new List<Entry>();
        if (document.Root?.Name.LocalName != "PCSettings") return result;
        foreach (var item in document.Root.Elements().Where(e => e.Name.LocalName == "SearchableContent").Take(4096))
        {
            var candidates = item.Descendants().Where(e => e.Name.LocalName == "PolicyIds")
                .SelectMany(e => e.Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            var page = candidates.FirstOrDefault(AllowedUris.Contains);
            if (page == null) continue;
            var label = item.Descendants().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value ?? "";
            label = resource(label).Trim();
            if (label.Length is 0 or > 512 || label.StartsWith('@') || label.Any(char.IsControl)) continue;
            var keywords = item.Descendants().Where(e => e.Name.LocalName is "HighKeywords" or "LowKeywords")
                .Select(e => resource(e.Value)).Where(s => !s.StartsWith('@')).Take(4);
            var id = "restart:setting:native:" + page + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(label)))[..16];
            var words = string.Join(' ', keywords);
            result.Add(new(new(id, label, "ms-settings:" + page, false), words[..Math.Min(words.Length, 8192)]));
        }
        return result.DistinctBy(e => e.App.Id).ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<Entry> Read()
    {
        var result = new List<Entry>();
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "ImmersiveControlPanel", "Settings");
        if (Directory.Exists(directory))
            foreach (var file in Directory.EnumerateFiles(directory, "AllSystemSettings_*.xml").Take(4))
                try
                {
                    if (new FileInfo(file).Length > 4_000_000 || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                    using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    result.AddRange(ReadEntries(stream, Resource));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException) { }
        result.AddRange(ControlPanel());
        return result.DistinctBy(e => e.App.Id).Take(4096).ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static string Resource(string value)
    {
        if (!value.StartsWith('@')) return value.Length <= 8192 ? value : "";
        if (value.Length > 2048) return "";
        var buffer = new StringBuilder(8192);
        return SHLoadIndirectString(value, buffer, (uint)buffer.Capacity, 0) >= 0 ? buffer.ToString() : "";
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<Entry> ControlPanel()
    {
        var result = new List<Entry>(); object? shell = null, folder = null, items = null;
        object? Call(object value, string name, params object[] args) => value.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, value, args);
        object? Get(object value, string name, params object[] args) => value.GetType().InvokeMember(name, BindingFlags.GetProperty, null, value, args);
        void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true)!);
            folder = Call(shell!, "NameSpace", "shell:ControlPanelFolder");
            if (folder == null) return result;
            items = Call(folder, "Items"); if (items == null) return result;
            var count = Math.Min((int)Get(items, "Count")!, 256);
            for (var i = 0; i < count; i++)
            {
                object? item = null;
                try
                {
                    item = Call(items, "Item", i); if (item == null) continue;
                    var name = Get(item, "Name") as string ?? ""; var path = Get(item, "Path") as string ?? "";
                    if (name.Length is 0 or > 512 || name.Any(char.IsControl) || !SafeControlPanelPath(path)) continue;
                    var canonical = Call(item, "ExtendedProperty", "System.ApplicationName") as string ?? "";
                    var id = "restart:setting:control-panel:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..24];
                    result.Add(new(new(id, name, "shell:" + path, false), Regex.Replace(canonical.Replace("Microsoft.", ""), "([a-z])([A-Z])", "$1 $2")));
                }
                catch (Exception ex) when (ex is COMException or TargetInvocationException or InvalidCastException) { }
                finally { Release(item); }
            }
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or InvalidCastException) { }
        finally { Release(items); Release(folder); Release(shell); }
        return result;
    }

    internal static bool SafeControlPanelPath(string path) => path.Length <= 512
        && (path.StartsWith("::{26EE0668-A00A-44D7-9371-BEB064C98683}", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("::{21EC2020-3AEA-1069-A2DD-08002B30309D}", StringComparison.OrdinalIgnoreCase))
        && Regex.IsMatch(path, @"^::\{[0-9A-Fa-f-]{36}\}(?:\\(?:[0-9]+|::\{[0-9A-Fa-f-]{36}\}))*$", RegexOptions.CultureInvariant);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SHLoadIndirectString(string source, StringBuilder output, uint characters, nint reserved);
}
