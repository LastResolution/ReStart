[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$rules = @{
    'ReStart.Core' = @()
    'ReStart.TileHost' = @('ReStart.Core')
    'ReStart.App' = @('ReStart.Core', 'ReStart.TileHost')
}
foreach ($name in $rules.Keys) {
    $path = Join-Path $repoRoot "src/$name/$name.csproj"
    $project = [xml](Get-Content -LiteralPath $path -Raw)
    foreach ($reference in $project.SelectNodes('//ProjectReference')) {
        $target = [IO.Path]::GetFileNameWithoutExtension($reference.Include.Replace('\', '/'))
        if ($target -notin $rules[$name]) { throw "Forbidden project dependency: $name -> $target" }
        if ($name -eq 'ReStart.App' -and $target -eq 'ReStart.TileHost' -and $reference.GetAttribute('ReferenceOutputAssembly') -ne 'false') {
            throw 'The UI-to-worker reference must remain build-only.'
        }
    }
    if ($name -ne 'ReStart.App' -and $project.SelectNodes('//UseWPF | //UseWindowsForms').InnerText -contains 'true') {
        throw "UI framework enabled outside the App project: $name"
    }
    if ($name -eq 'ReStart.Core' -and $project.SelectNodes('//PackageReference | //Reference').Count -gt 0) {
        throw 'Review the architecture before adding external assembly dependencies to Core.'
    }
}
$entryFiles = @{
    'src/ReStart.Core' = @('GlobalUsings.cs')
    'src/ReStart.App' = @('App.xaml.cs', 'GlobalUsings.cs')
    'tests/ReStart.Core.Tests' = @('GlobalUsings.cs')
}
foreach ($directory in $entryFiles.Keys) {
    foreach ($source in Get-ChildItem -LiteralPath (Join-Path $repoRoot $directory) -Filter '*.cs' -File) {
        if ($source.Name -notin $entryFiles[$directory]) { throw "Place feature code in its owning folder instead of the project root: $directory/$($source.Name)" }
    }
}
Write-Output 'Verified project dependency boundaries and feature-folder placement.'
