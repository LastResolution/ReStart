[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable, [ValidateRange(3,1800)][int]$SoakSeconds = 3)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$directory = Join-Path $repoRoot ('artifacts/reliability/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$source = (Resolve-Path -LiteralPath $Executable).Path
$log = Join-Path $directory 'fixture.log'
$process = Start-Process -FilePath $source -ArgumentList @('--render-studio', ('"' + $directory + '"'), '--reliability-only','--soak-seconds',"$SoakSeconds") -WindowStyle Hidden -RedirectStandardError $log -PassThru
try {
    if (!$process.WaitForExit(180000 + $SoakSeconds * 1000)) { $process.Kill($true); throw 'Tile reliability verification timed out.' }
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $log; throw 'Tile reliability verification failed.' }
    Get-Content -LiteralPath (Join-Path $directory 'reliability.json') -Raw
} finally { $process.Dispose() }
