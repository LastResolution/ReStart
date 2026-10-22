[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
if (-not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $source) 'ReStart-package.json'))) {
    throw 'Update process verification requires an extracted self-contained portable package.'
}
$directory = Join-Path $repoRoot ('artifacts/update-installation/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$process = Start-Process -FilePath $source -ArgumentList @('--verify-update-installations', ('"' + $directory + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(240000)) { $process.Kill($true); throw 'Update installation verification timed out.' }
    if ($process.ExitCode -ne 0) { throw "Update installation verification failed: $($process.ExitCode)" }
    $report = Get-Content -LiteralPath (Join-Path $directory 'verification.json') -Raw | ConvertFrom-Json
    if (-not $report.success -or $report.scenarios -ne 4) { throw 'Update installation verification reported failure.' }
    $report
}
finally { $process.Dispose() }
