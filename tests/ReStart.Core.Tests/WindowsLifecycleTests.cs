using Microsoft.Win32;
using System.Runtime.Versioning;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsLifecycleTests
{
    [Fact]
    public void DuplicateLaunchSignalsExistingAndCanRestartAfterExit()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = "tests-" + Guid.NewGuid().ToString("N");
        using (var first = new SingleInstanceGate(name))
        {
            Assert.True(first.IsPrimary);
            using var second = new SingleInstanceGate(name);
            Assert.False(second.IsPrimary);
            second.NotifyExisting();
            Assert.True(first.Activation.WaitOne(2000));
            Assert.False(first.Activation.WaitOne(0));
            using var preview = new SingleInstanceGate(name, true);
            Assert.True(preview.IsPrimary);
        }
        using var restarted = new SingleInstanceGate(name);
        Assert.True(restarted.IsPrimary);
    }
    [Fact]
    public void ConcurrentLaunchesHaveOnlyOnePrimary()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = "tests-" + Guid.NewGuid().ToString("N");
        var gates = new SingleInstanceGate[20];
        try
        {
            Parallel.For(0, gates.Length, i => gates[i] = new(name));
            Assert.Single(gates, g => g.IsPrimary);
        }
        finally { foreach (var gate in gates) gate?.Dispose(); }
    }
    [Fact]
    public void StartupRegistrationQuotesPathDisablesAndRollsBack()
    {
        if (!OperatingSystem.IsWindows()) return;
        var subKey = @"Software\ReStart.Tests\" + Guid.NewGuid().ToString("N");
        var registration = new StartupRegistration(subKey);
        try
        {
            Assert.False(registration.IsEnabled);
            registration.SetEnabled(true, @"C:\App Folder\ReStart.exe");
            Assert.Equal("\"C:\\App Folder\\ReStart.exe\" --background", registration.Command);
            var snapshot = registration.Capture();
            registration.SetEnabled(true, @"C:\New Folder\ReStart.exe");
            Assert.Contains("New Folder", registration.Command);
            registration.Restore(snapshot);
            Assert.Equal(snapshot.Value, registration.Command);
            registration.SetEnabled(false);
            Assert.False(registration.IsEnabled);
            Assert.Throws<InvalidOperationException>(() => registration.SetEnabled(true, "relative.exe"));
            Assert.Throws<InvalidOperationException>(() => registration.SetEnabled(true, @"C:\dotnet.exe"));
            Assert.Throws<InvalidOperationException>(() => registration.SetEnabled(true, @"C:\" + new string('a', 260) + ".exe"));
            Assert.False(registration.IsEnabled);
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(subKey, false); }
    }
}
