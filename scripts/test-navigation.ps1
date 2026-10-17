[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$snapshot = Join-Path $repoRoot 'artifacts/navigation/navigation.png'
New-Item -ItemType Directory -Path (Split-Path -Parent $snapshot) -Force | Out-Null
$process = Start-Process -FilePath $source -ArgumentList @('--smoke-test', '--navigation-test', '--snapshot', ('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Navigation verification timed out.' }
    $reportPath = [IO.Path]::ChangeExtension($snapshot, '.json')
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath -Raw | Write-Output }
        throw "Navigation verification failed: $($process.ExitCode)"
    }
    $result = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $result.success -or -not $result.navigation) { throw 'Navigation verification reported failure.' }
    $result.navigation
}
finally { $process.Dispose() }
