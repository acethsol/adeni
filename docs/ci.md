# Continuous integration

Workflow: [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)  
Runs on pushes and PRs targeting `dev`, `qa`, or `main`.

| Job | What it does |
|-----|----------------|
| **build-and-test** | Restore/build .NET, build `Adeni.Database`, `dotnet test`, **fail** if NuGet reports vulnerable packages |
| **sqlserver** | SQL Server 2022 service → `publish-db.ps1` (SqlPackage) → `Category=SqlServer` tests |
| **frontend** | `npm ci`, workspace typecheck, Vitest/Jasmine for **shared**, **business**, **customer**, and **admin**, Angular production builds (business/admin/customer), **npm audit** (prod high+, all critical) |

## Dependency updates

[`.github/dependabot.yml`](../.github/dependabot.yml) — weekly PRs for NuGet, npm, and GitHub Actions.

Root [`package.json`](../package.json) **overrides** pin transitive fixes for CI `npm audit --omit=dev --audit-level=high` (currently `body-parser@2.4.0`, `zod@4.6.7`; `@adeni/shared` depends on Zod 4). After changing overrides, run `npm install` and re-run the audit commands below.

## Local equivalents

```powershell
dotnet test Adeni.slnx -c Release
bash scripts/assert-no-vulnerable-nuget.sh

docker compose up -d
./scripts/publish-db.ps1 -CreateNewDatabase
$env:ADENI_SQLSERVER_TESTS = "1"
dotnet test tests/Adeni.Infrastructure.Tests --filter "Category=SqlServer"

npm ci
npm run typecheck
npm test --workspace @adeni/shared
npm test --workspace @adeni/business
npm test --workspace @adeni/customer
npm test --workspace @adeni/admin
npm run build:business
npm run build:admin
npm run build:customer
npm audit --omit=dev --audit-level=high
npm audit --audit-level=critical
```

## Branch and environment promotion

`dev` → `qa` → `main`, by reviewed PR at each step:

| Branch | Meaning | Expected environment |
|---|---|---|
| `dev` | Active integration, feature/audit PR target | Development/integration |
| `qa` | Staging promotion from reviewed `dev` | Staging, production-like infrastructure and real selected providers |
| `main` | Production release promoted from validated `qa` | Production, approved immutable release artifact |

No direct production commits, force pushes, or bypass promotions. A production hotfix should use a reviewed PR and be reconciled through the integration/staging branches. This audit does not create branches, merge PRs, configure GitHub protections, or deploy environments.

The checked-in workflow validates changes; **there is no Azure deployment job yet**. Adding `qa` to its push/PR filters extends validation without deployment side effects. The frontend job now also runs the existing Business unit-test script (ChromeHeadless on the hosted Ubuntu image), so API-client compatibility, navigation and realtime teardown regressions run in CI. Future delivery should build a versioned artifact, deploy to staging behind protected environment approvals, run provider/SQL/SSR smoke tests, then promote the same artifact to production with an explicit rollback plan. Environment secrets, origin restrictions and trusted proxy addresses must be configured separately; see [production infrastructure](production-infrastructure.md).

Recommended protections for all three branches: PR-only changes, required approving review, required `build-and-test`, `sqlserver` and `frontend` checks, resolved review threads, stale-review dismissal, restricted force pushes/deletion and administrator bypass. Use CODEOWNERS review for auth, billing, CI and public contract changes where the team supports it. These are recommendations, not a claim that protections are installed.

The `frontend` check remains blocked by existing production stylesheet budgets (see [audit closeout](audit-hardening-closeout.md)). Do not waive it or raise budgets just to promote. Resolve the blocker, obtain green CI including real SQL Server tests, then review the audit PR into `dev`; promote `dev` → `qa` → `main` only after staging validation.
