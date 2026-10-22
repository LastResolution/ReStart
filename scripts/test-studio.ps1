[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable, [switch]$RecoveryOnly)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$directory = Join-Path $repoRoot ('artifacts/studio/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$errors = Join-Path $directory 'render.log'
$arguments = @('--render-studio', ('"' + $directory + '"'))
if ($RecoveryOnly) { $arguments += '--recovery-only' }
$process = Start-Process -FilePath $source -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardError $errors
try {
    if (-not $process.WaitForExit(600000)) { $process.Kill(); throw 'Web tile and Tile Studio verification timed out.' }
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $errors | Write-Output; throw 'Web tile and Tile Studio verification failed.' }
    $result = Get-Content -LiteralPath (Join-Path $directory 'verification.json') -Raw | ConvertFrom-Json
    $minimumChecks = if ($RecoveryOnly) { 45 } else { 9300 }
    if (-not $result.success -or $result.checks -lt $minimumChecks) { throw 'Incomplete web tile verification.' }
    $result
    Write-Output $directory
}
finally { $process.Dispose() }
