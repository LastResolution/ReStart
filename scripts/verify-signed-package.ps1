[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$UnsignedPackage,
    [Parameter(Mandatory)][string]$SignedPackage,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$ExpectedThumbprint
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$executables = @('ReStart.exe', 'TileHost/ReStart.TileHost.exe')

function Read-Package([string]$Path) {
    $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Path).Path)
    $files = [Collections.Generic.Dictionary[string, byte[]]]::new([StringComparer]::OrdinalIgnoreCase)
    try {
        foreach ($entry in $archive.Entries) {
            if (-not $entry.Name) { continue }
            if ($entry.FullName -match '\\|^/|:|(^|/)\.\.(/|$)' -or $entry.Length -gt 256MB) { throw 'Unsafe signed archive entry.' }
            if ($files.ContainsKey($entry.FullName)) { throw 'Duplicate signed archive entry.' }
            $stream = $entry.Open(); $buffer = [IO.MemoryStream]::new()
            try { $stream.CopyTo($buffer); $files.Add($entry.FullName, $buffer.ToArray()) }
            finally { $stream.Dispose(); $buffer.Dispose() }
        }
        return ,$files
    }
    finally { $archive.Dispose() }
}

function Get-ContentDigest([byte[]]$Bytes) {
    # Authenticode changes the checksum, security directory and appended
    # certificate table. All remaining bytes, including the .NET bundle, must match.
    if ($Bytes.Length -lt 256 -or [BitConverter]::ToUInt16($Bytes, 0) -ne 0x5a4d) { throw 'Invalid executable header.' }
    $pe = [BitConverter]::ToInt32($Bytes, 60)
    if ($pe -lt 64 -or $pe -gt $Bytes.Length - 256 -or [BitConverter]::ToUInt32($Bytes, $pe) -ne 0x4550) { throw 'Invalid PE header.' }
    $optional = $pe + 24
    $magic = [BitConverter]::ToUInt16($Bytes, $optional)
    $directories = switch ($magic) { 0x10b { 96 } 0x20b { 112 } default { throw 'Unsupported executable format.' } }
    $security = $optional + $directories + 32
    $offset = [BitConverter]::ToUInt32($Bytes, $security)
    $length = [BitConverter]::ToUInt32($Bytes, $security + 4)
    if ($length -eq 0) {
        if ($offset -ne 0) { throw 'Invalid empty certificate table.' }
        $contentLength = $Bytes.Length
    }
    else {
        if ($length -lt 8 -or $offset -lt $security + 8 -or $offset % 8 -ne 0 -or [long]$offset + $length -ne $Bytes.Length) { throw 'Invalid certificate table bounds.' }
        $contentLength = [int]$offset
    }
    $normalized = [byte[]]::new([int]([Math]::Ceiling($contentLength / 8.0) * 8))
    [Array]::Copy($Bytes, $normalized, $contentLength)
    [Array]::Clear($normalized, $optional + 64, 4)
    [Array]::Clear($normalized, $security, 8)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($normalized))
}

$original = Read-Package $UnsignedPackage
$signed = Read-Package $SignedPackage
if ($original.Count -ne $signed.Count -or @($original.Keys | Where-Object { -not $signed.ContainsKey($_) }).Count -gt 0) { throw 'Signing changed the package file list.' }
foreach ($name in $executables) { if (-not $signed.ContainsKey($name)) { throw "Missing signed executable: $name" } }
$inspection = Join-Path $repoRoot ('artifacts/signature-inspection/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $inspection -Force | Out-Null
foreach ($name in $original.Keys) {
    if ($name -in $executables) {
        if ((Get-ContentDigest $original[$name]) -cne (Get-ContentDigest $signed[$name])) { throw "Signing changed executable content: $name" }
        $file = Join-Path $inspection ([IO.Path]::GetFileName($name))
        [IO.File]::WriteAllBytes($file, $signed[$name])
        $signature = Get-AuthenticodeSignature -LiteralPath $file
        if ($signature.Status -ne 'Valid' -or $signature.SignatureType -ne 'Authenticode' -or $null -eq $signature.TimeStamperCertificate -or $signature.SignerCertificate.Thumbprint -ine $ExpectedThumbprint) {
            throw "Missing, invalid, untimestamped or unexpected code signature: $name"
        }
    }
    elseif ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($original[$name])) -cne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($signed[$name]))) {
        throw "Signing changed another package file: $name"
    }
}
& (Join-Path $PSScriptRoot 'check-privacy.ps1') -Package $SignedPackage
Write-Output "Verified $($executables.Count) timestamped code signatures and unchanged package content."
