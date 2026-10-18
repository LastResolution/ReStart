[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$directory = Join-Path $repoRoot ('artifacts/editing/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$snapshot = Join-Path $directory 'start.png'
$process = Start-Process -FilePath $source -ArgumentList @('--smoke-test', '--editing-test', '--snapshot', ('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Layout editing verification timed out.' }
    $result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot, '.json')) -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success -or -not $result.editing) {
        $result | ConvertTo-Json -Depth 5 | Write-Output
        throw 'Layout editing verification failed.'
    }
    $result.editing
}
finally { $process.Dispose() }
