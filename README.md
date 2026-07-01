# Adeni Backend

Trusted local services marketplace API — **.NET 10**, **DDD/Clean Architecture**, **SOC 2 controls built in from Sprint 0**.

## Quick start

```powershell
cd C:\DEV\Aceth\adeni
docker compose up -d          # PostgreSQL
dotnet restore Adeni.slnx
dotnet test Adeni.slnx -c Release
dotnet run --project src/Adeni.Api
```

Health: `GET http://localhost:5xxx/health`

## Solution structure

```
src/
├── Adeni.Domain/           # Entities, Result<T>, value objects
├── Adeni.Application/      # Abstractions, PiiMasker, Auth0 options
├── Adeni.Infrastructure/   # EF Core, Auth0 JWT, Key Vault, audit persistence
└── Adeni.Api/              # Middleware pipeline, controllers
tests/                      # 77+ unit/integration tests
```

## M0 features (current)

| Feature | Implementation |
|---------|----------------|
| Auth0 JWT | `AddAdeniAuth()` — RS256, audience/issuer validation |
| Admin MFA policy | `AdminMfaPolicy` requires `amr: mfa` claim |
| PostgreSQL + EF Core | `AdeniDbContext`, schemas `identity`, `tenancy`, `admin` |
| Tenant global filters | `TenantMatches()` on `BusinessUser`, `Tenant` |
| Cross-tenant guard | `TenantAccessMiddleware` + audit on 403 |
| Audit persistence | `EfAuditLogWriter` → `admin.audit_logs` |
| Key Vault | Skipped in Development/Testing; required in staging/prod |

## Configuration

**Development** (`appsettings.Development.json`):

```json
{
  "ConnectionStrings": { "AdeniDb": "Host=localhost;..." },
  "Auth0": { "Enabled": false }
}
```

Set `Auth0:Enabled=true` when testing Auth0 integration. See [docs/auth0-setup.md](docs/auth0-setup.md).

## Database migrations

```powershell
dotnet ef database update --project src/Adeni.Infrastructure --startup-project src/Adeni.Api
```

## Compliance docs (Confluence)

- [SOC 2 Compliance Framework](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247170)
- [MFA Enforcement Runbook](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247205)
- [Privacy Policy Legal Review Checklist](https://aceth.atlassian.net/wiki/spaces/SD/pages/26738699)

## Remote

Target GitHub org: **acethsol** (same as careaxis-api, aceth-web).

```powershell
git remote add origin https://github.com/acethsol/adeni.git
git push -u origin main
```

Create the empty repo at https://github.com/organizations/acethsol/repositories/new if it does not exist.
