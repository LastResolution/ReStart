[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$files = [ordered]@{
    'en' = 'README.md'; 'ja' = 'README.ja.md'; 'de' = 'README.de.md'; 'fr' = 'README.fr.md'
    'es' = 'README.es.md'; 'ko' = 'README.ko.md'; 'zh-Hans' = 'README.zh-CN.md'; 'zh-Hant' = 'README.zh-TW.md'
}
$catalogs = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/ReStart.Core/Locales') -Filter '*.json' | ForEach-Object BaseName)
if (@($catalogs | Where-Object { -not $files.Contains($_) }).Count -gt 0 -or $catalogs.Count -ne $files.Count) { throw 'README languages do not match the supported locale catalogs.' }
foreach ($entry in $files.GetEnumerator()) {
    $text = Get-Content -LiteralPath (Join-Path $repoRoot $entry.Value) -Raw
    foreach ($file in $files.Values) {
        if (-not $text.Contains("]($file)")) { throw "Missing language link in $($entry.Value): $file" }
    }
    if ($entry.Key -ne 'en' -and ($text -notmatch '(?m)^>.*(?:AI|KI|IA)' -or -not $text.Contains('](README.md)'))) {
        throw "Missing visible AI-translation notice or English source in $($entry.Value)."
    }
    $links = @([regex]::Matches($text, '\]\((?<url>[^)\s]+)\)|(?:href|src)="(?<url>[^"\s]+)"'))
    foreach ($match in $links) {
        $url = $match.Groups['url'].Value
        if ($url -match '^(?:https?://|#)') { continue }
        $relative = [Uri]::UnescapeDataString(($url -split '#')[0])
        if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relative))) { throw "Broken local link in $($entry.Value): $relative" }
    }
}
Write-Output "Verified $($files.Count) README languages, AI-translation notices and local links/images."
