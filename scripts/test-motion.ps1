[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$directory = Join-Path $repoRoot ('artifacts/motion/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$snapshot = Join-Path $directory 'start.png'
$process = Start-Process -FilePath $source -ArgumentList @('--smoke-test', '--motion-test', '--snapshot', ('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Motion verification timed out.' }
    $result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot, '.json')) -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success -or -not $result.motion) {
        $result | ConvertTo-Json -Depth 5 | Write-Output
        throw 'Motion verification failed.'
    }
    $result.motion
}
finally { $process.Dispose() }
