# Installs a tablet-edition build for the current user and starts it at logon.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1 -Source <build folder> [-User <name>] [-NoStart]
#
# - copies <build folder> (tools/tablet/build.sh output) to %LOCALAPPDATA%\Programs\ReStartTablet
#   of -User (stopping a running copy first; the user's layout in %LOCALAPPDATA%\ReStart is untouched)
# - registers the logon task "ReStart Tablet": ReStart.exe --background, interactive, non-elevated,
#   normal priority (4: Task Scheduler's default 7 is below normal and every app launched from
#   Home would inherit it), allowed on battery, no time limit, restarted on failure
# - disables the old "TabletHome" task (it is not deleted; re-enable it to go back)
# Stock ReStart in %LOCALAPPDATA%\Programs\ReStart is left alone. Both share one instance
# lease, so whichever starts first wins; do not enable "Start with Windows" in stock ReStart.
# Uninstall: Unregister-ScheduledTask "ReStart Tablet"; stop ReStart; delete the folder.
param(
  [Parameter(Mandatory)][string]$Source,
  [string]$User = $env:USERNAME,
  [switch]$NoStart
)
$ErrorActionPreference = 'Stop'
$taskName = 'ReStart Tablet'
if (-not (Test-Path (Join-Path $Source 'ReStart.exe'))) { throw "No ReStart.exe in $Source" }
$userProfile = (Get-CimInstance Win32_UserProfile | Where-Object { $_.LocalPath -like "*\$User" } | Select-Object -First 1).LocalPath
if (-not $userProfile) { throw "No profile for user $User" }
$target = Join-Path $userProfile 'AppData\Local\Programs\ReStartTablet'
$exe = Join-Path $target 'ReStart.exe'

# Stop a running tablet build (and let its watchdog restore the taskbar) before replacing files.
$running = @(Get-CimInstance Win32_Process -Filter "Name='ReStart.exe'" | Where-Object { $_.ExecutablePath -like "$target\*" })
if ($running.Count) {
  $stop = Join-Path $PSScriptRoot 'stop.ps1'
  if (Test-Path $stop) { & $stop } else { $running | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue } }
}

New-Item -ItemType Directory -Force $target | Out-Null
# Mirror the build: robocopy exit codes below 8 are success.
robocopy $Source $target /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }

$action = New-ScheduledTaskAction -Execute $exe -Argument '--background' -WorkingDirectory $target
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $User
$principal = New-ScheduledTaskPrincipal -UserId $User -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) `
  -Priority 4 -MultipleInstances IgnoreNew -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings `
  -Description 'ReStart tablet edition: Home screen and tablet shell (keyboard detached), normal Start otherwise.' -Force | Out-Null
Write-Output "registered task '$taskName' -> $exe --background"

$old = Get-ScheduledTask -TaskName 'TabletHome' -ErrorAction SilentlyContinue
if ($old -and $old.State -ne 'Disabled') { Disable-ScheduledTask -TaskName 'TabletHome' | Out-Null; Write-Output "disabled task 'TabletHome'" }

if (-not $NoStart) { Start-ScheduledTask -TaskName $taskName; Write-Output 'started' }
