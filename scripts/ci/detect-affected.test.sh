#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DETECT="${ROOT}/scripts/ci/detect-affected.sh"

run_detect() {
  local files=$1
  local out
  out="$(mktemp)"
  GITHUB_OUTPUT="$out" AFFECTED_FILES_OVERRIDE="$files" bash "$DETECT"
  cat "$out"
  rm -f "$out"
}

assert_flag() {
  local output=$1
  local key=$2
  local expected=$3
  if ! echo "$output" | grep -q "^${key}=${expected}$"; then
    echo "FAIL: expected ${key}=${expected}" >&2
    echo "$output" >&2
    exit 1
  fi
}

echo "Scenario 1: apps/customer only"
o="$(run_detect $'apps/customer/src/server.ts\n')"
assert_flag "$o" run_customer true
assert_flag "$o" run_business false
assert_flag "$o" run_admin false
assert_flag "$o" run_backend false

echo "Scenario 2: apps/business only"
o="$(run_detect $'apps/business/src/main.ts\n')"
assert_flag "$o" run_business true
assert_flag "$o" run_customer false
assert_flag "$o" run_admin false

echo "Scenario 3: packages/ui"
o="$(run_detect $'packages/ui/src/foo.ts\n')"
assert_flag "$o" run_customer true
assert_flag "$o" run_business true
assert_flag "$o" run_admin true
assert_flag "$o" run_frontend_packages true

echo "Scenario 4: packages/api-client"
o="$(run_detect $'packages/api-client/src/index.ts\n')"
assert_flag "$o" run_customer true
assert_flag "$o" run_business true
assert_flag "$o" run_admin true

echo "Scenario 5: backend only"
o="$(run_detect $'src/Adeni.Infrastructure/Foo.cs\n')"
assert_flag "$o" run_backend true
assert_flag "$o" run_customer false
assert_flag "$o" run_business false
assert_flag "$o" run_admin false

echo "Scenario 6: db only"
o="$(run_detect $'db/Adeni.Database/Tables/Foo.sql\n')"
assert_flag "$o" run_database true
assert_flag "$o" run_backend true
assert_flag "$o" run_customer false

echo "Scenario 7: customer + business"
o="$(run_detect $'apps/customer/a.ts\napps/business/b.ts\n')"
assert_flag "$o" run_customer true
assert_flag "$o" run_business true
assert_flag "$o" run_admin false

echo "Scenario 8: CI config"
o="$(run_detect $'.github/workflows/ci.yml\n')"
assert_flag "$o" run_ci_all true
assert_flag "$o" run_backend true
assert_flag "$o" run_customer true

echo "Scenario 9: SQL-sensitive infrastructure finance path"
o="$(run_detect $'src/Adeni.Infrastructure/Finance/PaymentOrchestrator.cs\n')"
assert_flag "$o" run_database true
assert_flag "$o" run_backend true

echo "Scenario 10: Application contract path fans out frontend"
o="$(run_detect $'src/Adeni.Application/Finance/IPaymentProvider.cs\n')"
assert_flag "$o" run_customer true
assert_flag "$o" run_business true
assert_flag "$o" run_admin true

echo "All detect-affected scenarios passed."
