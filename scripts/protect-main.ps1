[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository = 'LastResolution/ReStart')

$ErrorActionPreference = 'Stop'
$config = Join-Path (Split-Path -Parent $PSScriptRoot) '.github/main-protection.json'
$expected = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
$response = & gh api --method PUT "repos/$Repository/branches/main/protection" --input $config 2>&1
if ($LASTEXITCODE -ne 0) {
    if (($response -join "`n") -match 'Upgrade to GitHub Pro|HTTP 403') {
        throw 'GitHub rejected main protection. Private repositories require a qualifying plan; main is not protected. Keep the repository private. See https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets'
    }
    throw "GitHub rejected main protection: $($response -join [Environment]::NewLine)"
}
$actual = ($response -join "`n") | ConvertFrom-Json
$missing = @($expected.required_status_checks.contexts | Where-Object { $_ -notin $actual.required_status_checks.contexts })
if ($missing.Count -gt 0 -or -not $actual.required_status_checks.strict -or -not $actual.enforce_admins.enabled -or
    -not $actual.required_pull_request_reviews -or -not $actual.required_linear_history.enabled -or
    -not $actual.required_conversation_resolution.enabled -or $actual.allow_force_pushes.enabled -or $actual.allow_deletions.enabled) {
    throw 'GitHub did not return the expected main protection configuration. Inspect repository settings before relying on it.'
}
Write-Output "Verified main protection for $Repository."
