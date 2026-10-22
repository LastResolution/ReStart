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
    /// <returns>A task completing after four process scenarios and owned-cache cleanup are verified.</returns>
    private static async Task VerifyUpdateInstallationsAsync(string directory, string? previousExecutable = null)
    {
        Directory.CreateDirectory(directory); PortableUpdates.CacheRoot = Path.Combine(directory, "jobs");
        Directory.CreateDirectory(PortableUpdates.CacheRoot);
        var checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        var source = Environment.ProcessPath!;
        string? successfulWorkspace = null;
        var current = ReleaseVersion.Parse(AppBuildInfo.Version)!;
        var previous = current.Patch > 0 ? new ReleaseVersion(current.Major, current.Minor, current.Patch - 1)
            : current.Minor > 0 ? new ReleaseVersion(current.Major, current.Minor - 1, 0) : new ReleaseVersion(Math.Max(0, current.Major - 1), 0, 0);
        if (previous.CompareTo(current) >= 0) throw new InvalidOperationException("Fixture requires a version after 0.0.0.");
        if (previousExecutable != null)
            Check(ReleaseVersion.Parse(FileVersionInfo.GetVersionInfo(previousExecutable).ProductVersion)?.ToString() == previous.ToString(),
                "Previous fixture executable does not match the preceding release version.");
        foreach (var scenario in new[] { "success", "startup-failure", "locked-file", "interrupted" })
        {
            var installation = Path.Combine(directory, scenario);
            Directory.CreateDirectory(Path.Combine(installation, "TileHost"));
            File.Copy(previousExecutable ?? source, Path.Combine(installation, "ReStart.exe"), overwrite: true);
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
            if (scenario == "startup-failure") File.WriteAllText(Path.Combine(workspace, "simulate-startup-failure"), "fail");
            using var locked = scenario == "locked-file" ? new FileStream(Path.Combine(installation, "README.txt"), FileMode.Open, FileAccess.Read, FileShare.Read) : null;
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
            var outcome = Path.Combine(workspace, "outcome");
            for (var i = 0; i < 600 && !File.Exists(outcome); i++) await Task.Delay(100);
            Check(File.Exists(outcome), "Updater did not finish the isolated scenario.");
            Check(File.ReadAllText(outcome) == (scenario == "success" ? "complete" : "rolled-back"), "Updater returned the wrong completion outcome.");
            Check(UpdatePackage.ReadManifest(installation).Version == (scenario == "success" ? current.ToString() : previous.ToString()), "Installation version was not committed or restored.");
            UpdatePackage.VerifyFiles(installation, UpdatePackage.ReadManifest(installation)); checks++;
            Check(!File.Exists(Path.Combine(installation, UpdateTransaction.PendingName)), "Pending marker survived a completed update.");
            Check(File.ReadAllText(Path.Combine(installation, "local-sentinel.txt")) == "private fixture sentinel", "Updater changed an unrelated local file.");
            Check(!File.Exists(Path.Combine(workspace, "backup", "local-sentinel.txt")), "Updater copied an unrelated local file.");
            var startupMarker = Path.Combine(workspace, scenario == "success" ? "fixture-started" : "fixture-restored");
            for (var i = 0; i < 100 && !File.Exists(startupMarker); i++) await Task.Delay(100);
            Check(File.Exists(startupMarker), "Updated/restored app did not start through the isolated entry point.");
            if (scenario == "success") successfulWorkspace = workspace;
        }
        var unrelated = Path.Combine(successfulWorkspace!, "keep-local-file.txt"); File.WriteAllText(unrelated, "retained");
        PortableUpdates.CleanupFinished();
        Check(File.ReadAllText(unrelated) == "retained", "Cache cleanup removed an unrelated file.");
        Check(!File.Exists(Path.Combine(successfulWorkspace!, "ReStart.exe")), "Completed update binaries were not cleaned up.");
        Check(File.ReadAllText(Path.Combine(directory, "success", "local-sentinel.txt")) == "private fixture sentinel", "Cache cleanup changed the installed user's file.");
        File.WriteAllText(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(new { success = true, scenarios = 4, checks, previousExecutableVersion = previousExecutable == null ? null : previous.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteFixtureManifest(string installation, string version)
    {
        var files = Directory.EnumerateFiles(installation, "*", SearchOption.AllDirectories).Select(p => new PackageFile(
            Path.GetRelativePath(installation, p).Replace('\\', '/'), new FileInfo(p).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))).ToArray();
        File.WriteAllText(Path.Combine(installation, UpdatePackage.ManifestName), JsonSerializer.Serialize(new PackageManifest(1, version, AppBuildInfo.Runtime, files), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
