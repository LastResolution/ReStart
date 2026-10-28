# Saves a Home Assistant address + long-lived token for the tablet Home tile, encrypted with
# DPAPI for the current user exactly like HomeAssistantConnection.Save. Must run in the user's
# own session (userrun): DPAPI user keys are not available to SSH key logons. Deletes the input.
param([Parameter(Mandatory)][string]$In, [Parameter(Mandatory)][string]$Url)
Add-Type -AssemblyName System.Security
try {
    $token = (Get-Content -LiteralPath $In -Raw).Trim()
    $json = '{"Url":"' + $Url + '","Token":"' + $token + '"}'
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $entropy = [Text.Encoding]::UTF8.GetBytes('ReStart:Tablet:HomeAssistant:v1')
    $protected = [Security.Cryptography.ProtectedData]::Protect($bytes, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    $dir = Join-Path $env:LOCALAPPDATA 'ReStart\tablet'
    New-Item -ItemType Directory -Force $dir | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $dir 'home-assistant.dat'), $protected)
    Set-Content -LiteralPath "$In.done" 'saved'
} catch { Set-Content -LiteralPath "$In.done" ('failed: ' + $_.Exception.GetType().Name) }
finally { Remove-Item -LiteralPath $In -Force -ErrorAction SilentlyContinue }
