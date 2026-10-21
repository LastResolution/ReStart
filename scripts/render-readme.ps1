[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$directory = Join-Path $repoRoot ('artifacts/readme-render/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$source = (Resolve-Path -LiteralPath $Executable).Path
$errors = Join-Path $directory 'stderr.txt'
$process = Start-Process -FilePath $source -ArgumentList @('--render-docs', ('"' + $directory + '"')) -WindowStyle Hidden -PassThru -RedirectStandardError $errors
try {
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Documentation rendering timed out.' }
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $errors | Write-Output; throw 'Documentation rendering failed.' }
    foreach ($name in @('launcher-dark.png', 'launcher-light.png', 'motion-frames/open-complete.png')) {
        if (-not (Test-Path -LiteralPath (Join-Path $directory $name))) { throw "Missing documentation image: $name" }
    }
    if (Get-Command ffmpeg -ErrorAction SilentlyContinue) {
        $frames = Join-Path $directory 'motion-frames'
        $sequence = [Collections.Generic.List[string]]::new()
        foreach ($frame in @(Get-ChildItem -LiteralPath $frames -Filter 'open-0*.png' | Sort-Object Name)) {
            $sequence.Add("file 'motion-frames/$($frame.Name)'"); $sequence.Add('option framerate 60'); $sequence.Add('duration 0.016667')
        }
        $complete = 'motion-frames/open-complete.png'
        $sequence.Add("file '$complete'"); $sequence.Add('option framerate 60'); $sequence.Add('duration 1.2')
        foreach ($frame in @(Get-ChildItem -LiteralPath $frames -Filter 'close-*.png' | Sort-Object Name)) {
            $sequence.Add("file 'motion-frames/$($frame.Name)'"); $sequence.Add('option framerate 60'); $sequence.Add('duration 0.016667')
        }
        $last = 'motion-frames/close-010.png'
        $sequence.Add("file '$last'"); $sequence.Add('option framerate 60'); $sequence.Add('duration 0.6'); $sequence.Add("file '$last'"); $sequence.Add('option framerate 60')
        $list = Join-Path $directory 'motion.txt'
        [IO.File]::WriteAllLines($list, $sequence, [Text.UTF8Encoding]::new($false))
        & ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i $list -vf 'scale=1120:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle' -fps_mode vfr -loop 0 (Join-Path $directory 'start-motion.gif')
        if ($LASTEXITCODE -ne 0) { throw 'Documentation animation encoding failed.' }
    }
    Write-Output $directory
}
finally { $process.Dispose() }
