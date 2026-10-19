[CmdletBinding()]
param([string]$OutputPath = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ReStart/ExampleData/status.json'))

$ErrorActionPreference = 'Stop'
$targetPath = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($targetPath)) | Out-Null
$temporaryPath = $targetPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    # Any application can publish this small JSON shape. No ReStart process API is required.
    $data = @{ text = 'Connected'; detail = [DateTimeOffset]::Now.ToString('T'); progress = 0.75 }
    [IO.File]::WriteAllText($temporaryPath, ($data | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $targetPath -Force
    Write-Output $targetPath
}
finally { if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath } }
