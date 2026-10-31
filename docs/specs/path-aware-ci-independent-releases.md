# Spec: Path-aware CI and independent release units

**Status:** Implemented on `chore/path-aware-ci-independent-releases`  
**Branch base:** `dev` after PR #44 (`d985b20`).

## Goal

Run only relevant CI jobs per change while fanning out when shared packages or CI config change. Preserve monorepo; enable independent Customer / Business / Admin / API / Database release artifacts later.

## Mechanism

- `scripts/ci/detect-affected.sh` — diff base/head (PR base SHA or push `before`/`sha`), emits `run_*` outputs.
- Jobs **always exist**; heavy steps gated with `if:` so required check names stay stable (`build-and-test`, `sqlserver`, `frontend`).
- Per-app jobs: `frontend-customer`, `frontend-business`, `frontend-admin`; **`frontend`** aggregates failures.
- **`npm-audit`** runs when root lockfile/npm scripts change or `run_ci_all`.
- **`ci-complete`** optional aggregate for future branch rules.

## Branch protection

Keep requiring **`build-and-test`**, **`sqlserver`**, **`frontend`**. Skipped paths succeed within those jobs. Optionally require **`ci-complete`** instead of individual legs later.

## Tests

`bash scripts/ci/detect-affected.test.sh` — eight scenarios from the CI audit plan.

## Out of scope

Deploy workflows, Nx/Turborepo, per-file shared ownership map.
