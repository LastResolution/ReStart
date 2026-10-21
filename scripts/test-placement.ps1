[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$testRoot = Join-Path $repoRoot ('artifacts/placement/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
# A fresh executable directory keeps persistence checks away from any prior preview data.
foreach ($entry in Get-ChildItem -LiteralPath (Split-Path -Parent $source)) {
    if ($entry.Name -eq 'preview-data' -or $entry.Name -like '*.WebView2') { continue }
    Copy-Item -LiteralPath $entry.FullName -Destination $testRoot -Recurse
}
$snapshot = Join-Path $testRoot 'placement.png'
$process = Start-Process -FilePath (Join-Path $testRoot 'ReStart.exe') -ArgumentList @('--smoke-test', '--placement-test', '--snapshot', ('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Placement verification timed out.' }
    $reportPath = [IO.Path]::ChangeExtension($snapshot, '.json')
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath -Raw | Write-Output }
        throw "Placement verification failed: $($process.ExitCode)"
    }
    $result = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $result.success -or -not $result.placement) { throw 'Placement verification reported failure.' }
    $result.placement
    Write-Output (Join-Path $testRoot 'placement-board.png')
    Write-Output (Join-Path $testRoot 'placement-folder.png')
}
finally { $process.Dispose() }
