# Adeni — Target client architecture

> **Status:** Canonical target (Oct 2026)  
> **Backend:** [architecture.md](./architecture.md) (modular monolith)  
> **Migration:** [ADR-012](./adr/ADR-012-angular-web-deferred-flutter.md) (Next.js strangler → three Angular apps)

This document is the **north-star diagram** for who talks to what on the public internet. One .NET API remains the source of truth for domain logic; clients differ by audience and deploy boundary only.

---

## Diagram

```
                    ADENI

              Public Internet
                    │
                    ▼
        ┌─────────────────────┐
        │  Discover Web       │
        │  Angular SSR        │
        │  SEO / Public       │
        └─────────────────────┘

 Business Users                 Consumers
       │                            │
       ▼                            ▼
┌────────────────┐         ┌────────────────┐
│ Business Portal│         │ Flutter Mobile │
│ Angular        │         │ iOS + Android  │
└────────────────┘         └────────────────┘

 Admin
   │
   ▼
┌────────────────┐
│ Admin Portal   │
│ Angular        │
└────────────────┘

           All clients
               │
               ▼
          .NET API
               │
       ┌───────┴───────┐
       │               │
   PostgreSQL        Redis
```

---

## Client roles

| Client | Repo (target) | Audience | Notes |
|--------|---------------|----------|--------|
| **Discover Web** | `apps/discover` | Consumers (web) | SSR for SEO, public discovery, business profiles, booking. Auth0 SPA for signed-in customers when enabled. |
| **Business Portal** | `apps/portal` | Business tenants | Services, availability, bookings, profile, payments/plan. Auth0 SPA (business role) or dev sub locally. |
| **Admin Portal** | `apps/admin` | Adeni staff | Pending verification, markets, businesses, customer privacy (export/erasure). Separate origin recommended in production (e.g. `admin.adeni.com`). |
| **Flutter mobile** | `apps/mobile` | Business ops (owner, employee, front desk) | Skeleton in repo; consumer app after web GA. Same API + OpenAPI. |
| **.NET API** | `src/Adeni.Api` | All of the above | Modular monolith; no business logic duplicated in clients. |
| **PostgreSQL / Redis** | infra | API | Primary store + cache/session/rate-limit as configured. |

**Shared contracts:** `packages/shared`, `packages/api-client` (TypeScript today; Flutter codegen from OpenAPI when mobile lands).

---

## Legacy clients (archived)

Next.js and Expo were **removed from the main monorepo** (October 2026). Reference copy: [legacy-clients-archive.md](./legacy-clients-archive.md). New work uses **direct API** calls from Angular — no long-lived BFF layer.

---

## Local development (default ports)

| App | Command | URL |
|-----|---------|-----|
| API | `dotnet run --project src/Adeni.Api` | `http://localhost:5169` |
| Discover | `npm run dev:discover` | `http://localhost:5190` |
| Portal | `npm run dev:portal` | `http://localhost:5173` |
| Admin | `npm run dev:admin` | `http://localhost:5180` |
Auth0: three **SPA** applications (discover, portal, admin) when staging is ready; local dev can use `X-Dev-Auth0-Sub` with `Auth0:Enabled: false` — see [auth0-setup.md](./auth0-setup.md).

---

## Production routing (staging checklist)

- Public host → **Discover** (SSR), paths such as `/`, `/discover`, `/businesses/:slug`
- Business host or path prefix → **Portal** (e.g. `/business/` base href or subdomain)
- Admin host → **Admin** (restricted network or separate subdomain)
- All origins → same **API** with CORS + Auth0 audience

Document the Azure / Front Door map when cutover is scheduled (ADR-012 acceptance item).

---

## GTM vs architecture

Go-to-market may **narrow** discover categories and copy (e.g. wellness in Ottawa and Lagos) without changing this topology: same four client surfaces, same API, different catalog filters and marketing — see [markets.md](./markets.md) and product strategy.

---

## Related

- [architecture.md](./architecture.md) — modules, domain boundaries, extraction
- [ADR-012](./adr/ADR-012-angular-web-deferred-flutter.md) — decision and strangler order
- [frontend.md](./frontend.md) — monorepo commands and env
