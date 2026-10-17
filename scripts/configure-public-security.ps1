[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository = 'LastResolution/ReStart')

$ErrorActionPreference = 'Stop'
$metadata = & gh api "repos/$Repository"
if ($LASTEXITCODE -ne 0) { throw 'Cannot read repository metadata.' }
if (($metadata | ConvertFrom-Json).private) { throw 'Apply public security settings after repository publication.' }
foreach ($feature in @('private-vulnerability-reporting', 'vulnerability-alerts', 'automated-security-fixes')) {
    & gh api --method PUT "repos/$Repository/$feature"
    if ($LASTEXITCODE -ne 0) { throw "Cannot enable $feature." }
}
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('ReStart-security-' + [Guid]::NewGuid().ToString('N') + '.json')
try {
    $payload = @{ security_and_analysis = @{ secret_scanning = @{ status = 'enabled' }; secret_scanning_push_protection = @{ status = 'enabled' } } }
    [IO.File]::WriteAllText($temporary, ($payload | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    & gh api --method PATCH "repos/$Repository" --input $temporary | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enable secret scanning and push protection.' }
    $codeql = @{ state = 'configured'; languages = @('csharp', 'javascript-typescript'); query_suite = 'default' }
    [IO.File]::WriteAllText($temporary, ($codeql | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    & gh api --method PATCH "repos/$Repository/code-scanning/default-setup" --input $temporary
    if ($LASTEXITCODE -ne 0) { throw 'Cannot configure CodeQL default setup.' }
}
finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
$reporting = & gh api "repos/$Repository/private-vulnerability-reporting"
if ($LASTEXITCODE -ne 0 -or -not ($reporting | ConvertFrom-Json).enabled) { throw 'Private vulnerability reporting was not enabled.' }
$fixes = & gh api "repos/$Repository/automated-security-fixes"
if ($LASTEXITCODE -ne 0 -or -not ($fixes | ConvertFrom-Json).enabled) { throw 'Dependabot security updates were not enabled.' }
& gh api "repos/$Repository/vulnerability-alerts" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Dependabot alerts were not enabled.' }
$repositoryState = (& gh api "repos/$Repository" | ConvertFrom-Json)
if ($LASTEXITCODE -ne 0 -or $repositoryState.security_and_analysis.secret_scanning.status -ne 'enabled' -or
    $repositoryState.security_and_analysis.secret_scanning_push_protection.status -ne 'enabled') { throw 'Secret scanning protections are not enabled.' }
Write-Output 'Verified private reporting, Dependabot alerts/security updates, secret scanning and push protection. CodeQL setup was requested; inspect its run and alerts separately.'
