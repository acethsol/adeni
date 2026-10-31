# Continuous integration

Workflow: [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)  
Runs on pushes and PRs targeting `dev`, `qa`, or `main`.

## Path-aware jobs

[`scripts/ci/detect-affected.sh`](../scripts/ci/detect-affected.sh) compares the PR base SHA (or push `before` → `sha`) to changed paths and sets `run_*` flags. Jobs **always run** so branch protection check names stay stable; heavy steps are skipped when unaffected (job still succeeds).

| Job | Runs heavy work when |
|-----|----------------------|
| **detect-changes** | Always (outputs flags + self-test) |
| **build-and-test** | `src/`, `tests/`, `db/` (build slice), `Adeni.slnx`, shared catalog JSON, brand assets, `.github/` / CI scripts |
| **sqlserver** | `db/`, `publish-db.ps1`, `packages/shared/src/data/` |
| **npm-audit** | Root `package.json` / lockfile, brand sync scripts, or CI-all |
| **frontend-customer** | `apps/customer/` or shared frontend packages |
| **frontend-business** | `apps/business/` or shared frontend packages |
| **frontend-admin** | `apps/admin/` or shared frontend packages |
| **frontend** | Aggregates the three app jobs (required check name unchanged) |
| **ci-complete** | Fails if any required leg failed |

Shared package paths (`packages/ui`, `packages/shared`, `packages/api-client`, `packages/brand`) fan out to **all three** Angular apps. Backend-only changes do **not** build Angular apps.

Validate rules locally:

```bash
bash scripts/ci/detect-affected.test.sh
```

## Dependency graph (frontend)

```text
apps/customer|business|admin
  → @adeni/ui → @adeni/shared, @adeni/brand
  → @adeni/api-client → @adeni/shared
  → @adeni/brand
```

Backend copies selected files from `packages/shared/src/data/` and `packages/brand/assets/` (see `Adeni.Api.csproj`).

## Dependency updates

[`.github/dependabot.yml`](../.github/dependabot.yml) — weekly PRs for NuGet, npm, and GitHub Actions.

Root [`package.json`](../package.json) **overrides** pin transitive fixes for CI `npm audit --omit=dev --audit-level=high`.

## Local equivalents

**Full matrix (same as CI-all):**

```powershell
dotnet test Adeni.slnx -c Release
bash scripts/assert-no-vulnerable-nuget.sh

docker compose up -d
./scripts/publish-db.ps1 -CreateNewDatabase
$env:ADENI_SQLSERVER_TESTS = "1"
dotnet test tests/Adeni.Infrastructure.Tests --filter "Category=SqlServer"

npm ci
npm run typecheck --workspace @adeni/customer   # or business/admin only
npm test --workspace @adeni/shared
npm test --workspace @adeni/customer
npm run build:customer
npm audit --omit=dev --audit-level=high
```

**Customer-only example:** run customer test/build/typecheck; skip business/admin builds unless shared packages changed.

## Branch and environment promotion

`dev` → `qa` → `main`, by reviewed PR at each step:

| Branch | Meaning | Expected environment |
|---|---|---|
| `dev` | Active integration | Development/integration |
| `qa` | Staging promotion | Staging |
| `main` | Production release | Production artifact by commit SHA |

No deployment workflow is checked in yet. Independent release units (target): Customer Web, Business Web, Admin Web, Adeni API, Database — each promotable by artifact SHA without shipping unrelated apps.

## Branch protection

Recommended required checks: **`build-and-test`**, **`sqlserver`**, **`frontend`**. Optional: **`ci-complete`**. Per-app jobs (`frontend-customer`, etc.) are for parallelism and do not need to be required if `frontend` aggregate is required.

When `.github/workflows/**` changes, detection sets **CI-all** and runs the full matrix.

Spec: [path-aware-ci-independent-releases.md](specs/path-aware-ci-independent-releases.md).
