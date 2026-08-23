# Adeni — Agent Playbook

Project DNA for Cursor Cloud Agents and local Agent sessions. Read this before changing code.

## What we are building

**Adeni** is a trusted local services marketplace API (Lagos, beauty vertical first). Customers discover verified businesses; businesses onboard, get approved, and manage services. Flutter mobile client + this .NET backend.

| Area | Stack |
|------|-------|
| API | .NET 10, ASP.NET Core, Clean Architecture / DDD |
| Data | PostgreSQL (schemas: `identity`, `tenancy`, `admin`), EF Core |
| Cache / locks | Redis (`ICacheService`, `IDistributedLockProvider`) |
| Auth | Auth0 JWT (RS256), custom claims under `https://adeni.io/` |
| Compliance | SOC 2 controls from Sprint 0 — audit logs, PII masking, admin MFA |

## Solution layout

```
src/
├── Adeni.Domain/           # Entities, value objects, Result<T>, audit types
├── Adeni.Application/      # Interfaces, DTOs, PiiMasker, options
├── Adeni.Infrastructure/   # EF, Redis, Auth0, services, migrations
└── Adeni.Api/              # Controllers, middleware, Program.cs
tests/                      # Unit + integration (WebApplicationFactory)
docs/                       # PRD, sprints, Auth0, database, caching
```

**Dependency rule:** Domain → Application ← Infrastructure ← Api. Never reference Infrastructure from Domain.

## Current sprint (Sprint 3)

**Goal:** Auth0 + Flutter shell — real authentication end-to-end.

| Done | In progress / next |
|------|---------------------|
| Sprints 0–2: foundation, onboarding, discovery | Auth0 enabled in staging |
| 88+ tests, CI, Redis cache | Flutter login + auth sync + categories |
| | Sprint 4: booking + Redis slot locks |

Check [docs/sprints.md](docs/sprints.md) before assuming scope.

## Conventions (match existing code)

### Result pattern

Services return `Result` / `Result<T>`. Controllers map with `.Match()` — no thrown exceptions for expected failures.

```csharp
return result.Match<IActionResult>(
    value => Ok(value),
    error => error.Code switch
    {
        "validation" => BadRequest(new { title = error.Message }),
        _ => BadRequest(new { title = error.Message })
    });
```

### Layer placement

| Change type | Where |
|-------------|-------|
| Entity / enum | `Adeni.Domain` |
| Interface + DTO | `Adeni.Application` |
| EF, Redis, Auth0, service impl | `Adeni.Infrastructure` |
| HTTP, middleware | `Adeni.Api` |
| Migrations | `Adeni.Infrastructure/Persistence/Migrations/` |

Register new services in `ServiceCollectionExtensions` (Application + Infrastructure).

### API routes

- Version prefix: `/api/v1/...`
- Admin: `/admin/...` (audit-logged, MFA policy for admins)
- Tenant-scoped routes require `X-Tenant-Id` header + JWT tenant claim match

### Tests

- Run: `dotnet test Adeni.slnx -c Release`
- Integration tests use `WebApplicationFactory<Program>` with environment `Testing`
- Clear Redis/Postgres in test config when using in-memory fallback:
  `ConnectionStrings:AdeniDb` and `Redis:ConnectionString` → empty string
- Name tests `{Method}_{Scenario}_{Expected}`

### Style

- File-scoped namespaces, `sealed` classes where possible
- Primary constructors for DI
- Minimal diffs — no drive-by refactors
- Comments only for non-obvious business or security rules

## Security non-negotiables

Every feature must respect these. They are not optional polish.

1. **Tenant isolation** — `AdeniDbContext` query filters + `TenantAccessMiddleware`. Cross-tenant attempts → 403 + Warning log + audit entry.
2. **PII in logs** — never log full phone/email/body. Use `PiiMasker` and `PiiDestructuringPolicy`.
3. **Admin audit** — all `/admin/*` mutations → `admin.audit_logs` via `AuditMiddleware`.
4. **Admin MFA** — `AdminMfaPolicy` requires `amr: mfa` when `Auth0:RequireMfaForAdmin=true`.
5. **Secrets** — Key Vault in staging/prod; nothing committed. See [docs/auth0-setup.md](docs/auth0-setup.md).
6. **Public endpoints** — discovery and profiles are anonymous; still validate input and cache safely.
7. **Dev-only shortcuts** — `DevBusinessAuthMiddleware` only in Development/Testing. Never in production.

