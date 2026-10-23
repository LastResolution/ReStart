using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Coordinates the separately running portable updater; no shell, elevation or settings files are used.</summary>
internal static partial class PortableUpdates
{
    /// <summary>Stores local handoff metadata; never included in a copied diagnostic report.</summary>
    /// <param name="Installation">Absolute portable installation directory whose manifest owns the replaced files.</param>
    /// <param name="ParentId">Process to wait for before mutation; paired with ParentStart to reject PID reuse.</param>
    /// <param name="ParentStart">Parent creation time as UTC DateTime ticks.</param>
    /// <param name="Version">Verified candidate semantic version.</param>
    /// <param name="Runtime">Installed win-x64 or win-arm64 architecture.</param>
    /// <param name="Fixture">Whether restart uses isolated verification entry points instead of normal user settings.</param>
    /// <param name="Language">Supported UI language used by recovery prompts; defaults to English for old fixtures.</param>
    /// <param name="Light">Resolved light/dark caption mode when the user confirms restart.</param>
    /// <param name="Colors">Optional whitelist of ARGB palette colors; null leaves the helper's default palette.</param>
    internal sealed record Request(string Installation, int ParentId, long ParentStart, string Version, string Runtime, bool Fixture = false,
        string Language = "en", bool Light = false, Dictionary<string, string>? Colors = null);
    internal static readonly string[] PaletteKeys = ["TextBrush", "MutedBrush", "PanelBrush", "TileBrush", "AccentBrush", "AccentTextBrush", "FocusAccentBrush", "ControlBrush", "BorderBrush", "HoverBrush", "ScrollThumbBrush"];
    /// <summary>Identifies one prepared workspace for explicit restart.</summary>
    /// <param name="Id">N-format GUID used beneath the update cache, never an arbitrary command line.</param>
    /// <param name="Directory">Absolute owned workspace containing the validated payload and helper copy.</param>
    /// <param name="Version">Candidate semantic version displayed in the restart action.</param>
    /// <param name="Installation">Bound installation directory; null is reserved for nonexecuting UI fixtures.</param>
    internal sealed record Prepared(string Id, string Directory, string Version, string? Installation = null);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static string CacheRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart.UpdateCache");
    internal static readonly Queue<DiagnosticCode> RecentCodes = new();
    /// <summary>Retains only the eight most recent classified failures in memory.</summary>
    /// <param name="code">Fixed error category; never an exception message or user-supplied string.</param>
    internal static void Record(DiagnosticCode code) { lock (RecentCodes) { RecentCodes.Enqueue(code); while (RecentCodes.Count > 8) RecentCodes.Dequeue(); } }

