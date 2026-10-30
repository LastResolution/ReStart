[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$probeDirectory = Join-Path $repoRoot 'artifacts/shell-probe'
dotnet build (Join-Path $repoRoot 'tools/ReStart.ShellProbe/ReStart.ShellProbe.csproj') -c Release -p:RestoreLockedMode=true -o $probeDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'Shell probe build failed.' }
$previousProbe = $env:RESTART_TEST_SHELL_PROBE
$env:RESTART_TEST_SHELL_PROBE = Join-Path $probeDirectory 'ReStart.ShellProbe.exe'
$source = (Resolve-Path -LiteralPath $Executable).Path
$snapshot = Join-Path $repoRoot ('artifacts/jump-lists/' + [Guid]::NewGuid().ToString('N') + '/start.png')
New-Item -ItemType Directory -Path (Split-Path -Parent $snapshot) -Force | Out-Null
$process = Start-Process -FilePath $source -ArgumentList @('--smoke-test','--jump-list-test','--snapshot',('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'Native jump-list verification timed out.' }
    $result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot,'.json')) -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success -or -not $result.nativeJumps) {
        $result | ConvertTo-Json -Depth 6 | Write-Output
        throw 'Native jump-list verification failed.'
    }
    $result.nativeJumps
}
finally { $process.Dispose(); $env:RESTART_TEST_SHELL_PROBE = $previousProbe }
