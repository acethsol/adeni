# Adeni — Technical Documentation

> **Status:** Current | **Version:** 1.0 | **Parent:** [Adeni Product Bible](https://aceth.atlassian.net/wiki/spaces/SD/pages/26279937)  
> **Repo mirror:** `docs/confluence/` on [github.com/acethsol/adeni](https://github.com/acethsol/adeni)

This page is the **entry point for engineers**: backend modular monolith, Angular web clients, Flutter mobile skeleton, shared contracts, and how they connect in development and production.

---

## 1. Platform summary

| Layer | Technology | Location |
|-------|------------|----------|
| API | .NET 10, ASP.NET Core, modular monolith | `src/Adeni.{Domain,Application,Infrastructure,Api}` |
| Database | SQL Server (Database Project + EF Core) | `db/Adeni.Database/` |
| Cache / locks | Redis | Docker Compose locally |
| Auth | Auth0 JWT (RS256), custom claims `https://adeni.io/` | See [Identity & Auth](./backend/identity-and-auth.md) |
| Public web | Angular SSR | `apps/discover` (:5190) |
| Business web | Angular SPA | `apps/portal` (:5173) |
| Admin web | Angular SPA | `apps/admin` (:5180) |
| Mobile | Flutter (business ops skeleton) | `apps/mobile` |
| Shared TS | Zod schemas, API client, UI kit | `packages/{shared,api-client,ui,brand}` |

**Architecture principles:** one deployable API, vertical modules with Application ports, tenant isolation by default, API-first clients. Deep dive: [Backend Overview](./backend-overview.md), [System Architecture v1.3](./system-architecture-v1.3.md).

---

## 2. Backend modules (quick map)

| Module | Doc | Primary consumers |
|--------|-----|-------------------|
| Identity & Auth | [identity-and-auth.md](./backend/identity-and-auth.md) | All apps |
| Tenancy | [tenancy.md](./backend/tenancy.md) | Portal, Discover, Admin |
| Catalog & Markets | [catalog-and-markets.md](./backend/catalog-and-markets.md) | Discover, Portal, Admin |
| Booking & Operations | [booking.md](./backend/booking.md) | Discover, Portal, Mobile (future) |
| Discovery | [discovery.md](./backend/discovery.md) | Discover |
| Reviews | [reviews.md](./backend/reviews.md) | Discover, Portal, Admin |
| Payments | [payments.md](./backend/payments.md) | Discover, Portal |
| Subscriptions | [subscriptions.md](./backend/subscriptions.md) | Portal, Admin |
| Messaging | [messaging.md](./backend/messaging.md) | Discover, Portal |
| Notifications | [notifications.md](./backend/notifications.md) | Backend (event-driven) |
| Trust | [trust.md](./backend/trust.md) | Portal, Admin |
| Dashboard | [dashboard.md](./backend/dashboard.md) | Portal |
| Admin | [admin.md](./backend/admin.md) | Admin |
| Storage | [storage.md](./backend/storage.md) | Portal, Discover |
| Translation | [translation.md](./backend/translation.md) | Discover (i18n) |
| Platform cross-cutting | [platform-cross-cutting.md](./backend/platform-cross-cutting.md) | All |

Module registration order in composition root: `ServiceCollectionExtensions.AddInfrastructure` → `AddIdentityModule` … `AddMessagingModule`.

---

## 3. Frontend applications (quick map)

| App | Doc | Audience | Default URL |
|-----|-----|----------|-------------|
| Discover | [discover-app.md](./frontend/discover-app.md) | Consumers (web) | http://localhost:5190 |
| Portal | [portal-app.md](./frontend/portal-app.md) | Business tenants | http://localhost:5173 |
| Admin | [admin-app.md](./frontend/admin-app.md) | Adeni staff | http://localhost:5180 |
| Mobile | [mobile-app.md](./frontend/mobile-app.md) | Business ops (Flutter) | Device / emulator |
| Shared packages | [shared-packages.md](./frontend/shared-packages.md) | All Angular apps | npm workspaces |

Overview: [Frontend Overview](./frontend-overview.md).

---

## 4. Request path (all clients)

```
Client (Angular / Flutter)
  → HTTPS + Bearer JWT (when authenticated)
  → X-Tenant-Id on tenant-scoped business routes
  → X-Correlation-Id (recommended)
  → Adeni.Api middleware pipeline
  → Controller → I*Service (Application) → Infrastructure module
  → SQL Server / Redis / Blob / external providers (Auth0, Paystack, Stripe)
```

Middleware order (non-Development): CorrelationId → SecurityHeaders → Auth → TenantScope → TenantAccess → TenantFilterSync → Audit. Development may inject dev-auth middleware — never in production.

---

## 5. Development quick start

```bash
docker compose up -d                    # SQL Server + Redis
./scripts/publish-db.ps1 -CreateNewDatabase   # schema (Windows) or see docs/database-setup.md
dotnet run --project src/Adeni.Api --launch-profile http   # :5169
npm install && npm run dev:discover     # :5190
npm run dev:portal                      # :5173
npm run dev:admin                       # :5180
```

| Service | URL |
|---------|-----|
| API | http://localhost:5169 |
| OpenAPI (dev) | http://localhost:5169/scalar/v1 |
| Health | http://localhost:5169/health |

---

## 6. Related Confluence pages

| Page | Notes |
|------|-------|
| [System Architecture v1.3](./system-architecture-v1.3.md) | Supersedes v1.2 stack details (SQL Server, Angular) |
| [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649) | Strategy-level module map (sync with backend overview) |
| [Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065) | Update when publishing frontend overview |
| [Observability v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/27230210) | App Insights, correlation |
| [SOC 2 Compliance Framework v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247170) | Audit, MFA, PII |

---

## 7. Changelog

| Date | Change |
|------|--------|
| Oct 2026 | Initial full module + app documentation set (Sprints through 35) |

---

_Last updated: October 2026_
