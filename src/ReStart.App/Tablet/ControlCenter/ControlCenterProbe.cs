using ReStart.App.Tablet.Media;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>
/// Remote verification for the laptop test loop: <c>control-center-state</c> logs every value the
/// modules show, <c>control-center-test &lt;control&gt;</c> changes one control through the same code
/// the module uses, reads it back, restores the original value and logs the round trip.
/// </summary>
internal static class ControlCenterProbe
{
    internal static async Task LogStateAsync()
    {
        var radios = new SystemRadios();
        await radios.StartAsync();
        using var audio = new SystemAudio();
        audio.Attach();
        var rotation = RotationLock.Read();
        var battery = SystemActions.Battery();
        TabletLog.Write("cc-state", $"volume={audio.Volume:0.00} muted={audio.Muted} brightness={await Brightness.ReadAsync()} refresh={RefreshRate.Read()} display={RefreshRate.InternalDisplay()}");
        TabletLog.Write("cc-state", $"rotation={rotation} dark={SystemTheme.IsDark} wifi={radios.WifiOn} network={SystemRadios.WifiNetwork() ?? "-"} bluetooth={radios.BluetoothOn} airplane={SystemRadios.AirplaneMode}");
        TabletLog.Write("cc-state", $"battery={battery} ghelper={GHelper.IsAvailable} mode={GHelper.PerformanceMode} displayState={SystemActions.DisplayState} tablet={TabletShell.IsTablet}");
        TabletLog.Write("cc-state", $"media={MediaSessions.Shared.Current?.Title ?? "-"} / {MediaSessions.Shared.Current?.Artist ?? "-"} playing={MediaSessions.Shared.Current?.IsPlaying}");
    }

