# Angular web migration checklist (strangler)

Companion to [ADR-012](./adr/ADR-012-angular-web-deferred-flutter.md). Use this as a living parity list while Next.js (`apps/web`) is replaced by **portal-web** and **discover-web**.

**Recommended order:** portal-web → discover-web → remove Next → Flutter mobile (phase 2).

---

## 0. Before writing UI

| Step | Done |
|------|------|
| Accept or amend ADR-012 (migration order, admin as 3rd app vs lazy routes) | ☐ |
| Add Auth0 SPA/native configs: discover origin, portal origin, callback/logout URLs | ☐ |
| Choose OpenAPI → TypeScript client for Angular (regenerate in CI from `src/Adeni.Api`) | ☐ |
| Document staging URL map (which paths hit Next vs Angular) | ☐ |
| Copy market resolution rules from [markets.md](./markets.md) + `@adeni/shared` behavior | ☐ |

---

## 1. Repo layout (target)

```
apps/
  discover-web/     Angular SSR — public SEO
  portal-web/       Angular — business (+ optional admin)
  web/              Next.js — legacy until strangler complete
  mobile/           Expo — maintenance until Flutter
packages/
  shared/           Keep until TS types duplicated or imported by codegen only
  api-client/       Reference for parity; Angular may use generated client
```

| Step | Done |
|------|------|
| Scaffold `portal-web` (Angular 22+, SSR if needed only for portal public pages) | ☐ |
| Scaffold `discover-web` (SSR/prerender for `/`, `/discover`, `/businesses/:slug`) | ☐ |
| Root `package.json` scripts: `dev:portal`, `dev:discover`, CI jobs | ☐ |
| Shared eslint/prettier or Nx boundary rules (optional) | ☐ |

---

## 2. Next BFF → Angular (classify each route)

Every handler under `apps/web/app/api/**` must end as **A**, **B**, or **C**:

| Class | Meaning |
|-------|---------|
| **A — Direct API** | Angular calls `ADENI_API_URL` with Auth0 bearer; delete Next proxy |
| **B — Thin BFF** | Keep a small server (Azure Functions, YARP, or minimal ASP.NET) only if secrets or cookie shaping require it |
| **C — Next-only temp** | Leave on Next until that app area migrates |

### Public / discover

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

### Business portal

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
| `.../api/payments/*` | A | Stub + initialize |

### Admin

| Next route | Class | Notes |
|------------|-------|--------|
| `.../api/admin/businesses/*` | A | Role guard |
| `.../api/admin/customers/*` | A | Export/delete |
| `.../api/admin/markets/*` | A | Live toggle |

### Auth helper

| Next route | Class | Notes |
|------------|-------|--------|
| `.../api/auth/nav` | A/B | Replace with Angular auth state + `/api/v1/auth/me` |

| Step | Done |
|------|------|
| All routes classified A/B/C | ☐ |
| Angular HTTP interceptor: Auth0 token + `Idempotency-Key` where required | ☐ |
| CORS verified for new origins on `Adeni.Api` | ☐ |

---

## 3. Portal-web feature parity (migrate first)

| Area | Next reference | Done |
|------|----------------|------|
| Auth0 login / role gate (business) | `/business/*` | ☐ |
| Dashboard shell + nav | | ☐ |
| Profile + cover upload | | ☐ |
| Services CRUD | | ☐ |
| Locations + availability | | ☐ |
| Bookings inbox (accept/reject) | | ☐ |
| Reviews panel | | ☐ |
| Payments / subscription / verification | | ☐ |
| Settings + share kit / plan (if present) | | ☐ |
| Messaging (Sprint 18) | | ☐ |

**Cutover:** proxy `/business` (and `/api/business` if any remain) to portal-web in staging → production.

---

## 4. Discover-web feature parity

| Area | Next reference | Done |
|------|----------------|------|
| `/` landing + categories | SSR metadata | ☐ |
| `/discover` search + filters | | ☐ |
| `/businesses/[slug]` profile SSR | JSON-LD / OG tags parity | ☐ |
| Booking flow + waitlist | | ☐ |
| Quote requests (Sprint 19) | | ☐ |
| Ask Adeni (rule-based → Sprint 20 LLM UI) | API-first widget | ☐ |
| Legal / market cookie / `?market=` | | ☐ |

**Cutover:** proxy `/`, `/discover`, `/businesses/*` to discover-web.

---

## 5. Admin

| Option | Done |
|--------|------|
| Lazy `/admin` in portal-web **or** separate `admin-web` | ☐ |
| Parity with Next admin screens | ☐ |

---

## 6. Quality gate before removing Next

| Check | Done |
|-------|------|
| E2E smoke: book, cancel, business accept, payment stub path | ☐ |
| SSR crawl spot-check (slug pages, sitemap if applicable) | ☐ |
| Auth0 production callbacks updated | ☐ |
| No remaining **C** routes on Next | ☐ |
| Delete or archive `apps/web`; update [frontend.md](./frontend.md) | ☐ |
| Mark ADR-012 **Accepted**; note ADR-010 superseded for web stack in Confluence | ☐ |

---

## 7. Flutter mobile (phase 2 — after web GA)

| Step | Done |
|------|------|
| Policy: Expo **feature freeze** except P0 fixes | ☐ |
| OpenAPI → Dart client | ☐ |
| Auth0 native app (PKCE) | ☐ |
| MVP: discover browse, book, account, messages | ☐ |
| Deep links to `/businesses/{slug}` on web until parity | ☐ |
| Store release + deprecate Expo | ☐ |

---

## 8. Ask Adeni / AI agent (parallel track)

Not blocked by Angular migration:

| Step | Done |
|------|------|
| Backend tool endpoints documented in OpenAPI | ☐ |
| LLM agent (Sprint 20) calls tools server-side | ☐ |
| Chat component in discover-web (embed) | ☐ |
| Angular MCP / dev skills optional for maintainer productivity | ☐ |

---

_Last updated: October 2026_
