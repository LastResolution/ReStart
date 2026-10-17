[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$snapshot = Join-Path $repoRoot ('artifacts/bundled-tiles/' + [Guid]::NewGuid().ToString('N') + '/start.png')
New-Item -ItemType Directory -Path (Split-Path -Parent $snapshot) -Force | Out-Null
$process = Start-Process -FilePath $source -ArgumentList @('--smoke-test', '--bundled-tiles-test', '--snapshot', ('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(90000)) { $process.Kill(); throw 'Bundled tile verification timed out.' }
    $result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot, '.json')) -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success -or -not $result.bundledTiles) {
        $result | ConvertTo-Json -Depth 5 | Write-Output
        throw 'Bundled tile verification failed.'
    }
    $result.bundledTiles
    Write-Output (Split-Path -Parent $snapshot)
}
finally { $process.Dispose() }
