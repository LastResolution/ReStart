[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository = 'LastResolution/ReStart')

$ErrorActionPreference = 'Stop'
$config = Join-Path (Split-Path -Parent $PSScriptRoot) '.github/main-ruleset.json'
$expected = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
$ids = @(& gh api "repos/$Repository/rulesets?per_page=100" --jq '.[] | select(.name == "Protect main" and .source_type == "Repository") | .id' 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Cannot read rulesets: $($ids -join [Environment]::NewLine)" }
$ids = @($ids | Where-Object { "$_" -match '^\d+$' })
if ($ids.Count -gt 1) { throw 'Multiple Protect main rulesets exist; resolve duplicates before applying.' }
$endpoint = "repos/$Repository/rulesets"
$method = 'POST'
if ($ids.Count -eq 1) { $endpoint += '/' + $ids[0]; $method = 'PUT' }
$response = & gh api --method $method $endpoint --input $config -H 'X-GitHub-Api-Version: 2026-03-10' 2>&1
if ($LASTEXITCODE -ne 0) {
    if (($response -join "`n") -match 'Upgrade to GitHub Pro|HTTP 403') {
        throw 'GitHub rejected main protection; main is not protected. Check repository permissions and plan eligibility. See https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets'
    }
    throw "GitHub rejected main protection: $($response -join [Environment]::NewLine)"
}
$actual = ($response -join "`n") | ConvertFrom-Json
$checks = @($actual.rules | Where-Object type -eq 'required_status_checks')[0].parameters
$review = @($actual.rules | Where-Object type -eq 'pull_request')[0].parameters
$required = @($expected.rules | Where-Object type -eq 'required_status_checks')[0].parameters.required_status_checks
$missing = @($required | Where-Object { $wanted = $_; -not @($checks.required_status_checks | Where-Object { $_.context -eq $wanted.context -and $_.integration_id -eq $wanted.integration_id }).Count })
if ($actual.enforcement -ne 'active' -or $actual.target -ne 'branch' -or @($actual.bypass_actors).Count -ne 0 -or
    '~DEFAULT_BRANCH' -notin $actual.conditions.ref_name.include -or $actual.conditions.ref_name.exclude.Count -ne 0 -or
    @($expected.rules.type | Where-Object { $_ -notin $actual.rules.type }).Count -ne 0 -or $missing.Count -ne 0 -or
    -not $checks.strict_required_status_checks_policy -or -not $review.required_review_thread_resolution -or
    $review.required_approving_review_count -ne 0 -or $review.allowed_merge_methods.Count -ne 1 -or $review.allowed_merge_methods[0] -ne 'squash') {
    throw 'GitHub did not return the expected active main ruleset.'
}
$effective = & gh api "repos/$Repository/rules/branches/main?per_page=100"
if ($LASTEXITCODE -ne 0) { throw 'Cannot verify effective main rules.' }
$applied = @($effective | ConvertFrom-Json | Where-Object ruleset_id -eq $actual.id)
if (@($expected.rules.type | Where-Object { $_ -notin $applied.type }).Count -ne 0) { throw 'Saved rules are not effective on main.' }
Write-Output "Verified active main ruleset: https://github.com/$Repository/rules/$($actual.id)"
