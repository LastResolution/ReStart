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
    $contributors = [regex]::Matches($text, '(?s)<!-- contributors:start -->(.*?)<!-- contributors:end -->')
    if ($contributors.Count -ne 1 -or -not $contributors[0].Value.Contains('src="https://lastresolution.github.io/ReStart/assets/contributors.svg"') -or
        -not $contributors[0].Value.Contains('href="https://github.com/LastResolution/ReStart/graphs/contributors"')) {
        throw "Missing shared contributor image or graph link in $($entry.Value)."
    }
    foreach ($file in $files.Values) {
        if (-not $text.Contains("]($file)") -and -not $text.Contains('href="' + $file + '"')) { throw "Missing language link in $($entry.Value): $file" }
    }
    if ($entry.Key -ne 'en' -and ($text -notmatch '(?m)^>.*(?:AI|KI|IA)' -or -not $text.Contains('](README.md)'))) {
        throw "Missing visible AI-translation notice or English source in $($entry.Value)."
    }
    # Package examples contain virtual resource URLs, not documentation links.
    $linkText = [regex]::Replace($text, '(?ms)^```[^\r\n]*\r?\n.*?^```[ \t]*\r?$', '')
    $links = @([regex]::Matches($linkText, '\]\((?<url>[^)\s]+)\)|(?:href|src)="(?<url>[^"\s]+)"'))
    foreach ($match in $links) {
        $url = $match.Groups['url'].Value
        if ($url -match '^(?:https?://|#)') { continue }
        $relative = [Uri]::UnescapeDataString(($url -split '#')[0])
        if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relative))) { throw "Broken local link in $($entry.Value): $relative" }
    }
}
Write-Output "Verified $($files.Count) README languages, AI-translation notices and local links/images."
