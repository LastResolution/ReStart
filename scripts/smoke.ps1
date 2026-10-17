[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
dotnet build (Join-Path $repoRoot 'ReStart.slnx') -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
$executable = Join-Path $repoRoot 'src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe'
$snapshot = Join-Path $repoRoot 'artifacts/smoke/start.png'
$process = Start-Process -FilePath $executable -ArgumentList @('--smoke-test', '--snapshot', ('"' + $snapshot + '"')) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Smoke test timed out.' }
if ($process.ExitCode -ne 0) { throw "Smoke test failed: $($process.ExitCode)" }
$result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot, '.json')) -Raw | ConvertFrom-Json
if (-not $result.success) { throw 'Smoke test reported failure.' }
$result
