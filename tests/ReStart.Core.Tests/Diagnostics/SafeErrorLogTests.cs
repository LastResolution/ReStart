using System.Text;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class SafeErrorLogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ReStart-log-tests-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void NeverWritesExceptionMessagesTypesOrInnerExceptionData()
    {
        SafeErrorLog.Write(_root, new InvalidOperationException("private-sentinel-token", new Exception("private-sentinel-path")));
        var text = File.ReadAllText(Path.Combine(_root, "error.log"));
        Assert.Contains(" state ", text); Assert.DoesNotContain("private-sentinel", text);
        Assert.DoesNotContain("InvalidOperationException", text);
    }
    [Fact]
    public void RemovesLegacyRawLogsWithoutCopyingTheirContents()
    {
        Directory.CreateDirectory(_root);
        foreach (var name in new[] { "error.log", "error.log.1", "error.log.2" }) File.WriteAllText(Path.Combine(_root, name), "private-sentinel");
        File.WriteAllText(Path.Combine(_root, "unrelated.txt"), "retained");
        SafeErrorLog.Prepare(_root);
        Assert.Single(Directory.GetFiles(_root));
        Assert.Equal("retained", File.ReadAllText(Path.Combine(_root, "unrelated.txt")));
    }
    [Fact]
    public void RotationRetainsAtMostThreeBoundedFilesAndSurvivesAccessFailure()
    {
        SafeErrorLog.Write(_root, new IOException("private-sentinel"));
        var path = Path.Combine(_root, "error.log");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            SafeErrorLog.Write(_root, new Exception("not shared"));
        for (var i = 0; i < 5; i++)
        {
            var header = "ReStart diagnostic log v1\n";
            File.WriteAllText(path, header + new string(' ', SafeErrorLog.MaximumBytes - Encoding.UTF8.GetByteCount(header)), new UTF8Encoding(false));
            SafeErrorLog.Write(_root, new IOException("private-sentinel"));
        }
        Assert.Equal(3, Directory.GetFiles(_root).Length);
        Assert.All(Directory.GetFiles(_root), file => Assert.InRange(new FileInfo(file).Length, 1, SafeErrorLog.MaximumBytes));
        Assert.Contains(" io ", File.ReadAllText(path));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
