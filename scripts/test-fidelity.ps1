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
$snapshot = Join-Path $repoRoot ('artifacts/fidelity/' + [Guid]::NewGuid().ToString('N') + '/start.png')
New-Item -ItemType Directory -Path (Split-Path -Parent $snapshot) -Force | Out-Null
$process = Start-Process -FilePath $source -ArgumentList @('--smoke-test','--fidelity-test','--snapshot',('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Fidelity verification timed out.' }
    $result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot,'.json')) -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.success -or -not $result.fidelity) {
        $result | ConvertTo-Json -Depth 5 | Write-Output
        throw 'Fidelity verification failed.'
    }
    $result.fidelity
}
finally { $process.Dispose(); $env:RESTART_TEST_SHELL_PROBE = $previousProbe }
