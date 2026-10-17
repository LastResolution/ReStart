[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $repoRoot ('artifacts/privacy-check/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$oldIndex = $env:GIT_INDEX_FILE
$oldEmail = $env:GIT_AUTHOR_EMAIL
$oldCommitter = $env:GIT_COMMITTER_EMAIL
try {
    $env:GIT_INDEX_FILE = Join-Path $testRoot 'index'
    $env:GIT_AUTHOR_EMAIL = '1+fixture@users.noreply.github.com'
    $env:GIT_COMMITTER_EMAIL = '1+fixture@users.noreply.github.com'
    $fixture = Join-Path $testRoot 'fixture.txt'
    foreach ($mode in @('clean', 'culture', 'path', 'token', 'author', 'committer')) {
        git read-tree --empty
        if ($LASTEXITCODE -ne 0) { throw 'Cannot create isolated test index.' }
        $value = switch ($mode) {
            'path' { ('C:' + '\Users\FixtureUser\sample.txt') }
            'token' { ('ghp_' + ('a' * 36)) }
            'culture' { ('asi-lk' + 'sk-' + ('sk-SKsl-SIes-ES' * 8)) }
            default { 'No private data.' }
        }
        [IO.File]::WriteAllText($fixture, $value)
        git add --force -- $fixture
        if ($LASTEXITCODE -ne 0) { throw 'Cannot stage privacy fixture.' }
        $env:GIT_AUTHOR_EMAIL = if ($mode -eq 'author') { 'fixture@example.invalid' } else { '1+fixture@users.noreply.github.com' }
        $env:GIT_COMMITTER_EMAIL = if ($mode -eq 'committer') { 'fixture@example.invalid' } else { '1+fixture@users.noreply.github.com' }
        $rejected = $false
        try { & (Join-Path $PSScriptRoot 'check-privacy.ps1') -Staged | Out-Null }
        catch { $rejected = $true }
        if ($rejected -ne ($mode -notin @('clean', 'culture'))) { throw "Unexpected privacy result: $mode" }
    }
    foreach ($unsafe in @($false, $true)) {
        $zip = Join-Path $testRoot ("package-$unsafe.zip")
        $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
        try {
            $entry = $archive.CreateEntry('fixture.bin'); $stream = $entry.Open()
            try {
                $text = if ($unsafe) { 'C:' + '\Users\FixtureUser\source.pdb' } else { '/_/ReStart/source.pdb' }
                $bytes = [Text.Encoding]::Unicode.GetBytes($text); $stream.Write($bytes)
            }
            finally { $stream.Dispose() }
        }
        finally { $archive.Dispose() }
        $rejected = $false
        try { & (Join-Path $PSScriptRoot 'check-privacy.ps1') -Package $zip | Out-Null }
        catch { $rejected = $true }
        if ($rejected -ne $unsafe) { throw 'Unexpected binary package privacy result.' }
    }
    Write-Output 'Verified staged content, effective Git identities and UTF-16 binary package path checks.'
}
finally { $env:GIT_INDEX_FILE = $oldIndex; $env:GIT_AUTHOR_EMAIL = $oldEmail; $env:GIT_COMMITTER_EMAIL = $oldCommitter }
