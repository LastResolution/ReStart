using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Coordinates the separately running portable updater; no shell, elevation or settings files are used.</summary>
internal static class PortableUpdates
{
    internal sealed record Request(string Installation, int ParentId, long ParentStart, string Version, string Runtime, bool Fixture = false);
    internal sealed record Prepared(string Id, string Directory, string Version);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static string CacheRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart.UpdateCache");
    internal static readonly Queue<DiagnosticCode> RecentCodes = new();
    internal static void Record(DiagnosticCode code) { lock (RecentCodes) { RecentCodes.Enqueue(code); while (RecentCodes.Count > 8) RecentCodes.Dequeue(); } }

    internal static bool SupportsInstallation(string installation)
    {
        try
        {
            var manifest = UpdatePackage.ReadManifest(installation);
            return manifest.Version == AppBuildInfo.Version && manifest.Runtime == AppBuildInfo.Runtime && !File.Exists(Path.Combine(installation, "ReStart.dll"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

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
        return new(id, directory, manifest.Version);
    }

    internal static async Task<bool> StartAsync(Prepared prepared)
    {
        var request = ReadRequest(prepared.Id);
        var start = StartInfo(Path.Combine(prepared.Directory, "ReStart.exe"));
        AddWorkerArguments(start, prepared.Id, request.Fixture);
        using var helper = Process.Start(start) ?? throw new IOException("Updater could not start.");
        for (var i = 0; i < 100 && !helper.HasExited; i++)
        {
            if (File.Exists(Path.Combine(prepared.Directory, "ready"))) return true;
            await Task.Delay(100);
        }
        return false;
    }

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
                if (File.Exists(Path.Combine(request.Installation, UpdateTransaction.PendingName)))
                { transaction.Rollback(); result = "rolled-back"; }
                else
                {
                    transaction.Apply();
                    replacement = Process.Start(RestartInfo(request, id, completing: true)) ?? throw new IOException("Replacement could not start.");
                    var acknowledged = false;
                    for (var i = 0; i < 1200; i++)
                    {
                        if (File.Exists(Path.Combine(directory, "acknowledged"))) { acknowledged = true; break; }
                        if (replacement.HasExited) break;
                        await Task.Delay(100);
                    }
                    if (!acknowledged)
                    {
                        if (!replacement.HasExited) { replacement.Kill(entireProcessTree: true); await replacement.WaitForExitAsync(); }
                        transaction.Rollback(); result = "rolled-back";
                    }
                    else { transaction.Complete(); result = "complete"; }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            if (replacement is { HasExited: false }) { replacement.Kill(entireProcessTree: true); await replacement.WaitForExitAsync(); }
            if (File.Exists(Path.Combine(directory, "transaction.json")))
            {
                try { transaction.Rollback(); result = "rolled-back"; }
                catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException or InvalidOperationException) { result = "recovery-failed"; }
            }
        }
        finally { replacement?.Dispose(); }
        File.WriteAllText(Path.Combine(directory, "outcome"), result);
        if (result != "complete" && result != "recovery-failed")
        {
            try { using var restored = Process.Start(RestartInfo(request, id, completing: false)); }
            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception) { return 3; }
        }
        return result == "complete" ? 0 : result == "rolled-back" ? 1 : 3;
    }

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

    internal static void Acknowledge(string id, string installation)
    {
        var request = ReadRequest(id);
        if (!Same(request.Installation, installation) || UpdatePackage.ReadManifest(installation).Version != request.Version)
            throw new InvalidDataException("Startup acknowledgment mismatch.");
        File.WriteAllText(Path.Combine(Workspace(id), "acknowledged"), "ready");
    }

    internal static string Outcome(string id)
    {
        var path = Path.Combine(Workspace(id), "outcome");
        return File.Exists(path) && new FileInfo(path).Length < 32 ? File.ReadAllText(path) : "failed";
    }
    internal static Request ReadRequest(string id)
    {
        var path = Path.Combine(Workspace(id), "request.json");
        if (new FileInfo(path).Length > 8192) throw new InvalidDataException("Update request too large.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllBytes(path), Json) ?? throw new InvalidDataException("Invalid update request.");
        if (!Path.IsPathFullyQualified(request.Installation) || !File.Exists(Path.Combine(request.Installation, UpdatePackage.ManifestName))
            || ReleaseVersion.Parse(request.Version) == null || request.Runtime is not ("win-x64" or "win-arm64")) throw new InvalidDataException("Invalid installation request.");
        return request;
    }
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
