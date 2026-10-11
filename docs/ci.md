# Continuous integration

Workflow: [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)  
Runs on pushes and PRs targeting `main` or `dev`.

| Job | What it does |
|-----|----------------|
| **build-and-test** | Restore/build .NET, build `Adeni.Database`, `dotnet test`, **fail** if NuGet reports vulnerable packages |
| **sqlserver** | SQL Server 2022 service → `publish-db.ps1` (SqlPackage) → `Category=SqlServer` tests |
| **frontend** | `npm ci`, workspace typecheck, `@adeni/shared` Vitest, Angular production builds (portal/admin/discover), **npm audit** (prod high+, all critical) |

## Dependency updates

[`.github/dependabot.yml`](../.github/dependabot.yml) — weekly PRs for NuGet, npm, and GitHub Actions.

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
npm run build:portal
npm audit --omit=dev --audit-level=high
npm audit --audit-level=critical
```
