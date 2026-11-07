using System.Text.Json;
using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class WindowsKeyTraceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "restart-key-trace-" + Guid.NewGuid().ToString("N"));
    private string Output => Path.Combine(_directory, "report.jsonl");
    public WindowsKeyTraceTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void ArbitraryKeysAndInvalidEnumValuesCannotReachTheReport()
    {
        using (var trace = new WindowsKeyTrace(Output))
        {
            for (uint key = 0; key < 256; key++) if (key is not (0x5B or 0x5C)) trace.Key(key, true, false, false, 0, 100);
            trace.Record((KeyTraceStage)9999, KeyTraceDetail.None);
            trace.Record(KeyTraceStage.Ready, (KeyTraceDetail)9999);
        }
        Assert.Equal(new[] { "Started", "Completed" }, Stages());
    }

    [Fact]
    public void ExportHasOnlyClosedVocabularyAndRelativeBoundedNumbers()
    {
        using (var trace = new WindowsKeyTrace(Output))
        {
            trace.Key(0x5B, true, false, true, uint.MaxValue - 5, 4);
            trace.Key(0x5C, false, true, false, 10, 9);
            trace.Record(KeyTraceStage.ToggleDispatched, KeyTraceDetail.RegisteredHotkey, int.MaxValue);
        }
        var lines = File.ReadAllLines(Output);
        Assert.Equal(new[] { "schema", "durationMs", "maximumEvents" }, JsonDocument.Parse(lines[0]).RootElement.EnumerateObject().Select(p => p.Name));
        foreach (var line in lines.Skip(1))
        {
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            Assert.Equal(new[] { "ms", "stage", "detail", "delayMs" }, row.EnumerateObject().Select(p => p.Name));
            Assert.True(Enum.TryParse<KeyTraceStage>(row.GetProperty("stage").GetString(), out var stage) && Enum.IsDefined(stage));
            Assert.True(Enum.TryParse<KeyTraceDetail>(row.GetProperty("detail").GetString(), out var detail) && Enum.IsDefined(detail));
            Assert.InRange(row.GetProperty("ms").GetInt32(), 0, WindowsKeyTrace.DurationMs);
            Assert.InRange(row.GetProperty("delayMs").GetInt32(), 0, WindowsKeyTrace.DurationMs);
        }
        using var first = JsonDocument.Parse(lines[2]);
        Assert.Equal("LeftWithModifier", first.RootElement.GetProperty("detail").GetString());
        Assert.Equal(10, first.RootElement.GetProperty("delayMs").GetInt32());
        using var second = JsonDocument.Parse(lines[3]);
        Assert.Equal(0, second.RootElement.GetProperty("delayMs").GetInt32());
    }

    [Fact]
    public void ConcurrentProducersHaveAFixedExportBound()
    {
        using (var trace = new WindowsKeyTrace(Output, maximumEvents: 32))
            Parallel.For(0, 5000, _ => trace.Record(KeyTraceStage.ToggleQueued, KeyTraceDetail.WindowsKey));
        var stages = Stages();
        Assert.Equal(34, stages.Length); // 32 data entries plus two fixed footer entries.
        Assert.Equal(1, stages.Count(s => s == "LimitReached"));
        Assert.Equal("Completed", stages[^1]);
    }

    [Fact]
    public async Task AutomaticallyStopsAndFlushesWithoutUiOrExplicitDisposal()
    {
        using var trace = new WindowsKeyTrace(Output, durationMs: 30);
        await Task.Delay(450);
        Assert.False(trace.Active);
        var before = File.ReadAllText(Output);
        Assert.Contains("Completed", before);
        trace.Record(KeyTraceStage.Ready);
        Assert.Equal(before, File.ReadAllText(Output));
        Assert.False(trace.WriteFailed);
    }

    [Fact]
    public async Task CheckpointsAreDurableWhileTheSessionStillRuns()
    {
        using var trace = new WindowsKeyTrace(Output);
        trace.Record(KeyTraceStage.ForegroundAttachBegin);
        await Task.Delay(450);
        Assert.True(trace.Active);
        using var reader = new StreamReader(new FileStream(Output, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        Assert.Contains("ForegroundAttachBegin", reader.ReadToEnd());
    }

    [Fact]
    public void ExistingFilesAreNeverOverwritten()
    {
        File.WriteAllText(Output, "unchanged");
        Assert.Throws<IOException>(() => new WindowsKeyTrace(Output));
        Assert.Equal("unchanged", File.ReadAllText(Output));
    }

    [Fact]
    public void RepeatedDisposalIsSafeAndLateEventsAreIgnored()
    {
        var trace = new WindowsKeyTrace(Output);
        trace.Dispose();
        var before = File.ReadAllText(Output);
        trace.Dispose();
        trace.Key(0x5B, true, false, false, 0, 100);
        trace.Record(KeyTraceStage.Ready);
        Assert.Equal(before, File.ReadAllText(Output));
        Assert.Equal(1, Stages().Count(stage => stage == "Completed"));
    }

    [Fact]
    public void ObservingKeysDoesNotChangeWindowsKeyDecisions()
    {
        var control = new WindowsKeyState();
        var observed = new WindowsKeyState();
        var keys = new[] { (0x5B, true), (0x5B, true), (0x5B, false), (0x5B, true), (0x45, true), (0x45, false), (0x5B, false), (0x5C, true), (0x5C, false) };
        using (var trace = new WindowsKeyTrace(Output))
            foreach (var (key, down) in keys)
            {
                trace.Key((uint)key, down, false, false, 0, 10);
                Assert.Equal(control.Process(key, down), observed.Process(key, down));
                Assert.Equal(control.HeldKey, observed.HeldKey);
            }
        Assert.Equal(keys.Count(item => item.Item1 is 0x5B or 0x5C) + 2, Stages().Length);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(90001, 10)]
    [InlineData(100, 0)]
    [InlineData(100, 4097)]
    public void SessionBoundsCannotBeExpanded(int duration, int events)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new WindowsKeyTrace(Output, duration, events));

    private string[] Stages() => File.ReadAllLines(Output).Skip(1).Select(line => JsonDocument.Parse(line).RootElement.GetProperty("stage").GetString()!).ToArray();
    public void Dispose() => Directory.Delete(_directory, true);
}
