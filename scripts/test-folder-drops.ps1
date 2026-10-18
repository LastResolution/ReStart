[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$directory = Join-Path $repoRoot ('artifacts/folder-drops/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$snapshot = Join-Path $directory 'start.png'
$process = Start-Process -FilePath $source -ArgumentList @('--smoke-test', '--folder-drop-test', '--snapshot', ('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    $finished = $false
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        if ($process.WaitForExit(30000)) { $finished = $true; break }
        Write-Output 'Folder-drop verification is still running.'
    }
    if (-not $finished) { $process.Kill(); throw 'Folder-drop verification timed out.' }
    $result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot, '.json')) -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success -or -not $result.folderDrops) {
        $result | ConvertTo-Json -Depth 5 | Write-Output
        throw 'Folder-drop verification failed.'
    }
    $result.folderDrops
}
finally { $process.Dispose() }
