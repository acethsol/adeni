# Creates or updates the "Dev — require CI" repository ruleset (merge blocked until ci-complete passes).
# Requires: gh CLI, repo admin, authenticated as acethsol/adeni maintainer.
# Usage: pwsh scripts/github/apply-dev-ruleset.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$payloadPath = Join-Path $repoRoot ".github/rulesets/dev-require-ci.json"

if (-not (Test-Path $payloadPath)) {
  throw "Missing ruleset payload: $payloadPath"
}

$payload = Get-Content -Raw $payloadPath | ConvertFrom-Json
$name = $payload.name

$existing = gh api "repos/acethsol/adeni/rulesets" --jq ".[] | select(.name==`"$name`") | .id" 2>$null
if ($LASTEXITCODE -ne 0) { $existing = $null }

if ($existing) {
  Write-Host "Updating ruleset id $existing ..."
  gh api -X PUT "repos/acethsol/adeni/rulesets/$existing" --input $payloadPath | Out-Null
} else {
  Write-Host "Creating ruleset ..."
  gh api -X POST repos/acethsol/adeni/rulesets --input $payloadPath | Out-Null
}

Write-Host "Done. PRs into dev require check: ci-complete (strict / up-to-date)."
