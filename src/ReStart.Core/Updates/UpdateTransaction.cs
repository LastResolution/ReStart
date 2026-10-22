using System.Text;
using System.Text.Json;

namespace ReStart.Core;

/// <summary>Replaces only manifest-owned distribution files, with a durable rollback journal.</summary>
/// <param name="installation">Existing portable installation; user settings and unrelated files are never enumerated or copied.</param>
/// <param name="workspace">Separate staging directory containing the verified payload; must not overlap the installation.</param>
public sealed class UpdateTransaction(string installation, string workspace)
{
    /// <summary>Gets the installation marker used to recover an interrupted update before normal startup.</summary>
    public const string PendingName = ".restart-update.json";
    private string JournalPath => Path.Combine(workspace, "transaction.json");
    private string Backup => Path.Combine(workspace, "backup");
    private sealed record Journal(PackageManifest Previous, PackageManifest Next, string State);

    /// <summary>Backs up verified distribution files and replaces them with the staged package.</summary>
    /// <param name="progress">Optional observer invoked after each replaced/deleted file; throwing aborts and rolls back the transaction.</param>
    /// <exception cref="IOException">A distribution is modified, a new filename collides with a local file, or disk/rollback access fails.</exception>
    /// <exception cref="InvalidOperationException">The workspace overlaps the installation or already has a journal.</exception>
    public void Apply(Action<int>? progress = null)
    {
        ValidateRoots();
        if (File.Exists(JournalPath)) throw new InvalidOperationException("An update journal already exists.");
        var previous = UpdatePackage.ReadManifest(installation);
        var next = UpdatePackage.ReadManifest(Path.Combine(workspace, "payload"));
        if (previous.Runtime != next.Runtime || ReleaseVersion.Parse(next.Version)!.CompareTo(ReleaseVersion.Parse(previous.Version)) <= 0)
            throw new InvalidDataException("Update must be newer and use the installed architecture.");
        UpdatePackage.VerifyFiles(installation, previous);
        UpdatePackage.VerifyFiles(Path.Combine(workspace, "payload"), next);
        var previousNames = previous.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in next.Files)
        {
            var target = UpdatePackage.SafePath(installation, file.Path);
            if (Directory.Exists(target) || !previousNames.Contains(file.Path) && File.Exists(target)) throw new InvalidDataException("Update would overwrite a local file.");
            for (var parent = Path.GetDirectoryName(target); parent != Path.GetFullPath(installation).TrimEnd(Path.DirectorySeparatorChar); parent = Path.GetDirectoryName(parent))
                if (parent == null || File.Exists(parent)) throw new InvalidDataException("Update directory collides with a local file.");
        }
        if (Directory.Exists(Backup)) throw new InvalidDataException("Backup already exists.");
        Directory.CreateDirectory(Backup);
        foreach (var name in previous.Files.Select(f => f.Path).Append(UpdatePackage.ManifestName))
        {
            var target = UpdatePackage.SafePath(Backup, name); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(UpdatePackage.SafePath(installation, name), target, overwrite: false);
        }
        UpdatePackage.VerifyFiles(Backup, previous);
        WriteJournal(new(previous, next, "prepared"));
        AtomicText(Path.Combine(installation, PendingName), Path.GetFileName(Path.GetFullPath(workspace)));
        WriteJournal(new(previous, next, "applying"));
        try
        {
            var count = 0;
            foreach (var file in next.Files)
            {
                var target = UpdatePackage.SafePath(installation, file.Path);
                if (!previousNames.Contains(file.Path) && File.Exists(target)) throw new InvalidDataException("A local file appeared during update.");
                Replace(UpdatePackage.SafePath(Path.Combine(workspace, "payload"), file.Path), target);
                progress?.Invoke(++count);
            }
            var nextNames = next.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in previous.Files.Where(f => !nextNames.Contains(f.Path)))
            { File.Delete(UpdatePackage.SafePath(installation, file.Path)); progress?.Invoke(++count); }
            Replace(UpdatePackage.SafePath(Path.Combine(workspace, "payload"), UpdatePackage.ManifestName), UpdatePackage.SafePath(installation, UpdatePackage.ManifestName));
            UpdatePackage.VerifyFiles(installation, next);
            WriteJournal(new(previous, next, "awaiting-startup"));
        }
        catch { Rollback(); throw; }
    }

    /// <summary>Commits the update only after the replacement application acknowledges successful startup.</summary>
    /// <exception cref="IOException">The journal, installed package or completion marker cannot be verified or written.</exception>
    public void Complete()
    {
        ValidateRoots();
        var journal = ReadJournal();
        if (journal.State != "awaiting-startup") throw new InvalidDataException("Update is not awaiting startup.");
        UpdatePackage.VerifyFiles(installation, journal.Next);
        WriteJournal(journal with { State = "complete" });
        File.Delete(Path.Combine(installation, PendingName));
    }

    /// <summary>Restores verified backups and removes only new distribution files; safe to repeat after interruption.</summary>
    /// <exception cref="IOException">A journal/backup is invalid, or restoring files fails; the pending marker remains for recovery.</exception>
    public void Rollback()
    {
        ValidateRoots();
        var journal = ReadJournal();
        if (journal.State == "complete") { File.Delete(Path.Combine(installation, PendingName)); return; }
        UpdatePackage.VerifyFiles(Backup, journal.Previous);
        var previous = journal.Previous.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in journal.Next.Files.Where(f => !previous.Contains(f.Path)))
        {
            var path = UpdatePackage.SafePath(installation, file.Path);
            // Atomic replacement creates either the exact new file or nothing.
            // Preserve an unrelated file that appeared after preflight.
            if (!File.Exists(path) || new FileInfo(path).Length != file.Size) continue;
            using (var stream = File.OpenRead(path))
                if (!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) continue;
            File.Delete(path);
        }
        foreach (var name in journal.Previous.Files.Select(f => f.Path).Append(UpdatePackage.ManifestName))
        {
            var source = UpdatePackage.SafePath(Backup, name); var target = UpdatePackage.SafePath(installation, name);
            // Do not rewrite an unchanged, locked original when another file caused failure.
            if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(source).Length)
            {
                using var old = File.OpenRead(source); using var current = File.OpenRead(target);
                if (System.Security.Cryptography.SHA256.HashData(old).SequenceEqual(System.Security.Cryptography.SHA256.HashData(current))) continue;
            }
            Replace(source, target);
        }
        UpdatePackage.VerifyFiles(installation, journal.Previous);
        WriteJournal(journal with { State = "rolled-back" });
        File.Delete(Path.Combine(installation, PendingName));
    }

    private Journal ReadJournal()
    {
        UpdatePackage.NoLinks(JournalPath);
        if (new FileInfo(JournalPath).Length > 512 * 1024) throw new InvalidDataException("Update journal too large.");
        Journal journal;
        try { journal = JsonSerializer.Deserialize<Journal>(File.ReadAllBytes(JournalPath), UpdatePackage.Json) ?? throw new InvalidDataException("Missing update journal."); }
        catch (JsonException) { throw new InvalidDataException("Invalid update journal."); }
        // Trust inventories only after independently reading the bounded manifests.
        var previous = UpdatePackage.ReadManifest(Backup);
        var next = UpdatePackage.ReadManifest(Path.Combine(workspace, "payload"));
        if (JsonSerializer.Serialize(previous, UpdatePackage.Json) != JsonSerializer.Serialize(journal.Previous, UpdatePackage.Json)
            || JsonSerializer.Serialize(next, UpdatePackage.Json) != JsonSerializer.Serialize(journal.Next, UpdatePackage.Json)
            || journal.State is not ("prepared" or "applying" or "awaiting-startup" or "complete" or "rolled-back"))
            throw new InvalidDataException("Update journal inventory mismatch.");
        return journal;
    }
    private void ValidateRoots()
    {
        UpdatePackage.NoLinks(installation); UpdatePackage.NoLinks(workspace);
        var first = Path.GetFullPath(installation).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var second = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (first.StartsWith(second, StringComparison.OrdinalIgnoreCase) || second.StartsWith(first, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Update directories overlap.");
    }
    private void WriteJournal(Journal journal) => AtomicText(JournalPath, JsonSerializer.Serialize(journal, UpdatePackage.Json));
    private static void Replace(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = Path.Combine(Path.GetDirectoryName(target)!, ".restart-copy-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var input = File.OpenRead(source)) { input.CopyTo(output); output.Flush(flushToDisk: true); }
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void AtomicText(string target, string text)
    {
        UpdatePackage.NoLinks(target);
        var temporary = target + ".new";
        UpdatePackage.NoLinks(temporary);
        using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { output.Write(Encoding.UTF8.GetBytes(text)); output.Flush(flushToDisk: true); }
        File.Move(temporary, target, overwrite: true);
    }
}
