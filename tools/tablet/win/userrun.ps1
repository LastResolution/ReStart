# Starts a program in the console session as the SSH user (non-elevated) through a one-off
# interactive scheduled task, because SSH sessions run in session 0 with no desktop.
param([Parameter(Mandatory)][string]$Exe, [string]$Arguments = '', [string]$WorkDir = '')
$name = 'Claude-UserRun-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$a = if ($Arguments) { New-ScheduledTaskAction -Execute $Exe -Argument $Arguments } else { New-ScheduledTaskAction -Execute $Exe }
if ($WorkDir) { $a.WorkingDirectory = $WorkDir }
$p = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited
$s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -Priority 4
Register-ScheduledTask -TaskName $name -Action $a -Principal $p -Settings $s -Force | Out-Null
Start-ScheduledTask -TaskName $name
Start-Sleep -Milliseconds 1500
Unregister-ScheduledTask -TaskName $name -Confirm:$false
