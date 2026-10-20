[CmdletBinding()]
param([string]$ReportJson = '', [switch]$SelfTest)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tools/ReStart.DocCheck/ReStart.DocCheck.csproj'
$arguments = @($repoRoot)
if ($SelfTest) { $arguments += '--self-test' }
if ($ReportJson) { $arguments += @('--report-json', [IO.Path]::GetFullPath($ReportJson)) }
# An independent SDK-hosted process avoids PowerShell's already-loaded Roslyn
# assemblies, which can differ from the SDK selected by global.json on CI.
dotnet run --project $project --configuration Release --no-restore --no-launch-profile -- @arguments
if ($LASTEXITCODE -ne 0) { throw 'Public API documentation verification failed.' }
