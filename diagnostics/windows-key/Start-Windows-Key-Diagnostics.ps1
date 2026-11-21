$ErrorActionPreference = 'Stop'
function Read-SharedReport([string]$Path) {
    $reader = [IO.StreamReader]::new([IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite))
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}
try {
    $executable = Join-Path $PSScriptRoot 'ReStart.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Extract the entire diagnostic ZIP before running this script.' }
    $directory = Join-Path $PSScriptRoot 'Windows-key-reports'
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $report = Join-Path $directory ('windows-key-' + [Guid]::NewGuid().ToString('N') + '.jsonl')
    Write-Host 'Exit the existing ReStart instance before continuing. This script will not stop it.'
    Read-Host 'Press Enter when ready' | Out-Null
    $process = Start-Process -FilePath $executable -ArgumentList @('--diagnose-windows-key', ('"' + $report + '"')) -WindowStyle Hidden -PassThru
    try {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $ready = $false
        while ($watch.Elapsed.TotalSeconds -lt 105) {
            if ($process.HasExited) {
                if ($process.ExitCode -eq 3) { throw 'Another ReStart instance is still running. Exit it from the tray, then run this script again.' }
                if ($process.ExitCode -eq 4) { throw 'The diagnostic report could not be created. Extract the package into a writable folder and retry.' }
                break
            }
            if (Test-Path -LiteralPath $report -PathType Leaf) {
                $content = Read-SharedReport $report
                if (-not $ready -and $content -match '"stage":"Ready"') {
                    $ready = $true
                    Write-Host ''
                    Write-Host 'READY. Recording stops automatically 90 seconds after launch.' -ForegroundColor Cyan
                    Write-Host 'Dismiss ReStart. Focus a normal app on the affected secondary display.'
                    Write-Host 'Press the physical Windows key once. If it fails, wait about 10 seconds.'
                    Write-Host 'Then try Ctrl+Alt+Space once for comparison. No typing is needed.'
                    Write-Host 'Keep taskbar reveal and Start-button interception disabled for this check.'
                }
                if ($content -match '"stage":"Completed"') { break }
            }
            Start-Sleep -Milliseconds 250
        }
        if (-not (Test-Path -LiteralPath $report -PathType Leaf)) { throw 'No report was created.' }
        Write-Host ''
        Write-Host 'Report saved in Windows-key-reports next to this script.' -ForegroundColor Cyan
        if ((Read-SharedReport $report) -notmatch '"stage":"Completed"') {
            Write-Host 'The report is incomplete; keep it anyway, since its last checkpoint may identify the delay.'
        }
        Write-Host 'Review the JSONL file, then attach only that file to the issue.'
        Write-Host 'Exit this diagnostic build from its tray menu and return to the stable build.'
        Write-Host 'The script does not exit, restart, update, or replace ReStart automatically.'
    } finally { $process.Dispose() }
} catch {
    # All displayed errors are fixed instructions. Do not expose exception paths.
    if ($_.Exception.Message -in @(
        'Extract the entire diagnostic ZIP before running this script.',
        'Another ReStart instance is still running. Exit it from the tray, then run this script again.',
        'The diagnostic report could not be created. Extract the package into a writable folder and retry.',
        'No report was created.'
    )) { Write-Host $_.Exception.Message -ForegroundColor Red }
    else { Write-Host 'The diagnostic session could not finish. Keep any report already created; no app was stopped or replaced.' -ForegroundColor Red }
    exit 1
}
