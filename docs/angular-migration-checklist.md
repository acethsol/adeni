# Angular web migration checklist (strangler)

Companion to [ADR-012](./adr/ADR-012-angular-web-deferred-flutter.md). Use this as a living parity list while **`apps/customer`**, **`apps/business`**, and **`apps/admin`** reach feature parity with the archived Next.js app ([legacy-clients-archive.md](./legacy-clients-archive.md)).

**Order:** portal → admin → discover → remove Next → Flutter mobile (phase 2).

---

## 0. Before writing UI

| Step | Done |
|------|------|
| ADR-012 accepted — three apps: `customer`, `business`, `admin` | ☑ |
| Add Auth0 SPA configs: customer, business, admin origins + callback/logout URLs | ☑ wired in Angular env; **Auth0 tenant registration deferred** until staging (dev subs OK) |
| Choose OpenAPI → TypeScript client for Angular (regenerate in CI from `src/Adeni.Api`) | ☐ |
| Document staging URL map (which paths hit Next vs each Angular app) | ☐ |
| Copy market resolution rules from [markets.md](./markets.md) + `@adeni/shared` behavior | ☐ |

---

## 1. Repo layout (target)

```
apps/
  customer/         Angular SSR — public SEO, booking, Ask Adeni UI
  business/         Angular — business role
  admin/            Angular — admin role only
  mobile/           Native Flutter business skeleton; consumer mobile is the product priority
packages/
  shared/           Shared contracts, schemas, permissions, capabilities and entitlement types
  api-client/       Typed HTTP client; compatible root facade over internal domains
  ui/               Reusable Angular UI
  brand/            Brand identity/assets
```

| Step | Done |
|------|------|
| Scaffold `apps/business` (Angular 22+; CSR or SSR only if needed) | ☑ |
| Scaffold `apps/admin` (Angular 22+; CSR typical) | ☑ |
| Scaffold `apps/customer` (SSR/prerender for `/`, `/discover`, `/businesses/:slug`) | ☑ MVP SSR (`/`, `/discover`, `/businesses/:slug`; booking flow later) |
| Root scripts: `dev:business`, `dev:admin`, `dev:customer`, CI jobs per app | ☑ (`dev:customer` :5190; `dev:business` :5173; CI builds business + admin + customer) |
| Shared eslint/prettier or Nx boundary rules (optional) | ☐ |

---

## 2. Next BFF → Angular (classify each route)

Every handler that existed under archived Next `app/api/**` must end as **A**, **B**, or **C** in Angular (see legacy repo):

| Class | Meaning |
|-------|---------|
| **A — Direct API** | Angular calls `ADENI_API_URL` with Auth0 bearer; delete Next proxy |
| **B — Thin BFF** | Keep a small server only if secrets or cookie shaping require it |
| **C — Next-only temp** | Leave on Next until that app area migrates |

### Public / discover → `apps/customer`

| Next route | Class | Notes |
|------------|-------|--------|
| `GET/POST .../api/v1/discovery` | A | |
| `.../api/market/geo` | A | |
| `.../api/businesses/[slug]/slots` | A | |
| `.../api/businesses/[slug]/quote-requests` | A | |
| `POST .../api/bookings` | A | Idempotency-Key header |
| `.../api/bookings/waitlist` | A | |
| `.../api/bookings/[id]/cancel` | A | |
| `.../api/bookings/[id]/review` | A | Customer auth |

### Business portal → `apps/business`

| Next route | Class | Notes |
|------------|-------|--------|
| `.../api/business/profile` (+ cover) | A | |
| `.../api/business/services` (+ `[id]`) | A | |
| `.../api/business/locations` (+ `[id]`) | A | |
| `.../api/business/availability` | A | |
| `.../api/business/bookings` (+ accept/reject) | A | |
| `.../api/business/settings` | A | |
| `.../api/business/reviews` | A | |
| `.../api/business/payments` (+ refund) | A | |
| `.../api/business/subscription` | A | |
| `.../api/business/verification` | A | |
| `.../api/business/register` | A | |
| `.../api/business/media/upload-url` | A | SAS upload flow |
| `.../api/payments/*` | A | Stub + initialize (customer checkout from discover may share) |

### Admin → `apps/admin`

| Next route | Class | Notes |
|------------|-------|--------|
| `.../api/admin/businesses/*` | A | Admin role guard |
| `.../api/admin/customers/*` | A | Export/delete |
| `.../api/admin/markets/*` | A | Live toggle |

### Auth helper (all apps)

| Next route | Class | Notes |
|------------|-------|--------|
| `.../api/auth/nav` | A/B | Angular auth state + `/api/v1/auth/me` per app |

