using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class App
{
    private async Task<bool> HandleUpdateEntryAsync(string[] args)
    {
        static string? Value(string[] values, string key)
        { var index = Array.IndexOf(values, key); return index >= 0 && index + 1 < values.Length ? values[index + 1] : null; }
        var fixtureRoot = Value(args, "--verify-update-root");
        if (fixtureRoot != null) PortableUpdates.CacheRoot = Path.GetFullPath(fixtureRoot);
        try
        {
            if (Value(args, "--apply-update") is { } id)
            {
                var result = await PortableUpdates.RunAsync(id);
                if (result == 3 && fixtureRoot == null) ShowUpdateRepair();
                Shutdown(result); return true;
            }
            if (Value(args, "--verify-update-installations") is { } directory)
            { await VerifyUpdateInstallationsAsync(Path.GetFullPath(directory)); Shutdown(0); return true; }
            if (fixtureRoot != null && Value(args, "--verify-update-parent") is { } parentId)
            {
                var request = PortableUpdates.ReadRequest(parentId);
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                request = request with { ParentId = process.Id, ParentStart = process.StartTime.ToUniversalTime().Ticks };
                File.WriteAllText(Path.Combine(PortableUpdates.Workspace(parentId), "request.json"), System.Text.Json.JsonSerializer.Serialize(request));
                var ready = await PortableUpdates.StartAsync(new(parentId, PortableUpdates.Workspace(parentId), request.Version));
                Shutdown(ready ? 0 : 1); return true;
            }
            if (fixtureRoot != null && Value(args, "--complete-update") is { } completeFixture)
            {
                var request = PortableUpdates.ReadRequest(completeFixture);
                if (File.Exists(Path.Combine(PortableUpdates.Workspace(completeFixture), "simulate-startup-failure"))) { Shutdown(1); return true; }
                PortableUpdates.Acknowledge(completeFixture, request.Installation);
                File.WriteAllText(Path.Combine(PortableUpdates.Workspace(completeFixture), "fixture-started"), "ready");
                Shutdown(0); return true;
            }
            if (fixtureRoot != null && Value(args, "--update-result") is { } restoredFixture)
            { File.WriteAllText(Path.Combine(PortableUpdates.Workspace(restoredFixture), "fixture-restored"), "ready"); Shutdown(0); return true; }
            if (!args.Contains("--preview") && !args.Contains("--smoke-test") && !args.Any(a => a.StartsWith("--render-", StringComparison.Ordinal) || a == "--verify-platform")
                && !args.Contains("--complete-update") && PortableUpdates.RedirectInterruptedStartup(AppContext.BaseDirectory))
            { Shutdown(0); return true; }
            if (Value(args, "--complete-update") is { } complete)
            {
                if (File.ReadAllText(Path.Combine(AppContext.BaseDirectory, UpdateTransaction.PendingName)) != complete)
                    throw new InvalidDataException("Update startup marker mismatch.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            if (Value(args, "--verify-update-installations") is { } output)
                File.WriteAllText(Path.Combine(output, "verification.json"), System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message }));
            else if (fixtureRoot == null) ShowUpdateRepair();
            Shutdown(3); return true;
        }
        return false;
    }

    private async Task<bool> FinishUpdatedStartupAsync(string[] args, MainWindow window)
    {
        var id = args[Array.IndexOf(args, "--complete-update") + 1];
        try
        {
            await window.InitializeAsync();
            if (!window.InitializedSuccessfully) throw new InvalidOperationException("Updated application initialization failed.");
            PortableUpdates.Acknowledge(id, AppContext.BaseDirectory);
            for (var i = 0; i < 300 && File.Exists(Path.Combine(AppContext.BaseDirectory, UpdateTransaction.PendingName)); i++) await Task.Delay(100);
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, UpdateTransaction.PendingName))) throw new IOException("Update was not committed.");
            window.DeferLayoutSaves = false;
            window.SaveLayout();
            window.SetStatus(L.T(K.UpdateInstalled));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { window.Exit(); Shutdown(1); return false; }
    }

    private static void ShowUpdateRepair()
    {
        PortableUpdates.Record(DiagnosticCode.UpdateRecoveryFailed);
        var window = new Window { Title = L.T(K.SettingsUpdates), Width = 560, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        window.Style = (Style)window.FindResource(typeof(Window)); ThemeWindow.Attach(window);
        var panel = new StackPanel { Margin = new(24) };
        panel.Children.Add(new TextBlock { Text = L.T(K.UpdateRepairHelp), TextWrapping = TextWrapping.Wrap });
        var close = new Button { Content = L.T(K.CommonClose), IsCancel = true, Margin = new(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => window.Close(); panel.Children.Add(close); window.Content = panel; window.ShowDialog();
    }
}
