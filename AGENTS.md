# Adeni — Agent Playbook

Project DNA for Cursor Cloud Agents and local Agent sessions. Read this before changing code.

## What we are building

**Adeni** is the operating and transaction layer for local service businesses — discovery, trust, booking, and business tooling. **Lagos first**, beauty & grooming wedge, supply-first GTM.

| Area | Stack |
|------|-------|
| API | .NET 10, modular monolith, Clean Architecture / DDD |
| Data | PostgreSQL, EF Core, Redis (cache + slot locks) |
| Auth | Auth0 JWT (RS256), claims under `https://adeni.io/` |
| Web | Next.js (`apps/web`) |
| Mobile | Expo / React Native (`apps/mobile`) |
| Shared contracts | `packages/shared`, `packages/api-client` |
| Compliance | SOC 2 from Sprint 0 — audit logs, PII masking, admin MFA |

**Strategy (read before large features):** [docs/product-strategy.md](docs/product-strategy.md)  
**Architecture (module boundaries):** [docs/architecture.md](docs/architecture.md)

## Solution layout

```
src/
├── Adeni.Domain/           # Entities, value objects, Result<T>, domain events
├── Adeni.Application/      # Module interfaces (I*Service), DTOs
├── Adeni.Infrastructure/   # EF, Redis, Auth0, module implementations
└── Adeni.Api/              # Controllers, middleware, composition root
apps/
├── web/                    # Next.js — discovery, portals, admin
└── mobile/                 # Expo — customer + business flows
packages/
├── shared/                 # Zod schemas, capabilities, business types
└── api-client/             # Typed HTTP client
tests/                      # Unit + integration + architecture tests
docs/                       # Strategy, sprints, specs, runbooks
```

**Dependency rule:** Domain → Application ← Infrastructure ← Api. Modules talk via `I*Service` only — see [architecture.md](docs/architecture.md).

## Current sprint

Check [docs/sprints.md](docs/sprints.md) for live status. As of last playbook update:

| | |
|---|---|
| **Done through** | Sprint 17 — Commerce orchestration (Paystack) |
| **Next up** | Sprint 18 — Messaging & WhatsApp bridge |
| **Then** | Sprint 19 trust/quote depth, Sprint 20 deploy + AI + observability |

Do not start work from a later sprint unless the user or sprint doc explicitly expands scope.

## v1 scope (agent guardrails)

Summarized from [product-strategy.md §2–3, §8](docs/product-strategy.md). When in doubt, stay **in** scope.

### In scope (now)

- Lagos market; beauty & grooming wedge (barbers, salons, nails, makeup)
- Supply-first: verified businesses, public profiles, shareable booking links
- Discovery + geo search + reviews tied to completed bookings
- Booking (scheduled appointments); business portal + customer my-bookings
- Business types foundation: `scheduled_appointment` (+ quote flow when Sprint 19 lands)
- Auth0 end-to-end; tenant isolation; admin verification queue
- SaaS tiers / entitlements (Sprint 16); Paystack orchestration (Sprint 17) — no custody
- Modular monolith; one API deployable

### Out of scope (do not build unless asked)

- Wallet / holding customer or business funds
- Full CRM, ERP, inventory, POS, payroll
- Pay-to-verify badges
- Competing with Google/Yelp on directory size
- All service categories or cities at once
- Microservices or multi-repo backend before module boundaries are enforced
- In-app messaging before Sprint 18; LLM agents before Sprint 20
- Production deploy / full observability stack before Sprint 20 (staging work is OK)

## AI calibration (voice correction)

When the user or spec says these terms, interpret **Adeni's way** — not generic AI defaults:

