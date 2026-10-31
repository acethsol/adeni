#!/usr/bin/env bash
# Emits GitHub Actions outputs for path-aware CI (see docs/ci.md).
set -euo pipefail

emit() {
  echo "${1}=${2}" >> "${GITHUB_OUTPUT:?GITHUB_OUTPUT is required}"
}

files_match() {
  local files=$1
  shift
  local pattern
  for pattern in "$@"; do
    if echo "$files" | grep -qE "${pattern}"; then
      return 0
    fi
  done
  return 1
}

resolve_base_head() {
  if [[ "${GITHUB_EVENT_NAME:-}" == "pull_request" ]]; then
    BASE="${PR_BASE_SHA:?PR_BASE_SHA required for pull_request}"
    HEAD="${PR_HEAD_SHA:?PR_HEAD_SHA required for pull_request}"
    return
  fi

  BASE="${BEFORE_SHA:-}"
  HEAD="${HEAD_SHA:-HEAD}"
  if [[ -z "$BASE" || "$BASE" == "0000000000000000000000000000000000000000" ]]; then
    if git rev-parse "${HEAD}^" >/dev/null 2>&1; then
      BASE="$(git rev-parse "${HEAD}^")"
    else
      BASE="$(git rev-parse HEAD)"
    fi
  fi
}

set_all_true() {
  emit run_ci_all true
  emit run_backend true
  emit run_database true
  emit run_npm_audit true
  emit run_nuget true
  emit run_customer true
  emit run_business true
  emit run_admin true
  emit run_frontend_packages true
}

if [[ -n "${AFFECTED_FILES_OVERRIDE:-}" ]]; then
  CHANGED="$AFFECTED_FILES_OVERRIDE"
else
  resolve_base_head
  CHANGED="$(git diff --name-only "$BASE" "$HEAD" 2>/dev/null || true)"
fi

if [[ -z "${CHANGED//[$'\t\r\n ']/}" ]]; then
  set_all_true
  exit 0
fi

run_ci_all=false
if files_match "$CHANGED" \
  '^\.github/' \
  '^scripts/ci/' \
  '^scripts/assert-no-vulnerable-nuget\.sh'; then
  run_ci_all=true
fi

run_customer=false
run_business=false
run_admin=false
run_frontend_packages=false
run_backend=false
run_database=false
run_npm_audit=false

if $run_ci_all || files_match "$CHANGED" '^apps/customer/'; then run_customer=true; fi
if $run_ci_all || files_match "$CHANGED" '^apps/business/'; then run_business=true; fi
if $run_ci_all || files_match "$CHANGED" '^apps/admin/'; then run_admin=true; fi

if $run_ci_all || files_match "$CHANGED" \
  '^packages/ui/' \
  '^packages/api-client/' \
  '^packages/brand/' \
  '^packages/shared/'; then
  run_frontend_packages=true
  run_customer=true
  run_business=true
  run_admin=true
fi

if $run_ci_all || files_match "$CHANGED" \
  '^package\.json$' \
  '^package-lock\.json$' \
  '^scripts/strip-nested-ui-angular\.mjs$' \
  '^scripts/sync-brand-assets\.mjs$'; then
  run_npm_audit=true
  run_customer=true
  run_business=true
  run_admin=true
  run_frontend_packages=true
fi

if $run_ci_all || files_match "$CHANGED" \
  '^src/' \
  '^tests/' \
  '^Adeni\.slnx$' \
  '^db/' \
  '^scripts/publish-db\.ps1$' \
  '^packages/shared/src/data/' \
  '^packages/brand/assets/'; then
  run_backend=true
fi

if $run_ci_all || files_match "$CHANGED" \
  '^db/' \
  '^scripts/publish-db\.ps1$' \
  '^packages/shared/src/data/'; then
  run_database=true
fi

run_nuget=false
if $run_backend || $run_ci_all; then run_nuget=true; fi

emit run_ci_all "$run_ci_all"
emit run_backend "$run_backend"
emit run_database "$run_database"
emit run_npm_audit "$run_npm_audit"
emit run_nuget "$run_nuget"
emit run_customer "$run_customer"
emit run_business "$run_business"
emit run_admin "$run_admin"
emit run_frontend_packages "$run_frontend_packages"
