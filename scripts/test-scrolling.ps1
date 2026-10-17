[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$testRoot = Join-Path $repoRoot ('artifacts/scrolling/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
foreach ($name in @('ReStart.exe', 'ReStart.dll', 'ReStart.Core.dll', 'ReStart.deps.json', 'ReStart.runtimeconfig.json')) {
    $file = Join-Path (Split-Path -Parent $source) $name
    if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination $testRoot }
}
$snapshot = Join-Path $testRoot 'scroll.png'
$process = Start-Process -FilePath (Join-Path $testRoot 'ReStart.exe') -ArgumentList @('--smoke-test', '--scroll-test', '--snapshot', ('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Scroll verification timed out.' }
    $result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot, '.json')) -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success -or -not $result.scrolling) {
        $result | ConvertTo-Json -Depth 5 | Write-Output
        throw 'Scroll verification failed.'
    }
    $result.scrolling.Cases | Format-Table
    if ($result.scrolling.DragStabilityChecks -ne 32) { throw 'Scrolled group drag stability was not verified.' }
    Write-Output "Stationary scrolled drag checks: $($result.scrolling.DragStabilityChecks)"
    Write-Output $testRoot
}
finally { $process.Dispose() }