| You hear | Do this |
|----------|---------|
| **Booking** | Use existing booking module + Redis slot locks (`IDistributedLockProvider`); respect business type / capabilities |
| **Public profile** | Mask phone in API responses; cache key `tenant:{id}:profile`; only `Verified` tenants in discovery |
| **New backend feature** | Application port (`I*Service`) first; module folder in Domain; register in `ServiceCollectionExtensions`; no cross-Infrastructure imports |
| **Frontend change** | Branch UI on `businessType` / capabilities from API — not hard-coded category slugs; update `packages/shared` if contract changes |
| **Payment / Paystack** | Orchestration only — `IPaymentProvider` port, no wallet/balance tables; see product-strategy §4.3 |
| **Webhook / payment event** | Verify provider signature **before** parsing JSON; idempotent handler; never mark paid on body alone — see [Webhook security (Murphy)](#webhook-security-murphy) |
| **New API endpoint / contract change** | `/api/v1/` only; auth on mutations; stable `code` errors; update `packages/shared` — see [API contract hardening (Murphy)](#api-contract-hardening-murphy) |
| **Quote request** | `quote_request` business type — not the default barber/salon flow; Sprint 19 depth |
| **Admin action** | Must audit; admin JWT needs MFA when policy enabled |
| **Cache** | Follow existing key patterns; invalidate on write; see [caching-setup.md](docs/caching-setup.md) |
| **Spec / feature** | Write `docs/specs/sprint-XX-name.md` from [template](docs/specs/_template.md) before coding |
| **AI agent / Ask Adeni** | Tool-calling against Adeni APIs only — never direct DB access; Sprint 20 |
| **Quick fix** | Minimal diff; match surrounding code; run tests — no drive-by refactors |

Add new rows here when the team corrects the agent twice on the same mistake.

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
| Entity / enum / domain event | `Adeni.Domain/{Module}/` |
| Interface + DTO | `Adeni.Application` |
| EF, Redis, Auth0, service impl | `Adeni.Infrastructure/{Module}/` |
| HTTP, middleware | `Adeni.Api` |
| Migrations | `Adeni.Infrastructure/Persistence/Migrations/` |
| Shared API types | `packages/shared` |

Register new services in `ServiceCollectionExtensions`. New modules: follow [architecture.md §4.4](docs/architecture.md).

### API routes

- Version prefix: `/api/v1/...`
- Admin: `/admin/...` (audit-logged, MFA policy for admins)
- Tenant-scoped routes require `X-Tenant-Id` header + JWT tenant claim match

### Tests

- Run: `dotnet test Adeni.slnx -c Release`
- Integration tests: `WebApplicationFactory<Program>`, environment `Testing`
- In-memory fallback: empty `ConnectionStrings:AdeniDb` and `Redis:ConnectionString` in test config
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
5. **Secrets** — Key Vault in staging/prod; nothing committed. See [auth0-setup.md](docs/auth0-setup.md).
6. **Public endpoints** — discovery and profiles are anonymous; still validate input and cache safely.
7. **Dev-only shortcuts** — `DevBusinessAuthMiddleware` only in Development/Testing. Never in production.
8. **Payment webhooks** — verify provider signature before parsing; idempotent state transitions; rate-limit public webhook routes in staging/prod. See [Webhook security (Murphy)](#webhook-security-murphy).
9. **API contract** — version all routes under `/api/v1/`; authenticate mutating endpoints; stable error codes; plan deprecation before breaking changes. See [API contract hardening (Murphy)](#api-contract-hardening-murphy).

## Webhook security (Murphy)

Source: [@mattmurphyai — payment webhook reel](https://www.instagram.com/reel/DcbzcfyioD9/) (Stripe example; same rules for Paystack).

**The failure mode:** Accepting webhooks without signature verification lets anyone POST a fake “payment succeeded” payload. Your server marks the order paid and triggers fulfillment — no real money moved.

**Required for every payment/subscription webhook endpoint:**

| Rule | Adeni implementation |
|------|------------------------|
| **1. Signature verification** | Paystack: HMAC SHA512 of **raw body** vs `x-paystack-signature` in `PaystackPaymentProvider.ParseWebhookAsync`. Reject with `payment.webhook_invalid` if missing or wrong. **`Paystack:WebhookSecret` required in staging/prod.** |
| **2. Idempotency** | `PaymentOrchestrator.ApplySuccessAsync` skips if status already `Completed`. Store `providerReference`; ignore duplicate success events. |
| **3. Never trust body alone** | Resolve `PaymentIntentRecord` by provider reference; transition only through orchestrator + domain events (`PaymentCompleted`). |
| **4. Endpoint protection** | Webhook routes have no JWT — **rate-limit** at edge/API (Sprint 20 / infra). Do not expose stub confirm in production. |
| **5. Minimal logging** | Audit `payment.webhook_received`; log event type + intent id — not full payload (PII). |

**Reference implementation:** `src/Adeni.Infrastructure/Payments/PaystackPaymentProvider.cs`, `PaymentOrchestrator.ProcessWebhookAsync`, [docs/payments.md](docs/payments.md).

**Known gap:** `POST /api/v1/subscriptions/webhook` is still a **stub without signature verification**. When wiring Paystack Subscriptions, reuse the same Murphy rules — do not copy the stub pattern.

**Security audit (play 4) must include:** Any new webhook endpoint checked against this table.

## API contract hardening (Murphy)

Source: [@mattmurphyai — API security reel](https://www.instagram.com/reel/DcYrFHSAOhJ/) — “Your API is your contract. Harden it.”

**The failure mode:** A forged POST/PATCH/DELETE hits your API with no proof of identity. The server mutates data — bookings, payments, tenant settings — with no questions asked.

**Murphy’s three fixes:**

1. **Request signing on every mutating endpoint** — caller proves the request is legitimate  
2. **API versioning from day one** — never break clients silently  
3. **Sunset headers + deprecation monitoring** — clients get machine-readable migration paths  

### Adeni compliance matrix

| Rule | Adeni today | Agent requirement |
|------|-------------|-------------------|
| **Version prefix** | All routes under `/api/v1/...`; clients use `packages/api-client` | New endpoints **must** use `/api/v1/`. v2 = new prefix, not silent breaks. |
| **Stable error contract** | RFC 7807 + dotted `code` + i18n ([api-errors.md](docs/api-errors.md)) | New errors via `ErrorCodes` + `packages/shared` locales — never English-only UI strings. |
| **Auth on mutations (app clients)** | Auth0 JWT (RS256) + roles; tenant routes need `X-Tenant-Id` + claim match | POST/PATCH/DELETE on tenant/business/admin data **require** JWT unless explicitly public-by-design. |
| **Webhook / provider signing** | Paystack HMAC on payment webhooks | See [Webhook security (Murphy)](#webhook-security-murphy). |
| **Idempotency on writes** | Payment intents + webhook replay protection | Add `Idempotency-Key` support for booking create and payment initialize when touching those flows. |
| **Anonymous mutations** | Some public POSTs (bookings, discovery-adjacent) | Document in spec; rate-limit; validate strictly; prefer auth where possible. |
| **Correlation** | `X-Correlation-Id` on every request/response | Preserve in clients; include in logs and Sentry/App Insights (Sprint 20). |
| **Sunset / Deprecation headers** | Not implemented yet | Before removing or breaking v1 fields: add `Deprecation`, `Sunset`, `Link: rel="successor-version"` per [api-errors.md § Versioning](docs/api-errors.md#api-versioning--deprecation-murphy). |
| **OpenAPI** | Scalar/OpenAPI in Development | Update when adding or changing public contract. |

### What “request signing” means for Adeni

Murphy’s “signing” is **not** duplicate HMAC on every browser call when Auth0 JWT already proves identity. Use the right trust layer per surface:

| Surface | Trust mechanism |
|---------|-----------------|
| Web / mobile (logged in) | Auth0 JWT + tenant header where scoped |
| Admin | JWT + MFA policy + audit |
| Payment webhooks | Provider HMAC on raw body |
| Future partner API | API key + HMAC or mTLS (backlog) |
| Anonymous public POST | Validation + rate limits + idempotency key — **weakest**; minimize scope |

### When changing the API contract

1. Add/update Zod schemas in `packages/shared` and `api-client` methods  
2. Add `ErrorCodes` entry if new failure modes  
3. If deprecating: response headers + changelog note in spec — do **not** remove fields without sunset period  
4. Integration test the HTTP contract  
5. Security audit (play 4) checks auth on new mutating routes  

**Known gaps:**

- No `Deprecation` / `Sunset` response headers yet  
- `Idempotency-Key` not universal on booking/payment POSTs  
- Anonymous mutating routes need rate limiting (Sprint 20 / infra)  
- `POST /api/v1/subscriptions/webhook` unsigned (see webhook section)  

**Reference:** [docs/api-errors.md](docs/api-errors.md), `ApiErrorResponseMapper`, `CorrelationIdMiddleware`.

## Production readiness (13-layer lens)

AI gets layers 1–5 (frontend, API, DB, auth, hosting). Gaps to close before real users — full plan in Sprint 20:

| Priority | Layer | Action |
|----------|-------|--------|
| Now | Auth | Auth0 on staging; no dev auth on customer paths |
| Now | Rate limiting | Public endpoints (discovery, profiles, auth/sync) |
| Sprint 20 | Error tracking | App Insights + Sentry per [observability.md](docs/observability.md) |
| Sprint 20 | CI/CD deploy | Staging → production pipeline |
| Later | Postgres RLS | Defense-in-depth on tenant tables |
| Later | CDN / scale | After core loop proven in Lagos |

## Standing plays

Use these prompts in Cursor Agent. One play at a time; finish before switching.

### 1. Spec write

```
Read AGENTS.md, docs/sprints.md, and docs/product-strategy.md (if strategic).
For [FEATURE]:
1. Copy docs/specs/_template.md → docs/specs/sprint-XX-[slug].md and fill it in
2. Restate goal in one sentence; list in-scope and out-of-scope
3. List files to touch (Domain → Application → Infrastructure → Api → tests → packages/shared)
4. Note tenant/auth/cache/audit/PII implications
5. Propose minimal API contract
Do not write code yet.
```

### 2. Implement feature

```
Implement [FEATURE] per docs/specs/sprint-XX-[slug].md. Follow AGENTS.md:
- Result<T> in services, Match in controllers
- Module boundaries per architecture.md
- Tenant-scoped data uses ITenantEntity + query filters
- Cache keys follow existing patterns
- Update packages/shared if API contract changed
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
Audit [PR/feature] against AGENTS.md security non-negotiables and v1 scope:
- Cross-tenant data paths
- PII in logs or API responses (public profiles must mask phone)
- Admin routes audited
- New public endpoints rate-limit ready
- Payment/subscription webhooks: signature + idempotency per [Webhook security (Murphy)](#webhook-security-murphy)
- New/changed API routes: auth on mutations, `/api/v1/`, shared schemas per [API contract hardening (Murphy)](#api-contract-hardening-murphy)
- No secrets or dev middleware in prod paths
- No out-of-scope features (payments custody, microservices, etc.)
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
- docs/sprints.md or spec status updated if scope changed
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

- [ ] Spec exists in `docs/specs/` (unless trivial fix)
- [ ] Tests pass locally
- [ ] No PII in logs or new responses without masking
- [ ] Tenant routes tested for cross-tenant denial
- [ ] Admin mutations produce audit records (if applicable)
- [ ] Migration added if schema changed
- [ ] Scope matches current sprint and v1 guardrails

## Key docs

| Doc | Purpose |
|-----|---------|
| [README.md](README.md) | Quick start, endpoints |
| [docs/product-strategy.md](docs/product-strategy.md) | Positioning, revenue, what NOT to build |
| [docs/architecture.md](docs/architecture.md) | Modular monolith, module map, new-feature checklist |
| [docs/sprints.md](docs/sprints.md) | Sprint scope and status |
| [docs/specs/](docs/specs/) | Feature specs (write before code) |
| [docs/prd-v1.1-body.md](docs/prd-v1.1-body.md) | NFRs, SOC2 requirements |
| [docs/api-errors.md](docs/api-errors.md) | RFC 7807 errors, versioning & deprecation policy |
| [docs/observability.md](docs/observability.md) | App Insights + Sentry, correlation IDs |
| [docs/auth0-setup.md](docs/auth0-setup.md) | Auth0 claims, MFA |
| [docs/database-setup.md](docs/database-setup.md) | Postgres, migrations |
| [docs/caching-setup.md](docs/caching-setup.md) | Redis keys, health |
| [docs/payments.md](docs/payments.md) | Paystack orchestration, webhooks, deposits |
| [docs/legal-launch-checklist.md](docs/legal-launch-checklist.md) | Privacy, Terms, NDPR launch gate (pre-production) |
| [docs/tenant-isolation.md](docs/tenant-isolation.md) | Tenant middleware and filters |

## Do not

- Skip layers (e.g. controller logic that belongs in a service)
- Bypass `Result<T>` with exceptions for validation
- Add dependencies without CI vulnerability scan passing
- Log raw phone numbers, emails, or message bodies
- Disable tenant filters or audit middleware to "make tests pass"
- Hard-code category checks in UI — use capabilities / businessType from API
- Accept payment webhooks without signature verification or idempotent handling
- Add mutating API routes without auth (unless documented public-by-design in spec)
- Break `/api/v1/` clients silently — use deprecation headers before removal
- Ship subscription webhooks without the same Murphy rules as payments
- Build Sprint 18+ features (messaging, LLM agents) unless explicitly requested
- Introduce microservices or wallet/custody tables
