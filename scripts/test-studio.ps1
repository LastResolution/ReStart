[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$directory = Join-Path $repoRoot ('artifacts/studio/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$errors = Join-Path $directory 'render.log'
$process = Start-Process -FilePath $source -ArgumentList @('--render-studio', ('"' + $directory + '"')) -WindowStyle Hidden -PassThru -RedirectStandardError $errors
try {
    if (-not $process.WaitForExit(600000)) { $process.Kill(); throw 'Web tile and Tile Studio verification timed out.' }
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $errors | Write-Output; throw 'Web tile and Tile Studio verification failed.' }
    $result = Get-Content -LiteralPath (Join-Path $directory 'verification.json') -Raw | ConvertFrom-Json
    if (-not $result.success -or $result.checks -lt 8400) { throw 'Incomplete web tile verification.' }
    $result
    Write-Output $directory
}
finally { $process.Dispose() }
