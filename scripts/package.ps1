[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishPath = Join-Path $repoRoot "artifacts/publish/$Runtime"
$packagePath = Join-Path $repoRoot 'artifacts/packages'
$zipPath = Join-Path $packagePath "ReStart-$Runtime.zip"
New-Item -ItemType Directory -Force -Path $publishPath, $packagePath | Out-Null

dotnet publish (Join-Path $repoRoot 'src/ReStart.App/ReStart.App.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $publishPath
if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $publishPath
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/portable-README.txt') -Destination (Join-Path $publishPath 'README.txt')
# Explicit file list prevents preview settings, personal layouts, and stale files entering a release.
Compress-Archive -LiteralPath @((Join-Path $publishPath 'ReStart.exe'), (Join-Path $publishPath 'LICENSE'), (Join-Path $publishPath 'README.txt')) -DestinationPath $zipPath -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zipPath.sha256", "$hash  $([IO.Path]::GetFileName($zipPath))`n", [Text.UTF8Encoding]::new($false))
Write-Output $zipPath
Write-Output "SHA256: $hash"
