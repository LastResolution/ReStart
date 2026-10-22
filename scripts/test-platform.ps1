[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable, [switch]$Visible)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$directory = Join-Path $repoRoot ('artifacts/platform/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$errors = Join-Path $directory 'verify.log'
$arguments = @('--verify-platform', ('"' + $directory + '"'))
if ($Visible) { $arguments += '--visible' }
$process = Start-Process -FilePath $source -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardError $errors
try {
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Platform verification timed out.' }
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $errors | Write-Output; throw 'Platform verification failed.' }
    $result = Get-Content -LiteralPath (Join-Path $directory 'verification.json') -Raw | ConvertFrom-Json
    if (-not $result.success -or $result.monitorCount -lt 1 -or $result.automationCases -ne 16 -or $result.checks -lt 300) { throw 'Incomplete platform verification.' }
    $result | ConvertTo-Json -Depth 6
    Write-Output $directory
}
finally { $process.Dispose() }
