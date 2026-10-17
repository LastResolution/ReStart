[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable, [switch]$AppFolders)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$snapshotName = if ($AppFolders) { 'app-folders' } else { 'navigation' }
$snapshot = Join-Path $repoRoot "artifacts/navigation/$snapshotName.png"
New-Item -ItemType Directory -Path (Split-Path -Parent $snapshot) -Force | Out-Null
$mode = if ($AppFolders) { '--app-tree-test' } else { '--navigation-test' }
$process = Start-Process -FilePath $source -ArgumentList @('--smoke-test', $mode, '--snapshot', ('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Navigation verification timed out.' }
    $reportPath = [IO.Path]::ChangeExtension($snapshot, '.json')
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath -Raw | Write-Output }
        throw "Navigation verification failed: $($process.ExitCode)"
    }
    $result = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $report = if ($AppFolders) { $result.appFolders } else { $result.navigation }
    if (-not $result.success -or -not $report) { throw 'Navigation verification reported failure.' }
    $report
}
finally { $process.Dispose() }
