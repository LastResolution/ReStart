using ReStart.App.Services;

namespace ReStart.Core.Tests;

public class WindowsSearchHandoffTests
{
    private sealed class Backend : IWindowsSearchHandoffBackend
    {
        public bool Opens = true;
        public bool ModifiersHeld { get; set; }
        public int OpensCalled;
        public int Dismissals;
        public string? Query;
        public CancellationToken Cancellation;
        public Func<Task<bool>>? Transfer;
        public TaskCompletionSource<bool>? DismissCompletion;
        public bool Open() { OpensCalled++; return Opens; }
        public Task<bool> TransferAsync(string query, CancellationToken cancellation)
        { Query = query; Cancellation = cancellation; return Transfer?.Invoke() ?? Task.FromResult(true); }
        public Task<bool> DismissForegroundAsync() { Dismissals++; return DismissCompletion?.Task ?? Task.FromResult(true); }
    }

    [Fact]
    public async Task BareWindowsKeyCanReturnFromAnUnrequestedNativeSearch()
    {
        var backend = new Backend(); using var handoff = new WindowsSearchHandoff(backend);
        await handoff.ReturnAsync(); Assert.Equal(0, backend.Dismissals);
        await handoff.ReturnAsync(explicitInput: true);
        Assert.Equal(1, backend.Dismissals); Assert.Equal(0, backend.OpensCalled); Assert.Null(backend.Query);
        Assert.False(handoff.HasHandoff);
    }

    [Fact]
    public async Task BareWindowsKeyReturnStillWaitsForModifiersAndCanBeSuperseded()
    {
        var backend = new Backend { ModifiersHeld = true }; using var handoff = new WindowsSearchHandoff(backend);
        var returning = handoff.ReturnAsync(explicitInput: true);
        Assert.False(returning.IsCompleted); Assert.Equal(0, backend.Dismissals);
        await handoff.OpenAsync("later explicit search"); backend.ModifiersHeld = false;
        await returning; Assert.Equal(0, backend.Dismissals); Assert.True(handoff.HasHandoff);
    }

    [Fact]
    public async Task AReturnFromAnUnrequestedSurfaceWaitsForItsDismissalBeforeCompleting()
    {
        var backend = new Backend { DismissCompletion = new() }; using var handoff = new WindowsSearchHandoff(backend);
        var returning = handoff.ReturnAsync(explicitInput: true);
        Assert.False(returning.IsCompleted); Assert.Equal(1, backend.Dismissals);
        backend.DismissCompletion.SetResult(true); await returning;
        Assert.Equal(0, backend.OpensCalled); Assert.Null(backend.Query);
    }

    [Fact]
    public async Task DisposingDuringAWindowsKeyModifierWaitCannotSendEscapeLater()
    {
        var backend = new Backend { ModifiersHeld = true }; var handoff = new WindowsSearchHandoff(backend);
        var returning = handoff.ReturnAsync(explicitInput: true);
        handoff.Dispose(); backend.ModifiersHeld = false;
        await returning; await handoff.ReturnAsync(explicitInput: true);
        Assert.Equal(0, backend.Dismissals);
    }

