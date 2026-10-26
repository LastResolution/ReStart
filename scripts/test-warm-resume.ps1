[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable, [ValidateRange(1,1800)][int]$IdleSeconds = 3)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$snapshot = Join-Path $repoRoot ('artifacts/warm-resume/' + [Guid]::NewGuid().ToString('N') + '/start.png')
New-Item -ItemType Directory -Path (Split-Path -Parent $snapshot) -Force | Out-Null
$process = Start-Process -FilePath $source -ArgumentList @('--smoke-test','--warm-resume-test','--resume-idle-seconds',"$IdleSeconds",'--snapshot',('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(90000 + $IdleSeconds * 1000)) { $process.Kill(); throw 'Warm resume verification timed out.' }
    $result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot,'.json')) -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success -or -not $result.warmResume) {
        $result | ConvertTo-Json -Depth 6 | Write-Output
        throw 'Warm resume verification failed.'
    }
    $result.warmResume | ConvertTo-Json -Depth 5
    Write-Output (Split-Path -Parent $snapshot)
}
finally { $process.Dispose() }
