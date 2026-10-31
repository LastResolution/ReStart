[CmdletBinding()]
param(
    [string]$Executable = '',
    [ValidatePattern('^[\p{L}\p{N} _-]{1,80}$')][string]$SearchQuery = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) {
    $buildPath = Join-Path $repoRoot 'artifacts/smoke/app'
    dotnet build (Join-Path $repoRoot 'src/ReStart.App/ReStart.App.csproj') -c Release -o $buildPath
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    $Executable = Join-Path $buildPath 'ReStart.exe'
}
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$snapshot = Join-Path $repoRoot 'artifacts/smoke/start.png'
$arguments = @('--smoke-test', '--snapshot', ('"' + $snapshot + '"'))
if ($SearchQuery) { $arguments += @('--search-query', ('"' + $SearchQuery + '"')) }
$process = Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Smoke test timed out.' }
if ($process.ExitCode -ne 0) {
    $report = [IO.Path]::ChangeExtension($snapshot, '.json')
    if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report -Raw | Write-Output }
    throw "Smoke test failed: $($process.ExitCode)"
}
$result = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($snapshot, '.json')) -Raw | ConvertFrom-Json
if (-not $result.success) { throw 'Smoke test reported failure.' }
$result
