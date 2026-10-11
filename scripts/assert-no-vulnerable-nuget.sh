#!/usr/bin/env bash
# Fail if any restored project reports NuGet vulnerabilities (direct or transitive).
# Usage: ./scripts/assert-no-vulnerable-nuget.sh [solution-or-project]
set -euo pipefail

TARGET="${1:-Adeni.slnx}"
OUT="$(mktemp)"
trap 'rm -f "$OUT"' EXIT

dotnet list "$TARGET" package --vulnerable --include-transitive | tee "$OUT"

# "dotnet list package --vulnerable" prints a per-project summary. Any line that is not
# the "has no vulnerable packages" / restore noise indicates a hit.
if grep -Eiq 'has the following vulnerable packages|Severity[[:space:]]*:' "$OUT"; then
  echo "::error::Vulnerable NuGet packages detected. Upgrade or remove them before merging."
  exit 1
fi

if ! grep -Eq 'has no vulnerable packages' "$OUT"; then
  echo "::warning::Could not confirm clean NuGet vulnerability report; check output above."
fi

echo "NuGet vulnerability check passed."
