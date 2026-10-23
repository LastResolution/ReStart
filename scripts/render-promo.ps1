[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable, [ValidateSet('Dark', 'Light')][string]$Theme = 'Dark')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$encoder = (Get-Command ffmpeg -ErrorAction Stop).Source
$probe = (Get-Command ffprobe -ErrorAction Stop).Source
$directory = Join-Path $repoRoot ('artifacts/promo/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$source = (Resolve-Path -LiteralPath $Executable).Path
$errors = Join-Path $directory 'render.log'
$renderArguments = @('--render-promo', ('"' + $directory + '"'), ('"' + $encoder + '"'))
if ($Theme -eq 'Light') { $renderArguments += '--light' }
$process = Start-Process -FilePath $source -ArgumentList $renderArguments -WindowStyle Hidden -PassThru -RedirectStandardError $errors
try {
    if (-not $process.WaitForExit(900000)) { $process.Kill(); throw 'Product video rendering timed out.' }
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $errors | Write-Output; throw 'Product video rendering failed.' }
    $movie = Join-Path $directory 'ReStart-demo.mp4'
    $metadata = (& $probe -v error -show_streams -show_format -of json $movie | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the encoded demonstration.' }
    $video = @($metadata.streams | Where-Object codec_type -eq video)
    if ($video.Count -ne 1 -or $video[0].codec_name -ne 'h264' -or $video[0].width -ne 1920 -or $video[0].height -ne 1080 -or
        $video[0].pix_fmt -ne 'yuv420p' -or [Math]::Abs([double]$metadata.format.duration - 60) -gt 0.05 -or (Get-Item -LiteralPath $movie).Length -ge 10000000) {
        throw 'The movie does not meet the GitHub playback/attachment contract.'
    }
    & $encoder -hide_banner -loglevel error -i $movie -f null -
    if ($LASTEXITCODE -ne 0) { throw 'The movie did not decode completely.' }
    & $encoder -hide_banner -loglevel error -y -ss 3.5 -t 5.5 -i $movie -vf 'fps=10,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle' -loop 0 (Join-Path $directory 'promo-preview.gif')
    if ($LASTEXITCODE -ne 0) { throw 'Preview animation encoding failed.' }
    Write-Output $directory
}
finally { $process.Dispose() }
