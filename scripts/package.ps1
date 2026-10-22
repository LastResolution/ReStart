[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishPath = Join-Path $repoRoot "artifacts/publish/$Runtime"
$buildPath = Join-Path $repoRoot "artifacts/build/$Runtime"
$packagePath = Join-Path $repoRoot 'artifacts/packages'
$zipPath = Join-Path $packagePath "ReStart-$Runtime.zip"
New-Item -ItemType Directory -Force -Path $publishPath, $packagePath | Out-Null

# SDK project references can build RID-neutral libraries even during a RID publish.
# Restore that graph explicitly: a clean checkout has no ordinary obj/assets yet.
dotnet restore (Join-Path $repoRoot 'src/ReStart.App/ReStart.App.csproj') --locked-mode
if ($LASTEXITCODE -ne 0) { throw "Restore failed: $LASTEXITCODE" }

dotnet publish (Join-Path $repoRoot 'src/ReStart.App/ReStart.App.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None "-p:OutputPath=$buildPath/" -o $publishPath
if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }

$workerPath = Join-Path $publishPath 'TileHost'
dotnet publish (Join-Path $repoRoot 'src/ReStart.TileHost/ReStart.TileHost.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $workerPath
if ($LASTEXITCODE -ne 0) { throw "Tile host publish failed: $LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $publishPath
$version = ([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props'))).Project.PropertyGroup.Version
$template = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/portable-README.txt') -Raw
[IO.File]::WriteAllText((Join-Path $publishPath 'README.txt'), $template.Replace('@VERSION@', $version), [Text.UTF8Encoding]::new($false))
# Explicit file list prevents preview settings, personal layouts, and stale files entering a release.
$stage = Join-Path $repoRoot ("artifacts/stage/" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage, (Join-Path $stage 'TileHost') | Out-Null
foreach ($name in @('ReStart.exe', 'LICENSE', 'README.txt')) { Copy-Item -LiteralPath (Join-Path $publishPath $name) -Destination $stage }
Copy-Item -LiteralPath (Join-Path $workerPath 'ReStart.TileHost.exe') -Destination (Join-Path $stage 'TileHost')
Copy-Item -LiteralPath (Join-Path $repoRoot 'examples/tiles') -Destination (Join-Path $stage 'Examples') -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'third-party') -Destination $stage -Recurse
# Carry the notices from the exact runtime packs resolved for this build.
$assets = Get-Content -LiteralPath (Join-Path $repoRoot "src/ReStart.App/obj/$Runtime/project.assets.json") -Raw | ConvertFrom-Json
function Find-RuntimeNotice([string]$packageId, [string]$name) {
    $dependency = @($assets.project.frameworks.PSObject.Properties | ForEach-Object { $_.Value.downloadDependencies } | Where-Object { $_.name -eq $packageId }) | Select-Object -First 1
    if (-not $dependency) { throw "Runtime pack not resolved: $packageId" }
    $version = $dependency.version.Trim([char[]]'[]()').Split(',')[0].Trim()
    foreach ($packageRoot in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $packageRoot ($packageId.ToLowerInvariant() + '/' + $version + '/' + $name)
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    throw "Runtime notice not found: $packageId/$name"
}
Copy-Item -LiteralPath (Find-RuntimeNotice "Microsoft.NETCore.App.Runtime.$Runtime" 'LICENSE.TXT') -Destination (Join-Path $stage 'third-party/DotNet-Runtime-LICENSE.txt')
Copy-Item -LiteralPath (Find-RuntimeNotice "Microsoft.NETCore.App.Runtime.$Runtime" 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $stage 'third-party/DotNet-Runtime-NOTICES.txt')
Copy-Item -LiteralPath (Find-RuntimeNotice "Microsoft.WindowsDesktop.App.Runtime.$Runtime" 'LICENSE') -Destination (Join-Path $stage 'third-party/DotNet-Desktop-LICENSE.txt')
# Only the explicit distribution stage is inventoried; no installation or local data is scanned.
$inventory = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
    if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked distribution files are not allowed.' }
    [ordered]@{
        path = [IO.Path]::GetRelativePath($stage, $_.FullName).Replace('\', '/')
        size = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$manifest = [ordered]@{ schema = 1; version = $version; runtime = $Runtime; files = $inventory } | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $stage 'ReStart-package.json'), $manifest, [Text.UTF8Encoding]::new($false))
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zipPath.sha256", "$hash  $([IO.Path]::GetFileName($zipPath))`n", [Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'check-privacy.ps1') -Package $zipPath
Write-Output $zipPath
Write-Output "SHA256: $hash"
