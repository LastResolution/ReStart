using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ReStart.Core;

/// <summary>Downloads explicit, user-requested packages from the fixed public repository.</summary>
/// <param name="http">Caller-owned credential-free HTTP client with automatic redirects disabled; it is never disposed here.</param>
public sealed class UpdateDownload(HttpClient http)
{
    /// <summary>Downloads a ZIP and SHA-256 sidecar, verifies the checksum and extracts the distribution.</summary>
    /// <param name="release">Eligible release selected by ReleaseClient; its supplied notes and URLs are not executed.</param>
    /// <param name="runtime">Installed package architecture: win-x64 or win-arm64.</param>
    /// <param name="directory">Fresh, nonexistent staging directory owned by the caller.</param>
    /// <param name="progress">Optional observer receiving ZIP bytes downloaded; callbacks run on the caller's async context.</param>
    /// <param name="cancellationToken">Cancels network reads or download; cancellation leaves the installation untouched.</param>
    /// <returns>Validated inventory in directory/payload; only successful results may be offered for installation.</returns>
    /// <exception cref="IOException">Download, checksum, package validation or disk access fails.</exception>
    /// <exception cref="HttpRequestException">The fixed release service cannot supply a successful response.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels or the HTTP client times out.</exception>
    public async Task<PackageManifest> PrepareAsync(UpdateRelease release, string runtime, string directory, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (ReleaseVersion.Parse(release.Tag) != release.Version || runtime is not ("win-x64" or "win-arm64") || Directory.Exists(directory))
            throw new InvalidDataException("Invalid staging request.");
        UpdatePackage.NoLinks(Path.GetDirectoryName(Path.GetFullPath(directory))!);
        Directory.CreateDirectory(directory);
        var name = $"ReStart-{runtime}.zip";
        var basis = $"https://github.com/LastResolution/ReStart/releases/download/{Uri.EscapeDataString(release.Tag)}/{name}";
        try
        {
            using var checksum = new MemoryStream();
            await DownloadAsync(new(basis + ".sha256"), checksum, 1024, null, cancellationToken);
            var parts = Encoding.UTF8.GetString(checksum.ToArray()).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !UpdatePackage.ValidHash(parts[0]) || parts[1] != name) throw new InvalidDataException("Invalid checksum sidecar.");
            var zip = Path.Combine(directory, "package.zip");
            await using (var file = new FileStream(zip, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, useAsync: true))
            {
                await DownloadAsync(new(basis), file, UpdatePackage.MaximumDownloadBytes, progress, cancellationToken);
                file.Position = 0;
                var hash = await SHA256.HashDataAsync(file, cancellationToken);
                if (!Convert.ToHexString(hash).Equals(parts[0], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package checksum mismatch.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => UpdatePackage.Extract(zip, Path.Combine(directory, "payload"), release.Version.ToString(), runtime), cancellationToken);
        }
        catch { Directory.Delete(directory, recursive: true); throw; }
    }

    private async Task DownloadAsync(Uri uri, Stream output, long maximum, IProgress<long>? progress, CancellationToken token)
    {
        for (var redirect = 0; redirect < 4; redirect++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0
                || uri.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                throw new InvalidDataException("Untrusted download redirect.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("ReStart-Updater/1");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            { uri = response.Headers.Location is { } next ? new Uri(uri, next) : throw new InvalidDataException("Missing download redirect."); continue; }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException("Download too large.");
            await using var input = await response.Content.ReadAsStreamAsync(token);
            var buffer = new byte[81920]; long total = 0; int count;
            while ((count = await input.ReadAsync(buffer, token)) > 0)
            {
                if ((total += count) > maximum) throw new InvalidDataException("Download too large.");
                await output.WriteAsync(buffer.AsMemory(0, count), token); progress?.Report(total);
            }
            return;
        }
        throw new InvalidDataException("Too many download redirects.");
    }
}