| Step | Done |
|------|------|
| All routes classified A/B/C | ☐ |
| HTTP interceptor per app: Auth0 token + `Idempotency-Key` where required | ☐ |
| CORS verified for customer, business, admin origins on `Adeni.Api` | ☐ |

---

## 3. `apps/business` feature parity (migrate first)

| Area | Next reference | Done |
|------|----------------|------|
| Auth0 login / role gate (business) | `/business/*` | ☐ (dev sub OK; Auth0 SPA later) |
| Dashboard shell + nav | | ☑ |
| Profile + cover upload | | ☑ |
| Services CRUD | | ☑ |
| Locations + availability | | ☑ |
| Bookings inbox (accept/reject) | | ☑ |
| Register (new business) | | ☑ |
| Reviews panel | | ☐ |
| Payments / subscription / verification | | ☑ payments + plan; verification on profile |
| Settings + share kit / plan (if present) | | ☑ share kit on profile; booking settings on profile |
| Messaging (Sprint 36) | | ☑ Conversations + SignalR inbox; WhatsApp templates + nav |
| Pending bookings bell (topbar) | `business-portal-bell.tsx` | ☑ |

**Cutover:** proxy `/business/*` to `apps/business` in staging → production.

---

## 4. `apps/admin` feature parity (second)

| Area | Next reference | Done |
|------|----------------|------|
| Auth0 login / role gate (admin only) | `/admin/*` | ☐ Auth0 SPA later; dev `auth0\|local-admin` |
| Pending businesses approve/reject | | ☑ |
| Business list + subscription tier | | ☑ |
| Customers list / export / delete | | ☑ `/customers` search, export, erasure |
| Markets CRUD + go-live | | ☑ go-live toggle (CRUD later) |

**Cutover:** proxy `/admin/*` to `apps/admin`. Prefer **separate origin or subdomain** in production (e.g. `admin.adeni.com`) if convenient.

---

## 5. `apps/customer` feature parity (third)

| Area | Next reference | Done |
|------|----------------|------|
| `/` landing + categories | SSR metadata | ☑ title/description + OG via `SeoService` |
| `/discover` search + filters | | ☑ category, q, sort, minRating; market/geo cookies |
| `/businesses/[slug]` profile SSR | JSON-LD / OG tags parity | ☑ LocalBusiness JSON-LD + OG on profile |
| Booking flow + waitlist | | ☑ booking wizard, waitlist, deposit redirect; quote flow |
| `/my-bookings` | | ☑ list + cancel |
| Market cookies (`adeni_market`, `adeni_coords`, `?market=`) | | ☑ shared constants in `@adeni/shared` |
| Quote requests (Sprint 19) | | ☐ |
| Ask Adeni (rule-based → Sprint 20 LLM UI) | API-first widget | ☐ |
| Legal / market cookie / `?market=` | | ☐ |

**Cutover:** proxy `/`, `/discover`, `/businesses/*` to `apps/customer`.

---

## 6. Quality gate before removing Next

| Check | Done |
|-------|------|
| E2E smoke: book, cancel, business accept, admin approve, payment stub path | ☐ |
| SSR crawl spot-check (discover slug pages, sitemap if applicable) | ☑ — hubs `/lagos` `/ottawa`, category landings, `robots.txt` + dynamic `sitemap.xml` ([spec](./specs/discover-seo-landings.md)) |
| Auth0 production callbacks updated for all three apps | ☐ |
| No remaining **C** routes on Next | ☐ |
| Delete or archive `apps/web`; update [frontend.md](./frontend.md) | ☑ (see [legacy-clients-archive.md](./legacy-clients-archive.md)) |
| Update Confluence: ADR-010 web stack superseded by ADR-012 | ☐ |

---

## 7. Flutter mobile

| Step | Done |
|------|------|
| Expo removed from monorepo (archive repo) | ☑ |
| Business skeleton `apps/mobile` (owner / employee / front desk) | ☑ — [spec](./specs/flutter-business-mobile-skeleton.md) |
| OpenAPI → Dart client | ☐ |
| Auth0 native app (PKCE) for business | ☐ |
| API check-in / check-out (front desk) | ☐ |
| Consumer MVP: discover browse, book, account (after web GA) | ☐ |
| Deep links to `/businesses/{slug}` on web until parity | ☐ |
| Native Flutter store release / migration from archived Expo store client | ☐ |

---

## 8. Ask Adeni / AI agent (parallel track)

Not blocked by Angular migration:

| Step | Done |
|------|------|
| Backend tool endpoints documented in OpenAPI | ☐ |
| LLM agent (Sprint 20) calls tools server-side | ☐ |
| Chat component in `apps/customer` | ☐ |
| Angular MCP / dev skills optional for maintainer productivity | ☐ |

---

_Last updated: October 2026_
