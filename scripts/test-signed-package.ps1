[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$directory = Join-Path $repoRoot ('artifacts/signature-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
# A publicly signed SDK executable supplies a real verification fixture. No
# certificate is created, imported or trusted and no private key is accessed.
$fixture = (Get-Command dotnet).Source
$signature = Get-AuthenticodeSignature -LiteralPath $fixture
if ($signature.Status -ne 'Valid' -or $signature.SignatureType -ne 'Authenticode' -or $null -eq $signature.TimeStamperCertificate) { throw 'The SDK signature fixture is not valid.' }
$signedBytes = [IO.File]::ReadAllBytes($fixture)
$optional = [BitConverter]::ToInt32($signedBytes, 60) + 24
$security = $optional + $(if ([BitConverter]::ToUInt16($signedBytes, $optional) -eq 0x20b) { 112 } else { 96 }) + 32
$certificateOffset = [BitConverter]::ToUInt32($signedBytes, $security)
$unsignedBytes = [byte[]]::new($certificateOffset)
[Array]::Copy($signedBytes, $unsignedBytes, $certificateOffset)
[Array]::Clear($unsignedBytes, $security, 8)
function Write-Fixture([string]$Path, [byte[]]$Executable, [string]$Mode = '') {
    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = @{ 'ReStart.exe' = $Executable; 'TileHost/ReStart.TileHost.exe' = $Executable; 'README.txt' = [Text.Encoding]::UTF8.GetBytes('Fixture.') }
        if ($Mode -eq 'extra') { $files['extra.txt'] = [byte[]]@(1) }
        if ($Mode -eq 'missing') { $files.Remove('TileHost/ReStart.TileHost.exe') }
        if ($Mode -eq 'document') { $files['README.txt'] = [byte[]]@(2) }
        if ($Mode -eq 'traversal') { $files['../outside.txt'] = [byte[]]@(3) }
        foreach ($name in $files.Keys) {
            $entry = $archive.CreateEntry($name); $stream = $entry.Open()
            try { $stream.Write($files[$name]) } finally { $stream.Dispose() }
        }
        if ($Mode -eq 'duplicate') { $null = $archive.CreateEntry('restart.EXE') }
    }
    finally { $archive.Dispose() }
}
$unsigned = Join-Path $directory 'original.zip'
Write-Fixture $unsigned $unsignedBytes
foreach ($mode in @('valid', 'unsigned', 'thumbprint', 'code', 'document', 'extra', 'missing', 'traversal', 'duplicate')) {
    $bytes = [byte[]]$signedBytes.Clone()
    if ($mode -eq 'code') { $bytes[64] = $bytes[64] -bxor 1 }
    if ($mode -eq 'unsigned') { $bytes = $unsignedBytes }
    $thumbprint = if ($mode -eq 'thumbprint') { '0' * 40 } else { $signature.SignerCertificate.Thumbprint }
    $package = Join-Path $directory "$mode.zip"
    Write-Fixture $package $bytes $mode
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'verify-signed-package.ps1') -UnsignedPackage $unsigned -SignedPackage $package -ExpectedThumbprint $thumbprint | Out-Null }
    catch { $rejected = $true; if ($mode -eq 'valid') { throw } }
    if ($rejected -ne ($mode -ne 'valid')) { throw "Unexpected signature verification result: $mode" }
}
Write-Output 'Verified trusted timestamped signatures, unsigned/tampered rejection, signer pinning and archive integrity in nine cases.'
