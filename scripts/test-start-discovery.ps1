[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable, [ValidateRange(1000, 200000)][int]$Iterations = 10000)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$report = Join-Path $repoRoot ('artifacts/start-discovery/' + [Guid]::NewGuid().ToString('N') + '/verification.json')
New-Item -ItemType Directory -Path (Split-Path -Parent $report) -Force | Out-Null
$process = Start-Process -FilePath $source -ArgumentList @('--verify-start-discovery', ('"' + $report + '"'), $Iterations) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(600000)) { $process.Kill(); throw 'Start discovery verification timed out.' }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success -or $result.measurements.iterations -ne $Iterations -or $result.measurements.rootsDuringSoak -ne 0) {
        $result | ConvertTo-Json -Depth 5 | Write-Output
        throw 'Start discovery verification failed.'
    }
    Write-Output "Verified $($result.checks) Start discovery checks and $Iterations repeated polls of two owned taskbars."
}
finally { $process.Dispose() }
