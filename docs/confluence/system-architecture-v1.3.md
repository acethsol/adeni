# Adeni — System Architecture (v1.3)

> **Status:** Engineering blueprint | **Version:** 1.3 | **Parent:** [Adeni Product Bible](https://aceth.atlassian.net/wiki/spaces/SD/pages/26279937)  
> **Supersedes:** [System Architecture v1.2](./system-architecture-v1.2.md) for **stack and client topology** (v1.2 C4 narrative remains useful historically).

> **v1.3 change (October 2026):** Primary database is **SQL Server** (Database Project + EF Core). Public and staff web clients are **three Angular apps** (`discover`, `portal`, `admin`). Legacy Next.js/Expo archived. Consumer mobile target is **Flutter** after web GA (ADR-012). Full per-module docs: [Technical Documentation index](./technical-documentation-index.md).

---

## 1. Purpose & scope

Defines **how Adeni is built**: boundaries, multi-tenant enforcement, modular monolith layout, client surfaces, integrations, deployment, and cross-cutting concerns.

**In scope:** Angular discover (SSR), portal, admin; Flutter business skeleton; .NET API; SQL Server; Redis; Auth0; Azure Blob; booking, queue, payments, subscriptions, messaging, trust.

**Out of scope (early):** Microservices deployment, multi-region active-active, customer fund custody, LLM direct DB access.

---

## 2. Architecture principles

| # | Principle | Implication |
| --- | --- | --- |
| 1 | Tenant isolation by default | `ITenantEntity` + EF filters + `TenantAccessMiddleware` |
| 2 | Clean Architecture + vertical modules | Domain / Application / Infrastructure / Api + Booking, Tenancy, … |
| 3 | Modular monolith | Single `Adeni.Api` process; extract when criteria met |
| 4 | API-first | All business rules in backend modules; clients use `@adeni/api-client` |
| 5 | Web-first discovery, mobile-first ops (target) | Angular SSR for SEO; Flutter for business floor |
| 6 | Fail closed on auth | Missing/invalid JWT → 401; cross-tenant → 403 |
| 7 | Domain events for side effects | In-process dispatcher; handlers in Notifications, Payments, etc. |
| 8 | Market-aware, not hard-coded locale | Markets catalog drives currency, payment provider defaults |

---

## 3. System context (C4 Level 1)

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Customer       │     │  Business       │     │   Admin         │
│  (Discover web) │     │  (Portal +      │     │  (Admin web)    │
│                 │     │   Flutter ops)  │     │                 │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │ HTTPS
                                 ▼
                    ┌────────────────────────┐
                    │   Adeni Platform API   │
                    │   (.NET modular        │
                    │    monolith)           │
                    └───────────┬────────────┘
                                │
       ┌────────────────────────┼────────────────────────┐
       ▼                        ▼                        ▼
  ┌─────────┐           ┌───────────┐           ┌────────────┐
  │ Auth0   │           │ SQL Server│           │ Azure Blob │
  │ (OIDC)  │           │ + Redis   │           │ (media)    │
  └─────────┘           └───────────┘           └────────────┘
       │
       ▼
  Paystack / Stripe (payments & subscription billing — market-dependent)
```

---

## 4. Container diagram (C4 Level 2)

```
┌──────────────────────────────────────────────────────────────┐
│                     Azure / hosting (target)                  │
│  ┌────────────────┐    ┌─────────────────────────────────┐  │
│  │ Angular apps   │    │ Adeni.Api (ASP.NET Core)        │  │
│  │ discover SSR   │───▶│  REST /api/v1/*                 │  │
│  │ portal, admin  │    │  Module services + middleware     │  │
│  └────────────────┘    └──────────┬──────────────────────┘  │
│                                   │                          │
│         ┌─────────────────────────┼─────────────────┐        │
│         ▼                         ▼                 ▼        │
│  ┌─────────────┐         ┌─────────────┐   ┌───────────┐  │
│  │ SQL Server  │         │ Redis       │   │ Blob      │  │
│  └─────────────┘         └─────────────┘   └───────────┘  │
└──────────────────────────────────────────────────────────────┘
```

Schema management: `db/Adeni.Database` published via SqlPackage; EF Core for ORM and migrations where used alongside the project.

---

## 5. Backend structure

```
src/
  Adeni.Domain/           Entities, events, Result<T>, ErrorCodes
  Adeni.Application/      I*Service ports, DTOs, options
  Adeni.Infrastructure/   Module implementations (per-folder *ServiceCollectionExtensions)
  Adeni.Api/              Controllers, middleware, OpenAPI (dev)
```

Composition root: `Adeni.Infrastructure.DependencyInjection.ServiceCollectionExtensions.AddInfrastructure`.

**Module boundary tests:** `ModuleBoundaryArchitectureTests` — Infrastructure module namespaces must not reference each other directly.

Detail: [Backend Overview](./backend-overview.md).

---

## 6. Multi-tenant model

- **Global customers** (`Customer`) can book across tenants.
- **Business data** is scoped by `TenantId` on `ITenantEntity` types.
- Business routes require header **`X-Tenant-Id`** matching JWT tenant claim.
- **Discovery** exposes only **Verified** tenants.

Repo runbook: `docs/tenant-isolation.md`.

---

## 7. Authentication flow

```
1. Client → Auth0 Universal Login (SPA or Native)
2. Auth0 → access_token (JWT, RS256)
3. Client → POST /api/v1/auth/sync (link Auth0 sub to Customer/BusinessUser)
4. Client → Authorization: Bearer … ; X-Tenant-Id on portal/tenant APIs
5. GET /api/v1/auth/me → roles, tenant, portal permissions
```

Admin routes: `/api/v1/admin/*` — MFA policy when `Auth0:RequireMfaForAdmin=true`. All admin mutations audited.

---

## 8. Frontend architecture

| Surface | Stack | Repo |
| --- | --- | --- |
| Discover | Angular 22 SSR | `apps/discover` |
| Business portal | Angular 22 SPA | `apps/portal` |
| Admin | Angular 22 SPA | `apps/admin` |
| Business mobile | Flutter | `apps/mobile` |
| Contracts | TypeScript + Zod | `packages/shared`, `packages/api-client` |
| Staff UI kit | Angular standalone components | `packages/ui` |

Detail: [Frontend Overview](./frontend-overview.md). Target topology: `docs/target-client-architecture.md`.

---

## 9. Integrations

| Integration | Module | Notes |
|-------------|--------|-------|
| Auth0 | Identity | JWT validation, sync, staff invites |
| Paystack | Payments, Subscriptions | Webhooks HMAC-verified (payments); subscription webhook hardening in progress |
| Stripe | Payments | NA markets |
| Azure Blob | Storage | Cover/gallery; CDN in staging/prod |
| Redis | Caching | Categories, profiles, slot locks |
| Translation API | Translation | Optional external provider |

---

## 10. CI/CD (summary)

PR → `dotnet test Adeni.slnx`, npm lint/typecheck/build for Angular workspaces, architecture tests.

Merge → build API container, deploy Angular static/SSR targets, run database publish/migrations, smoke tests.

See `docs/ci.md`.

---

## 11. Document traceability

| Source | Section |
| --- | --- |
| [Technical Documentation index](./technical-documentation-index.md) | All module + app pages |
| [architecture.md](../architecture.md) | Module rules, extraction |
| [product-bible.md](./product-bible.md) | Product index |
| PRD / specs | `docs/specs/` |

---

_Last updated: October 2026 | v1.3 — SQL Server, Angular clients, expanded modules_
