using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class UpdatePipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ReStart-update-test-" + Guid.NewGuid().ToString("N"));
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public UpdatePipelineTests() => Directory.CreateDirectory(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private static void Package(string root, string version, string runtime = "win-x64", bool extra = false)
    {
        Directory.CreateDirectory(Path.Combine(root, "TileHost"));
        File.WriteAllText(Path.Combine(root, "ReStart.exe"), "application " + version);
        File.WriteAllText(Path.Combine(root, "TileHost", "ReStart.TileHost.exe"), "worker " + version);
        if (extra) File.WriteAllText(Path.Combine(root, "new.txt"), "new distribution file");
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => new PackageFile(
            Path.GetRelativePath(root, path).Replace('\\', '/'), new FileInfo(path).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))).ToArray();
        File.WriteAllText(Path.Combine(root, UpdatePackage.ManifestName), JsonSerializer.Serialize(new PackageManifest(1, version, runtime, files), Json));
    }
    private string MakeArchive(string version = "1.1.0", string runtime = "win-x64")
    {
        var package = Path.Combine(_root, Guid.NewGuid().ToString("N")); Package(package, version, runtime);
        var zip = package + ".zip"; ZipFile.CreateFromDirectory(package, zip); return zip;
    }
    private (string Install, string Work, UpdateTransaction Transaction) Transaction(bool extra = true)
    {
        var install = Path.Combine(_root, "install"); var work = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Package(install, "1.0.0"); Package(Path.Combine(work, "payload"), "1.1.0", extra: extra);
        return (install, work, new(install, work));
    }
    [Fact]
    public void ExtractsCompleteDistributionAndRejectsChangedFiles()
    {
        var zip = MakeArchive(); var output = Path.Combine(_root, "extract");
        var manifest = UpdatePackage.Extract(zip, output, "1.1.0", "win-x64");
        Assert.Equal(2, manifest.Files.Length);
        File.AppendAllText(Path.Combine(output, "ReStart.exe"), "changed");
        Assert.Throws<InvalidDataException>(() => UpdatePackage.VerifyFiles(output, manifest));
    }
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/outside.txt")]
    [InlineData("C:/outside.txt")]
    [InlineData("folder\\outside.txt")]
    [InlineData("folder/../outside.txt")]
    [InlineData("ReStart.exe:secret")]
    [InlineData("CON.txt")]
    [InlineData("folder./outside")]
    [InlineData("folder /outside")]
    public void RejectsEscapingAndAmbiguousArchiveNamesBeforeExtraction(string name)
    {
        var zip = MakeArchive(); using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update)) archive.CreateEntry(name);
        var output = Path.Combine(_root, "extract");
        Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, output, "1.1.0", "win-x64"));
        Assert.False(Directory.Exists(output));
    }
    [Fact]
    public void RejectsSymlinksAndCaseInsensitiveDuplicateEntries()
    {
        var zip = MakeArchive();
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update)) archive.CreateEntry("link").ExternalAttributes = unchecked((int)0xA1FF0000);
        Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, Path.Combine(_root, "extract"), "1.1.0", "win-x64"));
        zip = MakeArchive(); using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update)) archive.CreateEntry("RESTART.EXE");
        Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, Path.Combine(_root, "extract"), "1.1.0", "win-x64"));
    }
    [Theory]
    [InlineData("1.2.0", "win-x64")]
    [InlineData("1.1.0", "win-arm64")]
    public void RejectsWrongVersionOrArchitectureAndCleansExtraction(string version, string runtime)
    {
        var zip = MakeArchive(); var output = Path.Combine(_root, "extract");
        Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, output, version, runtime)); Assert.False(Directory.Exists(output));
    }
    [Fact]
    public void RejectsUnlistedFilesAndOversizedInventories()
    {
        var zip = MakeArchive(); using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update)) archive.CreateEntry("unlisted.txt");
        Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, Path.Combine(_root, "extract"), "1.1.0", "win-x64"));
        var source = Path.Combine(_root, "source"); Package(source, "1.1.0");
        var manifest = UpdatePackage.ReadManifest(source);
        File.WriteAllText(Path.Combine(source, UpdatePackage.ManifestName), JsonSerializer.Serialize(manifest with { Files = [new("ReStart.exe", long.MaxValue, new string('0', 64))] }, Json));
        Assert.Throws<InvalidDataException>(() => UpdatePackage.ReadManifest(source));
    }
    [Fact]
    public void CompletesOnlyAfterAcknowledgmentAndPreservesLocalFiles()
    {
        var (install, work, transaction) = Transaction();
        File.WriteAllText(Path.Combine(install, "personal.txt"), "private sentinel");
        transaction.Apply();
        Assert.Equal("1.1.0", UpdatePackage.ReadManifest(install).Version);
        Assert.True(File.Exists(Path.Combine(install, UpdateTransaction.PendingName)));
        Assert.False(File.Exists(Path.Combine(work, "backup", "personal.txt")));
        transaction.Complete();
        Assert.False(File.Exists(Path.Combine(install, UpdateTransaction.PendingName)));
        Assert.Equal("private sentinel", File.ReadAllText(Path.Combine(install, "personal.txt")));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AnyReplacementFailureRestoresOldDistribution(int failingStep)
    {
        var (install, _, transaction) = Transaction();
        Assert.Throws<IOException>(() => transaction.Apply(count => { if (count == failingStep) throw new IOException("simulated disk failure"); }));
        Assert.Equal("1.0.0", UpdatePackage.ReadManifest(install).Version);
        UpdatePackage.VerifyFiles(install, UpdatePackage.ReadManifest(install));
        Assert.False(File.Exists(Path.Combine(install, "new.txt")));
        Assert.False(File.Exists(Path.Combine(install, UpdateTransaction.PendingName)));
    }
    [Fact]
    public void FailedStartupAndInterruptedRollbackAreRecoverable()
    {
        var (install, work, transaction) = Transaction(); transaction.Apply();
        new UpdateTransaction(install, work).Rollback();
        new UpdateTransaction(install, work).Rollback();
        Assert.Equal("1.0.0", UpdatePackage.ReadManifest(install).Version);
        Assert.False(File.Exists(Path.Combine(install, "new.txt")));
    }
    [Fact]
    public void ModifiedDistributionAndNewLocalFileCollisionFailBeforeMutation()
    {
        var (install, _, transaction) = Transaction();
        File.WriteAllText(Path.Combine(install, "new.txt"), "user file");
        Assert.Throws<InvalidDataException>(() => transaction.Apply());
        Assert.Equal("user file", File.ReadAllText(Path.Combine(install, "new.txt")));
        File.Delete(Path.Combine(install, "new.txt"));
        File.AppendAllText(Path.Combine(install, "ReStart.exe"), "modified");
        Assert.Throws<InvalidDataException>(() => transaction.Apply());
        Assert.False(File.Exists(Path.Combine(install, UpdateTransaction.PendingName)));
    }
    [Fact]
    public void CorruptBackupIsNeverUsedForRollback()
    {
        var (install, work, transaction) = Transaction(); transaction.Apply();
        File.WriteAllText(Path.Combine(work, "backup", "ReStart.exe"), "corrupt");
        Assert.Throws<InvalidDataException>(() => transaction.Rollback());
        Assert.True(File.Exists(Path.Combine(install, UpdateTransaction.PendingName)));
        Assert.Equal("application 1.1.0", File.ReadAllText(Path.Combine(install, "ReStart.exe")));
    }
    [Fact]
    public void RemovesStaleOwnedFilesButRestoresThemOnRollback()
    {
        var (install, work, _) = Transaction();
        File.WriteAllText(Path.Combine(install, "stale.txt"), "old file");
        File.Delete(Path.Combine(install, UpdatePackage.ManifestName));
        var owned = Directory.EnumerateFiles(install, "*", SearchOption.AllDirectories).Select(p => new PackageFile(Path.GetRelativePath(install, p).Replace('\\', '/'), new FileInfo(p).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))).ToArray();
        File.WriteAllText(Path.Combine(install, UpdatePackage.ManifestName), JsonSerializer.Serialize(new PackageManifest(1, "1.0.0", "win-x64", owned), Json));
        var transaction = new UpdateTransaction(install, work); transaction.Apply();
        Assert.False(File.Exists(Path.Combine(install, "stale.txt"))); transaction.Rollback();
        Assert.Equal("old file", File.ReadAllText(Path.Combine(install, "stale.txt")));
    }
    [Fact]
    public async Task DownloaderUsesFixedRepositoryAndChecksHashBeforeExtracting()
    {
        var zip = File.ReadAllBytes(MakeArchive()); var requests = new List<Uri>();
        using var http = new HttpClient(new Handler(request =>
        {
            requests.Add(request.RequestUri!); Assert.Null(request.Headers.Authorization);
            Assert.Equal("github.com", request.RequestUri!.Host);
            var content = request.RequestUri.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
                ? Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(zip)) + "  ReStart-win-x64.zip\n") : zip;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
        }));
        var output = Path.Combine(_root, "download");
        var result = await new UpdateDownload(http).PrepareAsync(new("v1.1.0", ReleaseVersion.Parse("1.1.0")!, "ignored"), "win-x64", output);
        Assert.Equal("1.1.0", result.Version); Assert.Equal(2, requests.Count);
        Assert.All(requests, u => Assert.StartsWith("/LastResolution/ReStart/releases/download/v1.1.0/ReStart-win-x64.zip", u.AbsolutePath));
    }
    [Theory]
    [InlineData("https://untrusted.example/payload")]
    [InlineData("http://github.com/LastResolution/ReStart/payload")]
    [InlineData("https://github.com:444/payload")]
    [InlineData("https://user@github.com/payload")]
    public async Task DownloaderRejectsUnsafeRedirectWithoutFollowingIt(string address)
    {
        var count = 0;
        using var http = new HttpClient(new Handler(_ => { count++; var response = new HttpResponseMessage(HttpStatusCode.Found); response.Headers.Location = new(address); return response; }));
        var output = Path.Combine(_root, "download");
        await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateDownload(http).PrepareAsync(new("v1.1.0", ReleaseVersion.Parse("1.1.0")!, ""), "win-x64", output));
        Assert.Equal(1, count); Assert.False(Directory.Exists(output));
    }
    [Fact]
    public async Task ChecksumMismatchAndCancellationNeverTouchInstallation()
    {
        using var http = new HttpClient(new Handler(request => new(HttpStatusCode.OK)
        { Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal) ? new string('0', 64) + "  ReStart-win-x64.zip" : "corrupt zip") }));
        var output = Path.Combine(_root, "download");
        var download = new UpdateDownload(http); var release = new UpdateRelease("v1.1.0", ReleaseVersion.Parse("1.1.0")!, "");
        await Assert.ThrowsAsync<InvalidDataException>(() => download.PrepareAsync(release, "win-x64", output)); Assert.False(Directory.Exists(output));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download.PrepareAsync(release, "win-x64", output, cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(output));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(response(request)); }
    }
}