    [Theory]
    [InlineData("Microsoft.Windows.Search_cw5n1h2txyewy", "SearchApp.exe")]
    [InlineData("Microsoft.Windows.Cortana_cw5n1h2txyewy", "SearchUI.exe")]
    [InlineData("MicrosoftWindows.Client.CBS_cw5n1h2txyewy", "SearchHost.exe")]
    [InlineData("Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy", "StartMenuExperienceHost.exe")]
    public void OnlyProtectedStartAndSearchSurfacesQualifyForWindowsKeyDismissal(string package, string executable)
    {
        if (!OperatingSystem.IsWindows()) return;
        const string windows = @"X:\Windows";
        var path = Path.Combine(windows, "SystemApps", package, executable);
        foreach (var className in new[] { "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow" })
        {
            Assert.True(WindowsSearchLauncher.IsShellSurface(className, path, windows, true));
            Assert.False(WindowsSearchLauncher.IsShellSurface(className, path + ".exe", windows, true));
            Assert.False(WindowsSearchLauncher.IsShellSurface(className, path.Replace("SystemApps", "SystemAppsBackup"), windows, true));
            Assert.False(WindowsSearchLauncher.IsShellSurface(className, path.Replace("SystemApps", @"SystemApps\..\SystemApps"), windows, true));
            Assert.False(WindowsSearchLauncher.IsShellSurface(className, @"X:\Other\" + executable, windows, true));
        }
        Assert.False(WindowsSearchLauncher.IsShellSurface("Owned other application", path, windows, true));
        Assert.False(WindowsSearchLauncher.IsShellSurface("Windows.UI.Core.CoreWindow", null, windows, true));
        Assert.False(WindowsSearchLauncher.IsShellSurface("Windows.UI.Core.CoreWindow", path, "", true));
        Assert.Equal(executable != "StartMenuExperienceHost.exe", WindowsSearchLauncher.IsShellSurface("Windows.UI.Core.CoreWindow", path, windows, false));
        Assert.False(WindowsSearchLauncher.IsShellSurface("XamlExplorerHostIslandWindow", path, windows, false));
    }

    [Theory]
    [InlineData("  notepad  ", "notepad")]
    [InlineData("日本語 ☁️ café 😀 & ? # %", "日本語 ☁️ café 😀 & ? # %")]
    [InlineData("cmd /c echo fixture; $(fixture)", "cmd /c echo fixture; $(fixture)")]
    public async Task PassesCurrentQueryAsLiteralText(string query, string expected)
    {
        var backend = new Backend(); using var handoff = new WindowsSearchHandoff(backend);
        Assert.Equal(WindowsSearchHandoffResult.Transferred, await handoff.OpenAsync(query));
        Assert.Equal(expected, backend.Query); Assert.Equal(1, backend.OpensCalled); Assert.True(handoff.HasHandoff);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\towned")]
    [InlineData("owned\r\n")]
    [InlineData("owned\0")]
    public async Task RejectedQueriesCannotOpenOrType(string? query)
    {
        var backend = new Backend(); using var handoff = new WindowsSearchHandoff(backend);
        Assert.Equal(WindowsSearchHandoffResult.Failed, await handoff.OpenAsync(query));
        Assert.Null(backend.Query); Assert.Equal(0, backend.OpensCalled); Assert.False(handoff.HasHandoff);
    }

    [Fact]
    public void RejectsMalformedUnicodeAndBoundsTextWithoutTruncation()
    {
        Assert.Null(WindowsSearchHandoff.NormalizeQuery("bad\uD800"));
        Assert.Null(WindowsSearchHandoff.NormalizeQuery("bad\uDC00"));
        Assert.Null(WindowsSearchHandoff.NormalizeQuery(new string('a', 2049)));
        Assert.Equal(2048, WindowsSearchHandoff.NormalizeQuery(new string('a', 2048))!.Length);
    }

    [Fact]
    public async Task FailedOpenDoesNotTransferOrDismiss()
    {
        var backend = new Backend { Opens = false }; using var handoff = new WindowsSearchHandoff(backend);
        Assert.Equal(WindowsSearchHandoffResult.Failed, await handoff.OpenAsync("owned"));
        await handoff.ReturnAsync(); Assert.Null(backend.Query); Assert.Equal(0, backend.Dismissals);
    }

    [Fact]
    public async Task UnavailableProviderStillTracksOpenedSurfaceForReturn()
    {
        var backend = new Backend { Transfer = () => Task.FromResult(false) }; using var handoff = new WindowsSearchHandoff(backend);
        Assert.Equal(WindowsSearchHandoffResult.OpenedWithoutQuery, await handoff.OpenAsync("owned"));
        Assert.True(handoff.HasHandoff); await handoff.ReturnAsync(); Assert.Equal(1, backend.Dismissals);
        await handoff.ReturnAsync(); Assert.Equal(1, backend.Dismissals);
    }

    [Fact]
    public async Task ReturnCancelsLateTextAndWaitsForNativeDismissal()
    {
        var transfer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new Backend { Transfer = () => transfer.Task, DismissCompletion = new() };
        using var handoff = new WindowsSearchHandoff(backend);
        var opening = handoff.OpenAsync("owned"); var returning = handoff.ReturnAsync();
        Assert.True(backend.Cancellation.IsCancellationRequested); Assert.False(returning.IsCompleted);
        Assert.Equal(WindowsSearchHandoffResult.Cancelled, await opening); Assert.False(handoff.HasHandoff);
        backend.DismissCompletion.SetResult(true); await returning; transfer.SetResult(true);
        Assert.Equal(1, backend.Dismissals);
    }

    [Fact]
    public async Task NewHandoffSupersedesOldTransferAndPendingModifierWait()
    {
        var transfer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new Backend { Transfer = () => transfer.Task, ModifiersHeld = true };
        using var handoff = new WindowsSearchHandoff(backend);
        var first = handoff.OpenAsync("first"); var returning = handoff.ReturnAsync();
        backend.Transfer = null;
        Assert.Equal(WindowsSearchHandoffResult.Transferred, await handoff.OpenAsync("second"));
        backend.ModifiersHeld = false; await returning;
        Assert.Equal(WindowsSearchHandoffResult.Cancelled, await first); Assert.Equal("second", backend.Query);
        Assert.Equal(0, backend.Dismissals); Assert.True(handoff.HasHandoff); transfer.SetResult(true);
    }

    [Fact]
    public async Task ReturnWaitsForPhysicalModifiersBeforeEscape()
    {
        var backend = new Backend { ModifiersHeld = true }; using var handoff = new WindowsSearchHandoff(backend);
        await handoff.OpenAsync("owned"); var returning = handoff.ReturnAsync();
        Assert.Equal(0, backend.Dismissals); backend.ModifiersHeld = false;
        await returning; Assert.Equal(1, backend.Dismissals);
    }

    [Fact]
    public async Task DisposeCancelsWithoutClosingAnExternalSurface()
    {
        var transfer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new Backend { Transfer = () => transfer.Task }; var handoff = new WindowsSearchHandoff(backend);
        var opening = handoff.OpenAsync("owned"); handoff.Dispose();
        Assert.True(backend.Cancellation.IsCancellationRequested);
        Assert.Equal(WindowsSearchHandoffResult.Cancelled, await opening);
        Assert.Equal(WindowsSearchHandoffResult.Failed, await handoff.OpenAsync("later"));
        await handoff.ReturnAsync(); Assert.Equal(0, backend.Dismissals); transfer.SetResult(true);
    }

    [Fact]
    public void RecognizesOnlyExactProtectedSearchExecutables()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string windows = @"X:\Windows";
        var valid = Path.Combine(windows, "SystemApps", "MicrosoftWindows.Client.CBS_cw5n1h2txyewy", "SearchHost.exe");
        Assert.True(WindowsSearchLauncher.IsSearchExecutable(valid, windows));
        Assert.True(WindowsSearchLauncher.IsSearchExecutable(valid.ToUpperInvariant(), windows));
        foreach (var path in new[] { @"X:\Other\SearchHost.exe", valid + ".exe", valid.Replace("SystemApps", "SystemAppsBackup"),
            valid.Replace("SearchHost.exe", "Other.exe"), valid.Replace("SystemApps", @"SystemApps\..\SystemApps") })
            Assert.False(WindowsSearchLauncher.IsSearchExecutable(path, windows));
        Assert.False(WindowsSearchLauncher.IsSearchExecutable(null, windows));
    }

    [Fact]
    public void DismissUsesOnlyBalancedEscapeWithoutWindowOrProcessTermination()
    {
        if (!OperatingSystem.IsWindows()) return;
        var inputs = WindowsSearchLauncher.CreateDismissInputs();
        Assert.Equal(new ushort[] { 0x1B, 0x1B }, inputs.Select(i => i.Union.Keyboard.Vk));
        Assert.Equal(new uint[] { 0, 2 }, inputs.Select(i => i.Union.Keyboard.Flags));
    }
}
