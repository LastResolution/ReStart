[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testDirectory = Join-Path $repoRoot ('artifacts/windows-key-trace/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$report = Join-Path $testDirectory 'verification.json'
$source = (Resolve-Path -LiteralPath $Executable).Path
$process = Start-Process -FilePath $source -ArgumentList @('--verify-windows-key-diagnostics', ('"' + $report + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(20000)) { throw 'The owned diagnostic route did not finish.' }
    if ($process.ExitCode -ne 0) { throw 'The owned diagnostic route failed.' }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if (-not $result.success -or $result.routedToggles -ne 1) { throw 'Diagnostic route assertions failed.' }
    Write-Output 'PASS: owned hotkey route, dispatcher timing, hook/UI heartbeats and closed report schema.'
} finally {
    if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    $process.Dispose()
}