    internal static async Task RunAsync(string control)
    {
        void Log(string message) => TabletLog.Write("cc-test", $"{control}: {message}");
        try
        {
            switch (control)
            {
                case "volume":
                {
                    using var audio = new SystemAudio(); audio.Attach();
                    var before = audio.Volume; var target = before > 0.5 ? 0.2f : 0.6f;
                    audio.SetVolume(target); var read = audio.Volume;
                    audio.SetVolume(before);
                    Log($"before={before:0.00} set={target:0.00} read={read:0.00} restored={audio.Volume:0.00} {(Math.Abs(read - target) < 0.01 ? "PASS" : "FAIL")}");
                    break;
                }
                case "mute":
                {
                    using var audio = new SystemAudio(); audio.Attach();
                    var before = audio.Muted; audio.SetMuted(!before); var read = audio.Muted; audio.SetMuted(before);
                    Log($"before={before} read={read} restored={audio.Muted} {(read != before ? "PASS" : "FAIL")}");
                    break;
                }
                case "brightness-reads":
                {
                    var reads = new List<int>();
                    for (var i = 0; i < 5; i++) reads.Add(await Brightness.ReadAsync());
                    Log($"{string.Join(",", reads)} {(reads.All(r => r >= 0) ? "PASS" : "FAIL")}");
                    break;
                }
                case "brightness":
                {
                    var before = await Brightness.ReadAsync(); var target = before > 50 ? 40 : 70;
                    if (before < 0) { Log("cannot read, not touching it"); break; }
                    Brightness.Set(target); await Task.Delay(800); var read = await Brightness.ReadAsync();
                    Brightness.Set(before); await Task.Delay(800);
                    Log($"before={before} set={target} read={read} restored={await Brightness.ReadAsync()} {(read == target ? "PASS" : "FAIL")}");
                    break;
                }
                case "refresh":
                {
                    var before = RefreshRate.Read(); var target = before.Current > 60 ? 60 : before.Max;
                    var viaGHelper = GHelper.IsAvailable && GHelper.ToggleRefreshRate();
                    if (!viaGHelper) RefreshRate.Set(target);
                    await Task.Delay(2500); var read = RefreshRate.Read();
                    if (read.Current != before.Current) { if (viaGHelper) GHelper.ToggleRefreshRate(); else RefreshRate.Set(before.Current); }
                    await Task.Delay(2500);
                    Log($"via={(viaGHelper ? "g-helper" : "direct")} before={before.Current} target={target} read={read.Current} restored={RefreshRate.Read().Current} {(read.Current == target ? "PASS" : "FAIL")}");
                    break;
                }
                case "refresh-direct":
                {
                    var before = RefreshRate.Read(); var target = before.Current > 60 ? 60 : before.Max;
                    var ok = RefreshRate.Set(target); await Task.Delay(1500); var read = RefreshRate.Read();
                    RefreshRate.Set(before.Current); await Task.Delay(1500);
                    Log($"ok={ok} before={before.Current} target={target} read={read.Current} restored={RefreshRate.Read().Current} {(read.Current == target ? "PASS" : "FAIL")}");
                    break;
                }
                case "rotation":
                {
                    var before = RotationLock.Read();
                    var ok = RotationLock.SetLocked(!before.Locked); await Task.Delay(300); var read = RotationLock.Read();
                    RotationLock.SetLocked(before.Locked); await Task.Delay(300);
                    Log($"before={before} ok={ok} read={read} restored={RotationLock.Read()} {(read.Locked != before.Locked ? "PASS" : "FAIL")}");
                    break;
                }
                case "theme":
                {
                    var before = SystemTheme.IsDark;
                    SystemTheme.SetDark(!before); await Task.Delay(2000); var read = SystemTheme.IsDark;
                    SystemTheme.SetDark(before); await Task.Delay(500);
                    Log($"before={before} read={read} restored={SystemTheme.IsDark} {(read != before ? "PASS" : "FAIL")}");
                    break;
                }
                case "wifi" or "bluetooth":
                {
                    var radios = new SystemRadios(); await radios.StartAsync();
                    bool? State() => control == "wifi" ? radios.WifiOn : radios.BluetoothOn;
                    Task<bool> Set(bool on) => control == "wifi" ? radios.SetWifiAsync(on) : radios.SetBluetoothAsync(on);
                    if (State() is not { } before) { Log("no radio"); break; }
                    var ok = await Set(!before); await Task.Delay(3000); var read = State();
                    var restored = await Set(before); await Task.Delay(1500);
                    Log($"before={before} ok={ok} read={read} restoredOk={restored} now={State()} {(read == !before ? "PASS" : "FAIL")}");
                    break;
                }
                case "airplane":
                {
                    if (SystemRadios.AirplaneMode is not { } before) { Log("radio management unavailable"); break; }
                    var ok = SystemRadios.SetAirplaneMode(!before); await Task.Delay(4000); var read = SystemRadios.AirplaneMode;
                    var restored = SystemRadios.SetAirplaneMode(before); await Task.Delay(2000);
                    Log($"before={before} ok={ok} read={read} restoredOk={restored} now={SystemRadios.AirplaneMode} {(read == !before ? "PASS" : "FAIL")}");
                    break;
                }
                case "performance":
                {
                    if (GHelper.PerformanceMode is not { } before) { Log("no g-helper config"); break; }
                    var target = before == GHelper.Silent ? GHelper.Balanced : GHelper.Silent;
                    var ok = GHelper.SetPerformanceMode(target); await Task.Delay(3500); var read = GHelper.PerformanceMode;
                    GHelper.SetPerformanceMode(before); await Task.Delay(3500);
                    Log($"before={before} set={target} ok={ok} read={read} restored={GHelper.PerformanceMode} {(read == target ? "PASS" : "FAIL")}");
                    break;
                }
                case "screen-off":
                {
                    SystemActions.ScreenOff(); await Task.Delay(3000); var off = SystemActions.DisplayState;
                    SystemActions.Wake(); await Task.Delay(2000);
                    Log($"after off={off} after wake={SystemActions.DisplayState} {(off == 0 && SystemActions.DisplayState == 1 ? "PASS" : "FAIL")}");
                    break;
                }
                case "keyboard":
                    await SystemActions.ToggleTouchKeyboardAsync(); Log("toggled");
                    break;
                case "ghelper-window":
                    Log("toggle " + GHelper.ToggleWindow());
                    break;
                case "selfshot":
                {
                    ControlCenterFeature.Show(); await Task.Delay(1200);
                    ControlCenterFeature.Window?.DebugSnapshot(Path.GetDirectoryName(TabletLog.FilePath)!);
                    await Task.Delay(300); TabletShell.CloseOverlays(); Log("saved");
                    break;
                }
                default: Log("unknown control"); break;
            }
        }
        catch (Exception ex) { Log("error " + ex); }
    }
}
