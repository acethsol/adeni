# Adeni — System Architecture (v1.2)

> **Status:** Engineering blueprint | **Version:** 1.2 | **Parent:** [Adeni Product Bible](https://aceth.atlassian.net/wiki/spaces/SD/pages/26279937) | **Frontend detail:** [Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065) | **Module detail:** [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649)

> **v1.2 change (August 2026):** Backend formalized as a **modular monolith** (ADR-011). Full module map, boundaries, and extraction criteria: [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649).

> **v1.1 change (July 2026):** Frontend pivot from Flutter to **Next.js** (web) + **Expo** (mobile). See [Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065).

---

## 1. Purpose & Scope

This document defines **how Adeni is built**: system boundaries, component responsibilities, multi-tenant enforcement, integration patterns, deployment topology, and cross-cutting concerns.

**In scope:** Next.js web (public + business + admin), Expo mobile (iOS/Android), .NET modular monolith API, PostgreSQL, Redis, Auth0, Azure Blob, admin verification, booking lifecycle.

**Planned (Sprints 15–20):** Notifications, Paystack payments, messaging, SaaS billing, quote workflows, SignalR chat, AI agents (tool-calling on module APIs only).

**Out of scope (early):** Microservices deployment, multi-region, OpenSearch, holding customer funds.

---

## 2. Architecture Principles

| # | Principle | Implication |
| --- | --- | --- |
| 1 | **Tenant isolation by default** | Every tenant query filters by `TenantId`; enforced at API + DB layer |
| 2 | **Clean Architecture + vertical modules** | Domain/Application/Infrastructure layers **and** Booking, Tenancy, Discovery modules |
| 3 | **Modular monolith** | One deployable `Adeni.Api`; design for extraction, don't split day one |
| 4 | **API-first** | Next.js and Expo are clients; business rules live in backend modules |
| 5 | **Mobile-first UX, web-first discovery** | Expo for daily mobile; Next.js SSR for SEO |
| 6 | **Fail closed on auth** | Invalid/missing token → 401; wrong tenant → 403 |
| 7 | **Domain events for side effects** | `BookingConfirmed` → notifications (in-process now; bus when extracting) |
| 8 | **Design for Lagos v1, global later** | Timezone, currency, geo scoped; no hard-coded Nigeria in domain |

---

## 3. System Context (C4 Level 1)

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Customer       │     │  Business       │     │   Admin         │
│  (Web + Mobile) │     │  (Web + Mobile) │     │  (Web only)     │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │ HTTPS / WSS
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
  │ Auth0   │           │ PostgreSQL│           │ Azure Blob │
  │ (OIDC)  │           │ + Redis   │           │ (photos)   │
  └─────────┘           └───────────┘           └────────────┘
```

**Client platforms:** Next.js (public SEO, business portal, admin MFA) · Expo (single iOS/Android app, role-based UX)

---

## 4. Container Diagram (C4 Level 2)

```
┌──────────────────────────────────────────────────────────────┐
│                        Azure (v1)                            │
│  ┌────────────────┐    ┌─────────────────────────────────┐  │
│  │ Next.js Web    │    │ Adeni.Api (ASP.NET Core)        │  │
│  │ + Expo Mobile  │───▶│  ├── REST Controllers             │  │
│  └────────────────┘    │  ├── Module services (Booking,    │  │
│                        │  │   Tenancy, Discovery, …)     │  │
│                        │  ├── Auth + Tenant middleware    │  │
│                        │  └── (Hangfire — planned)         │  │
│                        └──────────┬──────────────────────┘  │
│                                   │                          │
│         ┌─────────────────────────┼─────────────────┐        │
│         ▼                         ▼                 ▼        │
│  ┌─────────────┐         ┌─────────────┐   ┌───────────┐  │
│  │ PostgreSQL  │         │ Redis       │   │ Blob      │  │
│  └─────────────┘         └─────────────┘   └───────────┘  │
└──────────────────────────────────────────────────────────────┘
```

**Module boundaries:** see [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649).

---

## 5–7. Backend Structure, Multi-Tenant, Auth

Backend follows Clean Architecture with **vertical modules** inside `src/Adeni.{Domain,Application,Infrastructure,Api}`.

**Auth flow (clients):**

```
1. Client → Auth0 Universal Login
2. Auth0 → access_token (JWT)
3. Client → POST /api/v1/auth/sync
4. Client → Bearer token on API calls; X-Tenant-Id on business routes
5. GET /api/v1/auth/me → session claims for role-based routing
```

Tenant isolation: `TenantAccessMiddleware` + EF global filters on `ITenantEntity`. See repo `docs/tenant-isolation.md`.

---

## 8. Frontend Architecture

**Supersedes v1 §8 (Flutter).** Full specification: [Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065)

| Surface | Stack | Scope |
| --- | --- | --- |
| Web | Next.js App Router | `(public)/` SEO · `business/` portal · `admin/` MFA |
| Mobile | Expo + Expo Router | Single app — customer + business modes |
| Shared | `packages/api-client`, `packages/shared` | Typed client, Zod schemas, business types |

**Monorepo:** `apps/web`, `apps/mobile`, `packages/*` alongside `src/` (.NET).

---

## 9–17. Database, API, Booking, Messaging, Storage, Discovery, Caching, Security, Observability

PostgreSQL (EF Core), Redis (caching + slot locks), REST API v1, booking request→accept flow, reviews tied to completed bookings, discovery geo search, Azure Blob cover uploads.

**Observability:** App Insights on API (Sprint 20); Sentry on clients. See [Observability v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/27230210).

**Messaging / SignalR / Hangfire:** planned Sprints 18–20; modules added per [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649).

---

## 18. CI/CD Pipeline

```
PR → GitHub Actions
  ├── dotnet build + test
  ├── npm run lint + test (apps/web, apps/mobile, packages/*)
  ├── Next.js build
  └── EF migration dry-run

Merge to main →
  ├── Build Docker image (Adeni.Api)
  ├── Deploy API to App Service
  ├── Deploy Next.js (Azure Static Web Apps or Vercel)
  ├── Run EF migrations
  └── Smoke tests → manual promote to production
```

**Mobile releases:** EAS Build → TestFlight / Play Internal Testing (on release tags).

---

## 19–22. Infrastructure, ADRs, Scalability, SOC 2 Controls

Modular monolith deploys as **one API container** initially. Extract Search, Notifications, Messaging, Payments when scale/team/compliance justify (see extraction criteria in module architecture doc).

### ADR updates

| ADR | Decision | Status |
| --- | --- | --- |
| ADR-002 | Flutter over React Native + Next.js | **Superseded** |
| **ADR-010** | Next.js (web) + Expo (mobile) over Flutter | **Accepted** July 2026 |
| **ADR-011** | Modular monolith over microservices | **Accepted** August 2026 |
| ADR-001, 003–009 | _(unchanged)_ | Current |

---

## 23. Document Traceability

| Source | Architecture section |
| --- | --- |
| PRD §3 Roles | §6–7 |
| PRD §6 Booking | §11 |
| PRD §7 APIs | §10 |
| Frontend surfaces | §8, [Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065) |
| Module boundaries | [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649) |
| Product strategy | [Product strategy & revenue model](https://aceth.atlassian.net/wiki/spaces/SD/pages/41091073) |

---

_Last updated: August 2026 | v1.2 — modular monolith (ADR-011)_
