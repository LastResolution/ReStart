using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class App
{
    /// <summary>Exercises updater handoff and rollback using fictional installations, without ordinary settings, hooks or network requests.</summary>
    /// <param name="directory">New owned fixture root for installations, updater workspaces and the JSON report.</param>
    /// <param name="previousExecutable">Optional standalone preceding-release binary copied into each old installation. Null uses the current binary with a fictional old manifest.</param>
    /// <returns>A task completing after five process scenarios and owned-cache cleanup are verified. The transient-lock scenario always uses the current helper; the other four may use previousExecutable.</returns>
    private static async Task VerifyUpdateInstallationsAsync(string directory, string? previousExecutable = null)
    {
        Directory.CreateDirectory(directory); PortableUpdates.CacheRoot = Path.Combine(directory, "jobs");
        Directory.CreateDirectory(PortableUpdates.CacheRoot);
        var checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        checks += ReStart.App.MainWindow.VerifyProtectedUpdateControls(Path.Combine(directory, "protected-layouts"));
        var acknowledgedAfterFirstProbe = false; var reads = 0;
        Check(await PortableUpdates.WaitForAcknowledgementAsync(() => { reads++; return acknowledgedAfterFirstProbe; },
            () => { acknowledgedAfterFirstProbe = true; return true; }, TimeSpan.FromSeconds(1)), "Startup confirmation was lost between the marker and exit probes.");
        Check(reads == 2, "An exited replacement did not receive a final confirmation probe.");
        Check(!await PortableUpdates.WaitForAcknowledgementAsync(() => false, () => true, TimeSpan.FromSeconds(1)), "An unacknowledged exit was accepted.");
        var queriedExit = false;
        Check(await PortableUpdates.WaitForAcknowledgementAsync(() => true, () => { queriedExit = true; return true; }, TimeSpan.FromSeconds(1))
            && !queriedExit, "An already acknowledged startup unnecessarily depended on the replacement staying alive.");
        Check(!await PortableUpdates.WaitForAcknowledgementAsync(() => false, () => false, TimeSpan.Zero), "An expired startup deadline was accepted without confirmation.");
        var source = Environment.ProcessPath!;
        string? successfulWorkspace = null;
        var current = ReleaseVersion.Parse(AppBuildInfo.Version)!;
        var previous = previousExecutable != null
            ? ReleaseVersion.Parse(FileVersionInfo.GetVersionInfo(previousExecutable).ProductVersion) ?? throw new InvalidDataException("Previous fixture executable has no semantic version.")
            : current.Patch > 0 ? new ReleaseVersion(current.Major, current.Minor, current.Patch - 1)
            : current.Minor > 0 ? new ReleaseVersion(current.Major, current.Minor - 1, 0) : new ReleaseVersion(Math.Max(0, current.Major - 1), 0, 0);
        if (previous.CompareTo(current) >= 0) throw new InvalidOperationException("Fixture requires an older previous release.");
        foreach (var scenario in new[] { "success", "transient-lock", "startup-failure", "locked-file", "interrupted" })
        {
            var installation = Path.Combine(directory, scenario);
            Directory.CreateDirectory(Path.Combine(installation, "TileHost"));
            File.Copy(scenario == "transient-lock" ? source : previousExecutable ?? source, Path.Combine(installation, "ReStart.exe"), overwrite: true);
            File.WriteAllText(Path.Combine(installation, "TileHost", "ReStart.TileHost.exe"), "fictional worker");
            File.WriteAllText(Path.Combine(installation, "README.txt"), "old instructions");
            WriteFixtureManifest(installation, previous.ToString());
            File.WriteAllText(Path.Combine(installation, "local-sentinel.txt"), "private fixture sentinel");
            var id = Guid.NewGuid().ToString("N"); var workspace = PortableUpdates.Workspace(id);
            var payload = Path.Combine(workspace, "payload"); Directory.CreateDirectory(Path.Combine(payload, "TileHost"));
            File.Copy(source, Path.Combine(payload, "ReStart.exe"));
            File.WriteAllText(Path.Combine(payload, "TileHost", "ReStart.TileHost.exe"), "updated fictional worker");
            File.WriteAllText(Path.Combine(payload, "README.txt"), "new instructions");
            WriteFixtureManifest(payload, current.ToString());
            var manifest = UpdatePackage.ReadManifest(payload);
            PortableUpdates.FinalizePreparation(workspace, manifest, installation, fixture: true);
            Check(PortableUpdates.RestorePrepared(installation)?.Id == id, "Explicit preparation was not restored without applying it.");
            Check(UpdatePackage.ReadManifest(installation).Version == previous.ToString(), "Restoring preparation installed an update without consent.");
            Check(PortableUpdates.RestorePrepared(Path.Combine(directory, "another-installation")) == null, "An unrelated installation adopted this preparation.");
            if (scenario == "success")
            {
                var metadata = Directory.GetFiles(PortableUpdates.CacheRoot, "prepared-*.json").Single(); var savedIndex = File.ReadAllText(metadata);
                File.WriteAllText(metadata, "invalid"); Check(PortableUpdates.RestorePrepared(installation) == null, "Malformed readiness metadata was accepted.");
                File.WriteAllText(metadata, new string('x', 129)); Check(PortableUpdates.RestorePrepared(installation) == null, "Oversized readiness metadata was accepted.");
                File.WriteAllText(metadata, savedIndex);
                foreach (var candidate in new[] { Path.Combine(payload, "README.txt"), Path.Combine(workspace, "ReStart.exe") })
                {
                    var originalBytes = File.ReadAllBytes(candidate); File.AppendAllText(candidate, "modified");
                    Check(PortableUpdates.RestorePrepared(installation) == null, "A modified prepared payload or helper was accepted.");
                    var rejected = false;
                    try { await PortableUpdates.StartAsync(new(id, workspace, manifest.Version, installation)); }
                    catch (InvalidDataException) { rejected = true; }
                    Check(rejected && !File.Exists(Path.Combine(workspace, "accepted")), "Modified preparation reached an accepted handoff.");
                    File.WriteAllBytes(candidate, originalBytes);
                }
                Check(PortableUpdates.RestorePrepared(installation)?.Id == id, "Rejecting modified preparation consumed the restored valid package.");
            }
            if (scenario == "startup-failure") File.WriteAllText(Path.Combine(workspace, "simulate-startup-failure"), "fail");
            using var locked = scenario is "locked-file" or "transient-lock" ? new FileStream(Path.Combine(installation, "README.txt"), FileMode.Open, FileAccess.Read, FileShare.Read) : null;
            if (scenario == "interrupted")
            {
                new UpdateTransaction(installation, workspace).Apply();
                var request = PortableUpdates.ReadRequest(id) with { ParentId = 0, ParentStart = 0 };
                File.WriteAllText(Path.Combine(workspace, "request.json"), JsonSerializer.Serialize(request));
                Check(PortableUpdates.RedirectInterruptedStartup(installation), "Interrupted startup did not launch recovery.");
            }
            else
            {
                var start = new ProcessStartInfo(Path.Combine(installation, "ReStart.exe")) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var argument in new[] { "--verify-update-parent", id, "--verify-update-root", PortableUpdates.CacheRoot }) start.ArgumentList.Add(argument);
                using var parent = Process.Start(start) ?? throw new IOException("Fixture parent failed to start.");
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await parent.WaitForExitAsync(deadline.Token);
                Check(parent.ExitCode == 0, "Fixture parent did not hand off safely.");
            }
            if (scenario == "transient-lock")
            {
                // Start the hold interval only once the helper reaches replacement,
                // rather than measuring variable process startup/extraction time.
                for (var i = 0; i < 100 && !Directory.EnumerateFiles(installation, ".restart-copy-*").Any(); i++) await Task.Delay(100);
                Check(Directory.EnumerateFiles(installation, ".restart-copy-*").Any(), "Helper never reached the locked atomic replacement.");
                await Task.Delay(3500);
                Check(!File.Exists(Path.Combine(workspace, "outcome")), "Helper abandoned the transient lock before release.");
                locked!.Dispose();
            }
            var outcome = Path.Combine(workspace, "outcome");
            for (var i = 0; i < 600 && !File.Exists(outcome); i++) await Task.Delay(100);
            Check(File.Exists(outcome), "Updater did not finish the isolated scenario.");
            var successful = scenario is "success" or "transient-lock";
            var expectedOutcome = successful ? "complete" : "rolled-back";
            var actualOutcome = PortableUpdates.Outcome(id);
            var failurePath = Path.Combine(workspace, "failure-kind");
            var failure = File.Exists(failurePath) ? File.ReadAllText(failurePath) : "none";
            Check(actualOutcome == expectedOutcome,
                $"Updater returned {actualOutcome} for {scenario}, expected {expectedOutcome}; acknowledged={File.Exists(Path.Combine(workspace, "acknowledged"))}, started={File.Exists(Path.Combine(workspace, "fixture-started"))}, failure={failure}.");
            if (scenario == "locked-file" && previousExecutable == null)
            {
                using var context = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace, "failure-context.json")));
                var root = context.RootElement;
                Check(root.GetProperty("operation").GetString() == "replace" && root.GetProperty("file").GetString() == "README.txt"
                    && root.GetProperty("retries").GetInt32() > 0 && root.GetProperty("elapsedMs").GetInt64() >= 9_000,
                    "Persistent lock diagnostics lost their fixed operation, relative file or bounded retry evidence.");
                Check(root.EnumerateObject().Count() == 4, "Failure diagnostics contain unexpected fields.");
            }
            Check(UpdatePackage.ReadManifest(installation).Version == (successful ? current.ToString() : previous.ToString()), "Installation version was not committed or restored.");
            UpdatePackage.VerifyFiles(installation, UpdatePackage.ReadManifest(installation)); checks++;
            Check(!File.Exists(Path.Combine(installation, UpdateTransaction.PendingName)), "Pending marker survived a completed update.");
            Check(File.ReadAllText(Path.Combine(installation, "local-sentinel.txt")) == "private fixture sentinel", "Updater changed an unrelated local file.");
            Check(!File.Exists(Path.Combine(workspace, "backup", "local-sentinel.txt")), "Updater copied an unrelated local file.");
            var startupMarker = Path.Combine(workspace, successful ? "fixture-started" : "fixture-restored");
            for (var i = 0; i < 100 && !File.Exists(startupMarker); i++) await Task.Delay(100);
            Check(File.Exists(startupMarker), "Updated/restored app did not start through the isolated entry point.");
            if (scenario == "success") successfulWorkspace = workspace;
        }
        var unrelated = Path.Combine(successfulWorkspace!, "keep-local-file.txt"); File.WriteAllText(unrelated, "retained");
        PortableUpdates.CleanupFinished();
        Check(File.ReadAllText(unrelated) == "retained", "Cache cleanup removed an unrelated file.");
        Check(!File.Exists(Path.Combine(successfulWorkspace!, "ReStart.exe")), "Completed update binaries were not cleaned up.");
        Check(File.ReadAllText(Path.Combine(directory, "success", "local-sentinel.txt")) == "private fixture sentinel", "Cache cleanup changed the installed user's file.");
        File.WriteAllText(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(new { success = true, scenarios = 5, checks, previousExecutableVersion = previousExecutable == null ? null : previous.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteFixtureManifest(string installation, string version)
    {
        var files = Directory.EnumerateFiles(installation, "*", SearchOption.AllDirectories).Select(p => new PackageFile(
            Path.GetRelativePath(installation, p).Replace('\\', '/'), new FileInfo(p).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))).ToArray();
        File.WriteAllText(Path.Combine(installation, UpdatePackage.ManifestName), JsonSerializer.Serialize(new PackageManifest(1, version, AppBuildInfo.Runtime, files), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
