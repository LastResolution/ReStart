[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Executable).Path
$testRoot = Join-Path $repoRoot ('artifacts/file-pinning/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
foreach ($name in @('ReStart.exe', 'ReStart.dll', 'ReStart.Core.dll', 'ReStart.deps.json', 'ReStart.runtimeconfig.json')) {
    $file = Join-Path (Split-Path -Parent $source) $name
    if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination $testRoot }
}
$app = Join-Path $testRoot 'ReStart.exe'
$folder = Join-Path $testRoot 'Folder sample'
$file = Join-Path $testRoot 'Report sample.txt'
$link = Join-Path $testRoot 'Shortcut sample.lnk'
New-Item -ItemType Directory -Path $folder | Out-Null
Set-Content -LiteralPath $file -Value 'Inert pin fixture.'
$shell = New-Object -ComObject WScript.Shell
try { $shortcut = $shell.CreateShortcut($link); $shortcut.TargetPath = $file; $shortcut.Save() }
finally { if ($shortcut) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shortcut) }; [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
$primary = Start-Process -FilePath $app -ArgumentList @('--preview', '--background', '--pin', ('"' + $folder + '"')) -WindowStyle Hidden -PassThru
$layoutPath = Join-Path $testRoot 'preview-data/layout.json'
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (-not (Test-Path -LiteralPath $layoutPath)) {
        if ($primary.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw 'Cold pin did not create a layout.' }
        Start-Sleep -Milliseconds 100
    }
    foreach ($path in @($file, $link, $folder)) {
        $secondary = Start-Process -FilePath $app -ArgumentList @('--preview', '--pin', ('"' + $path + '"')) -WindowStyle Hidden -PassThru
        try {
            if (-not $secondary.WaitForExit(30000)) { $secondary.Kill(); throw 'Pin transfer timed out.' }
            if ($secondary.ExitCode -ne 0) { throw "Pin transfer failed: $($secondary.ExitCode)" }
            if ($primary.HasExited) { throw 'The primary exited during a pin transfer.' }
        }
        finally { $secondary.Dispose() }
    }
    $layout = Get-Content -LiteralPath $layoutPath -Raw | ConvertFrom-Json
    $tiles = @($layout.Groups | ForEach-Object { $_.Tiles } | ForEach-Object { $_; $_.Children })
    foreach ($path in @($folder, $file, $link)) {
        $matches = @($tiles | Where-Object { $_.Target -ceq $path })
        if ($matches.Count -ne 1 -or $matches[0].IsShellApp -or $matches[0].Children.Count -gt 0) { throw 'A filesystem pin lost its target, duplicated, or became a tile folder.' }
        $owner = @($layout.Groups | Where-Object { @($_.Tiles | Where-Object { $_.Target -ceq $path }).Count -gt 0 })
        if ($owner.Count -ne 1 -or -not $owner[0].IsStandalone) { throw 'A default filesystem pin was inserted into an existing group.' }
    }
    $invalid = Start-Process -FilePath $app -ArgumentList @('--preview', '--pin', ('"' + (Join-Path $testRoot 'missing') + '"')) -WindowStyle Hidden -PassThru
    try {
        if (-not $invalid.WaitForExit(30000)) { $invalid.Kill(); throw 'Invalid pin did not return.' }
        if ($invalid.ExitCode -ne 1) { throw 'A missing target was accepted.' }
    }
    finally { $invalid.Dispose() }
    $after = Get-Content -LiteralPath $layoutPath -Raw
    if (($after | ConvertFrom-Json).Groups.Count -ne $layout.Groups.Count) { throw 'A failed pin changed the layout.' }
    Write-Output 'Verified cold folder pin, two transfers, duplicate identity, exact shortcut target, and missing-target rejection.'
    Write-Output $testRoot
}
finally { if (-not $primary.HasExited) { $primary.Kill(); $primary.WaitForExit() }; $primary.Dispose() }
