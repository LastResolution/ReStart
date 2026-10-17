[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $repoRoot ('artifacts/lifecycle/' + [Guid]::NewGuid().ToString('N'))
$source = (Resolve-Path -LiteralPath $Executable).Path
$owned = [Collections.Generic.List[Diagnostics.Process]]::new()
try {
    $copies = @('first', 'second') | ForEach-Object {
        $directory = Join-Path $testRoot $_
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        foreach ($name in @('ReStart.exe', 'ReStart.dll', 'ReStart.Core.dll', 'ReStart.deps.json', 'ReStart.runtimeconfig.json')) {
            $file = Join-Path (Split-Path -Parent $source) $name
            if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination $directory }
        }
        Join-Path $directory 'ReStart.exe'
    }
    # Preview never installs hooks, a tray icon, or a real startup entry.
    $primary = Start-Process -FilePath $copies[0] -ArgumentList @('--preview', '--background') -WindowStyle Hidden -PassThru
    $owned.Add($primary)
    Start-Sleep -Seconds 3
    if ($primary.HasExited) { throw 'Primary failed to start; close an existing preview before this test.' }
    $duplicates = 1..10 | ForEach-Object {
        $process = Start-Process -FilePath $copies[$_ % 2] -ArgumentList @('--preview', '--background') -WindowStyle Hidden -PassThru
        $owned.Add($process)
        $process
    }
    foreach ($process in $duplicates) {
        if (-not $process.WaitForExit(15000)) { throw 'A duplicate stayed running.' }
        if ($process.ExitCode -ne 0) { throw "Duplicate failed: $($process.ExitCode)" }
    }
    $primary.Refresh()
    if ($primary.HasExited) { throw 'Primary exited after duplicate launches.' }
    $smoke = Start-Process -FilePath $copies[1] -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru
    $owned.Add($smoke)
    if (-not $smoke.WaitForExit(15000) -or $smoke.ExitCode -ne 2) { throw 'A smoke test must report an existing preview instead of falsely succeeding.' }
    Write-Output 'PASS: ten launches from two executable paths leave one primary; duplicate smoke returns exit code 2.'
}
finally {
    foreach ($process in $owned) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
        $process.Dispose()
    }
}