    /// <summary>Checks inexpensive distribution prerequisites without downloading or probing writes.</summary>
    /// <param name="installation">Current executable directory; it is read but not modified.</param>
    /// <returns>True for a matching portable inventory without the developer DLL; false for missing/unreadable inventories.</returns>
    internal static bool SupportsInstallation(string installation)
    {
        try
        {
            var manifest = UpdatePackage.ReadManifest(installation);
            return manifest.Version == AppBuildInfo.Version && manifest.Runtime == AppBuildInfo.Runtime && !File.Exists(Path.Combine(installation, "ReStart.dll"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Checks the executable identity and creates the local helper handoff after archive validation.</summary>
    /// <param name="directory">Fresh N-GUID workspace immediately beneath CacheRoot, containing payload.</param>
    /// <param name="manifest">Inventory already validated by UpdateDownload; version must be newer than the installation.</param>
    /// <param name="installation">Current portable installation; all distribution hashes are checked before copying its helper.</param>
    /// <param name="fixture">True only for isolated process verification with fictional installation files.</param>
    /// <returns>A prepared workspace; the installation itself remains unchanged.</returns>
    /// <exception cref="IOException">Inventory, version, architecture or filesystem validation fails.</exception>
    internal static Prepared FinalizePreparation(string directory, PackageManifest manifest, string installation, bool fixture = false)
    {
        var id = Path.GetFileName(directory);
        if (!Guid.TryParseExact(id, "N", out _) || !Same(directory, Path.Combine(CacheRoot, id))) throw new InvalidDataException("Invalid update workspace.");
        var previous = UpdatePackage.ReadManifest(installation);
        if (previous.Runtime != manifest.Runtime || ReleaseVersion.Parse(manifest.Version)!.CompareTo(ReleaseVersion.Parse(previous.Version)) <= 0)
            throw new InvalidDataException("Ineligible update package.");
        // Refuse modified installations before copying any helper or offering restart.
        UpdatePackage.VerifyFiles(installation, previous);
        var executable = Path.Combine(directory, "payload", "ReStart.exe");
        if (ReleaseVersion.Parse(FileVersionInfo.GetVersionInfo(executable).ProductVersion)?.ToString() != manifest.Version)
            throw new InvalidDataException("Executable version differs from its manifest.");
        using (var image = new PEReader(File.OpenRead(executable)))
            if (image.PEHeaders.CoffHeader.Machine != (manifest.Runtime == "win-arm64" ? Machine.Arm64 : Machine.Amd64))
                throw new InvalidDataException("Executable architecture differs from its manifest.");
        using var process = Process.GetCurrentProcess();
        var request = new Request(Path.GetFullPath(installation), process.Id, process.StartTime.ToUniversalTime().Ticks, manifest.Version, manifest.Runtime, fixture);
        File.WriteAllText(Path.Combine(directory, "request.json"), JsonSerializer.Serialize(request, Json));
        File.Copy(Path.Combine(installation, "ReStart.exe"), Path.Combine(directory, "ReStart.exe"), overwrite: false);
        var zip = Path.Combine(directory, "package.zip"); if (File.Exists(zip)) File.Delete(zip);
        RememberPrepared(id, installation);
        return new(id, directory, manifest.Version, Path.GetFullPath(installation));
    }

    /// <summary>Starts the cached helper and explicitly accepts its readiness handshake.</summary>
    /// <param name="prepared">Validated workspace returned by FinalizePreparation or RestorePrepared; its installation boundary is checked again.</param>
    /// <returns>True when the helper is ready and accepted; only then may the caller exit for installation.</returns>
    /// <exception cref="IOException">Handoff files cannot be read or written.</exception>
    internal static async Task<bool> StartAsync(Prepared prepared)
    {
        var request = ReadRequest(prepared.Id, prepared.Installation ?? AppContext.BaseDirectory);
        await Task.Run(() => ValidatePrepared(prepared, request));
        using var parent = Process.GetCurrentProcess();
        request = request with
        {
            ParentId = parent.Id,
            ParentStart = parent.StartTime.ToUniversalTime().Ticks,
            Language = L.Language,
            Light = ThemeWindow.IsLight,
            Colors = PaletteKeys.Where(k => System.Windows.Application.Current.Resources[k] is System.Windows.Media.SolidColorBrush)
                .ToDictionary(k => k, k => ((System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources[k]).Color.ToString())
        };
        File.WriteAllText(Path.Combine(prepared.Directory, "request.json"), JsonSerializer.Serialize(request, Json));
        File.Delete(Path.Combine(prepared.Directory, "ready"));
        File.Delete(Path.Combine(prepared.Directory, "accepted"));
        var start = StartInfo(Path.Combine(prepared.Directory, "ReStart.exe"));
        AddWorkerArguments(start, prepared.Id, request.Fixture);
        using var helper = Process.Start(start) ?? throw new IOException("Updater could not start.");
        for (var i = 0; i < 100 && !helper.HasExited; i++)
        {
            if (File.Exists(Path.Combine(prepared.Directory, "ready")))
            { File.WriteAllText(Path.Combine(prepared.Directory, "accepted"), "accepted"); ForgetPrepared(prepared.Id, request.Installation); return true; }
            await Task.Delay(100);
        }
        return false;
    }

    /// <summary>Waits for the old process, applies/restores its transaction, and verifies replacement startup.</summary>
    /// <param name="id">N-GUID of the accepted job beneath CacheRoot; concurrent helpers are rejected by leases.</param>
    /// <returns>0 for committed, 1 for restored, 2 for an unaccepted/timed-out handoff, or 3 for repair required.</returns>
    /// <exception cref="IOException">The initial lease or request cannot be acquired/read before transaction handling begins.</exception>
    internal static async Task<int> RunAsync(string id)
    {
        var directory = Workspace(id);
        using var lease = new FileStream(Path.Combine(directory, "lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var request = ReadRequest(id);
        var transaction = new UpdateTransaction(request.Installation, directory);
        var result = "failed";
        Process? replacement = null;
        try
        {
            // Acquire the installation guard before telling the old process to exit.
            using (var guard = new FileStream(Path.Combine(request.Installation, ".restart-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                File.WriteAllText(Path.Combine(directory, "ready"), "ready");
                if (!await WaitForParentAsync(request)) return 2;
                if (!File.Exists(Path.Combine(directory, "accepted")) && !File.Exists(Path.Combine(request.Installation, UpdateTransaction.PendingName))) return 2;
                if (File.Exists(Path.Combine(request.Installation, UpdateTransaction.PendingName)))
                { transaction.Rollback(); result = "rolled-back"; }
                else
                {
                    transaction.Apply();
                    replacement = Process.Start(RestartInfo(request, id, completing: true)) ?? throw new IOException("Replacement could not start.");
                    var acknowledged = await WaitForAcknowledgementAsync(() => File.Exists(Path.Combine(directory, "acknowledged")),
                        () => replacement.HasExited, TimeSpan.FromSeconds(120));
                    if (!acknowledged)
                    {
                        if (!replacement.HasExited) { replacement.Kill(entireProcessTree: true); await replacement.WaitForExitAsync(); }
                        transaction.Rollback(); result = "rolled-back";
                    }
                    else { transaction.Complete(); result = "complete"; }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            if (request.Fixture)
                File.WriteAllText(Path.Combine(directory, "failure-kind"), ex is System.ComponentModel.Win32Exception native
                    ? $"win32-{native.NativeErrorCode}" : ex is IOException io ? $"io-{io.HResult:X8}" : ex.GetType().Name);
            if (replacement is { HasExited: false }) { replacement.Kill(entireProcessTree: true); await replacement.WaitForExitAsync(); }
            if (File.Exists(Path.Combine(directory, "transaction.json")))
            {
                try { transaction.Rollback(); result = "rolled-back"; }
                catch (Exception rollback) when (rollback is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException) { result = "recovery-failed"; }
            }
        }
        finally { replacement?.Dispose(); }
        File.WriteAllText(Path.Combine(directory, "outcome"), result);
        if (result != "complete" && result != "recovery-failed")
        {
            try { using var restored = Process.Start(RestartInfo(request, id, completing: false)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.ComponentModel.Win32Exception) { return 3; }
        }
        return result == "complete" ? 0 : result == "rolled-back" ? 1 : 3;
    }

    /// <summary>Defers ordinary startup while an update is active, or starts recovery of an interrupted journal.</summary>
    /// <param name="installation">Actual executable directory; it must match any stored recovery request.</param>
    /// <returns>True when this launcher must exit without reading settings or installing hooks.</returns>
    /// <exception cref="IOException">The pending marker/request is invalid or recovery cannot be launched.</exception>
    internal static bool RedirectInterruptedStartup(string installation)
    {
        var marker = Path.Combine(installation, UpdateTransaction.PendingName);
        if (!File.Exists(marker))
        {
            var guardPath = Path.Combine(installation, ".restart-update.lock");
            if (!File.Exists(guardPath)) return false;
            try { using var guard = new FileStream(guardPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return false; }
            catch (IOException) { return true; }
        }
        if (new FileInfo(marker).Length > 64) throw new InvalidDataException("Invalid recovery marker.");
        var id = File.ReadAllText(marker);
        var request = ReadRequest(id);
        if (!Same(request.Installation, installation)) throw new InvalidDataException("Recovery installation mismatch.");
        // A living helper owns the installation guard; do not start a second transaction.
        try { using var guard = new FileStream(Path.Combine(installation, ".restart-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return true; }
        var start = StartInfo(Path.Combine(Workspace(id), "ReStart.exe")); AddWorkerArguments(start, id, request.Fixture);
        using var process = Process.Start(start) ?? throw new IOException("Recovery could not start.");
        return true;
    }

    /// <summary>Signals successful candidate initialization before settings writes are enabled.</summary>
    /// <param name="id">Active update's N-GUID.</param>
    /// <param name="installation">Initialized executable directory, which must match the request and version.</param>
    /// <exception cref="IOException">Identity checks fail or the acknowledgment cannot be written.</exception>
    internal static void Acknowledge(string id, string installation)
    {
        var request = ReadRequest(id);
        if (!Same(request.Installation, installation) || UpdatePackage.ReadManifest(installation).Version != request.Version)
            throw new InvalidDataException("Startup acknowledgment mismatch.");
        File.WriteAllText(Path.Combine(Workspace(id), "acknowledged"), "ready");
    }

    /// <summary>Reads a bounded local outcome without reading a log or exception text.</summary>
    /// <param name="id">N-GUID of an existing local update workspace.</param>
    /// <returns>A short outcome category, or failed when no bounded result is present.</returns>
    internal static string Outcome(string id)
    {
        var path = Path.Combine(Workspace(id), "outcome");
        return File.Exists(path) && new FileInfo(path).Length < 32 ? File.ReadAllText(path) : "failed";
    }
    /// <summary>Best-effort deletion of completed inventory-owned cache files; active/failed jobs and unrelated files remain.</summary>
    internal static void CleanupFinished()
    {
        if (!Directory.Exists(CacheRoot)) return;
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(CacheRoot).Take(32))
            {
                var id = Path.GetFileName(directory);
                if (!Guid.TryParseExact(id, "N", out _)) continue;
                try
                {
                    Workspace(id);
                    if (Outcome(id) is not ("complete" or "rolled-back")) continue;
                    var leasePath = Path.Combine(directory, "lease");
                    using (var lease = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        ForgetPrepared(id, ReadRequest(id).Installation);
                        foreach (var name in new[] { "payload", "backup" })
                        {
                            var root = Path.Combine(directory, name);
                            if (!Directory.Exists(root)) continue;
                            var manifest = UpdatePackage.ReadManifest(root);
                            var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
                            foreach (var file in manifest.Files.Select(f => f.Path).Append(UpdatePackage.ManifestName))
                            {
                                var path = Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar));
                                // Only inventory-owned files are deleted, and never through a junction.
                                for (var parent = path; parent != directory; parent = Path.GetDirectoryName(parent)!)
                                {
                                    if ((File.Exists(parent) || Directory.Exists(parent)) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                                        throw new IOException("Linked update cleanup path.");
                                }
                                File.Delete(path);
                                for (var parent = Path.GetDirectoryName(path); parent != directory; parent = Path.GetDirectoryName(parent)) parents.Add(parent!);
                            }
                            foreach (var parent in parents.OrderByDescending(p => p.Length))
                                if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
                        }
                        foreach (var name in new[] { "ReStart.exe", "package.zip", "request.json", "ready", "accepted", "acknowledged", "outcome", "transaction.json" })
                            File.Delete(Path.Combine(directory, name));
                    }
                    File.Delete(leasePath);
                    if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
    }
    /// <summary>Reads a bounded local request after validating its workspace and basic installation identity.</summary>
    /// <param name="id">N-GUID immediately beneath CacheRoot.</param>
    /// <returns>Validated local handoff metadata; its private installation path is never added to diagnostic reports.</returns>
    /// <exception cref="IOException">The request is absent, oversized or invalid.</exception>
    /// <param name="expectedInstallation">Optional current installation boundary checked before probing any request-supplied path.</param>
    internal static Request ReadRequest(string id, string? expectedInstallation = null)
    {
        var path = Path.Combine(Workspace(id), "request.json");
        if (new FileInfo(path).Length > 8192) throw new InvalidDataException("Update request too large.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllBytes(path), Json) ?? throw new InvalidDataException("Invalid update request.");
        if (!Path.IsPathFullyQualified(request.Installation) || expectedInstallation != null && !Same(request.Installation, expectedInstallation) || !File.Exists(Path.Combine(request.Installation, UpdatePackage.ManifestName))
            || ReleaseVersion.Parse(request.Version) == null || request.Runtime is not ("win-x64" or "win-arm64")
            || !TextCatalog.SupportedLanguages.Contains(request.Language)
            || request.Colors != null && request.Colors.Any(pair => !PaletteKeys.Contains(pair.Key) || pair.Value is not { Length: 9 }
                || pair.Value[0] != '#' || !pair.Value.AsSpan(1).ToArray().All(char.IsAsciiHexDigit))) throw new InvalidDataException("Invalid installation request.");
        return request;
    }
    /// <summary>Resolves only canonical job identifiers and rejects links in their cache ancestry.</summary>
    /// <param name="id">Exactly an N-format GUID; paths and other strings are rejected.</param>
    /// <returns>Absolute owned workspace path, whether or not the directory exists yet.</returns>
    /// <exception cref="IOException">The identifier is invalid or a path component is a link.</exception>
    internal static string Workspace(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid update identifier.");
        var directory = Path.Combine(CacheRoot, id);
        for (var path = Path.GetFullPath(directory); path != null; path = Path.GetDirectoryName(path))
            if ((Directory.Exists(path) || File.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked update workspace.");
        return directory;
    }
    private static async Task<bool> WaitForParentAsync(Request request)
    {
        try
        {
            using var parent = Process.GetProcessById(request.ParentId);
            if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStart) return true;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await parent.WaitForExitAsync(timeout.Token); return true; }
            catch (OperationCanceledException) { return false; }
        }
        catch (ArgumentException) { return true; }
    }
    private static ProcessStartInfo RestartInfo(Request request, string id, bool completing)
    {
        var start = StartInfo(Path.Combine(request.Installation, "ReStart.exe"));
        start.ArgumentList.Add(completing ? "--complete-update" : "--update-result"); start.ArgumentList.Add(id);
        if (request.Fixture) { start.ArgumentList.Add("--verify-update-root"); start.ArgumentList.Add(CacheRoot); }
        return start;
    }
    private static void AddWorkerArguments(ProcessStartInfo start, string id, bool fixture)
    {
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(id);
        if (fixture) { start.ArgumentList.Add("--verify-update-root"); start.ArgumentList.Add(CacheRoot); }
    }
    private static ProcessStartInfo StartInfo(string executable) => new(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
    private static bool Same(string first, string second) => Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