## Production readiness (13-layer lens)

AI gets layers 1–5 (frontend, API, DB, auth, hosting). Adeni backend gaps to close before real users:

| Priority | Layer | Action |
|----------|-------|--------|
| Now | Auth | Enable Auth0 in staging; remove dev auth from customer paths |
| Now | Rate limiting | Add to public endpoints (discovery, profiles, auth/sync) |
| Next | Error tracking | App Insights or Sentry beyond console Serilog |
| Next | CI/CD deploy | Pipeline after green tests |
| Later | Postgres RLS | Defense-in-depth on tenant tables |
| Later | CDN / scale | After Flutter + booking ship |

## Standing plays

Use these prompts in Cursor Agent. One play at a time; finish before switching.

### 1. Spec write

```
Read AGENTS.md and docs/sprints.md. For [FEATURE]:
- Restate goal in one sentence
- List files to touch (Domain → Application → Infrastructure → Api → tests)
- Note tenant/auth/cache/audit implications
- Propose minimal API contract (method, route, request/response)
Do not write code yet.
```

### 2. Implement feature

```
Implement [FEATURE] per the spec. Follow AGENTS.md conventions:
- Result<T> in services, Match in controllers
- Register DI in ServiceCollectionExtensions
- Tenant-scoped data uses ITenantEntity + query filters
- Cache keys follow existing patterns (see CategoryService, DiscoveryService)
Keep the diff focused. No unrelated changes.
```

### 3. Debug loop

```
[Failing test or error]. Investigate:
1. Reproduce with dotnet test [filter]
2. Trace request through middleware pipeline (ApplicationBuilderExtensions)
3. Check tenant header, Auth0 claims, cache key, EF filter
4. Fix root cause with smallest change
5. Re-run full test suite
```

### 4. Security audit

```
Audit [PR/feature] against AGENTS.md security non-negotiables:
- Cross-tenant data paths
- PII in logs or API responses (public profiles must mask phone)
- Admin routes audited
- New public endpoints rate-limit ready
- No secrets or dev middleware in prod paths
List findings as Critical / Should fix / OK.
```

### 5. Test write

```
Add tests for [FEATURE]:
- Unit test service logic with in-memory or mocked deps
- Integration test HTTP contract if new/changed endpoint
- Cover tenant isolation edge case if tenant-scoped
- Cover validation failure path
Run dotnet test Adeni.slnx -c Release and report results.
```

### 6. Review & deploy prep

```
Review this branch before PR:
- dotnet build && dotnet test Adeni.slnx -c Release
- dotnet list Adeni.slnx package --vulnerable
- New migrations included if schema changed
- docs/sprints.md updated if sprint scope changed
Summarize what changed and any deploy/config notes.
```

### 7. Stuck loop escape

```
I'm stuck on [PROBLEM]. Reset approach:
1. State what I expected vs what happened (include test output)
2. List what I already tried
3. Propose two alternatives: minimal fix vs correct long-term fix
4. Recommend one with tradeoffs
Do not rewrite large sections without approval.
```

## Before opening a PR

- [ ] Tests pass locally
- [ ] No PII in logs or new responses without masking
- [ ] Tenant routes tested for cross-tenant denial
- [ ] Admin mutations produce audit records (if applicable)
- [ ] Migration added if schema changed
- [ ] Scope matches current sprint unless user expanded it

## Key docs

| Doc | Purpose |
|-----|---------|
| [README.md](README.md) | Quick start, endpoints |
| [docs/sprints.md](docs/sprints.md) | Sprint scope and status |
| [docs/prd-v1.1-body.md](docs/prd-v1.1-body.md) | NFRs, SOC2 requirements |
| [docs/auth0-setup.md](docs/auth0-setup.md) | Auth0 claims, MFA |
| [docs/database-setup.md](docs/database-setup.md) | Postgres, migrations |
| [docs/caching-setup.md](docs/caching-setup.md) | Redis keys, health |

## Do not

- Skip layers (e.g. controller logic that belongs in a service)
- Bypass `Result<T>` with exceptions for validation
- Add dependencies without CI vulnerability scan passing
- Log raw phone numbers, emails, or message bodies
- Disable tenant filters or audit middleware to "make tests pass"
- Expand scope into booking/payments unless the user or sprint doc says so
