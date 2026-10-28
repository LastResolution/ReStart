# Stops every ReStart build in the console session (graceful first, like TabletHome --hide).
# A tablet-mode shell hides the taskbar; its watchdog (ReStart.exe --tablet-watchdog) restores
# it when the shell dies, so the shells are killed first and the watchdogs get a moment.
$ErrorActionPreference = 'SilentlyContinue'
$all = @(Get-CimInstance Win32_Process -Filter "Name='ReStart.exe'")
$watchdogs = @($all | Where-Object { $_.CommandLine -like '*--tablet-watchdog*' })
$shells = @($all | Where-Object { $_.CommandLine -notlike '*--tablet-watchdog*' })
$exe = @($all | Where-Object { $_.ExecutablePath } | Select-Object -First 1).ExecutablePath
foreach ($p in $shells) { $null = (Get-Process -Id $p.ProcessId).CloseMainWindow() }
Start-Sleep -Milliseconds 300
foreach ($p in $shells) { Stop-Process -Id $p.ProcessId -Force }
Get-Process ReStart.TileHost | Stop-Process -Force
for ($i = 0; $i -lt 20 -and $watchdogs.Count -and (Get-Process -Id $watchdogs.ProcessId); $i++) { Start-Sleep -Milliseconds 150 }
Get-Process ReStart | Stop-Process -Force
for ($i = 0; $i -lt 20 -and (Get-Process ReStart); $i++) { Start-Sleep -Milliseconds 250 }
# Last resort: a marker means the taskbar is still hidden; restore it from the user's session.
$marker = "$env:LOCALAPPDATA\ReStart\tablet-taskbar.json"
if ((Test-Path $marker) -and $exe) {
  & "$PSScriptRoot\userrun.ps1" -Exe $exe -Arguments '--tablet-restore-taskbar'
  for ($i = 0; $i -lt 20 -and (Test-Path $marker); $i++) { Start-Sleep -Milliseconds 250 }
  if (Test-Path $marker) { Write-Output 'stop: taskbar marker still present' } else { Write-Output 'stop: taskbar restored from marker' }
}
