# Angular web migration checklist (strangler)

Companion to [ADR-012](./adr/ADR-012-angular-web-deferred-flutter.md). Use this as a living parity list while Next.js (`apps/web`) is replaced by **`apps/discover`**, **`apps/portal`**, and **`apps/admin`**.

**Order:** portal → admin → discover → remove Next → Flutter mobile (phase 2).

---

## 0. Before writing UI

| Step | Done |
|------|------|
| ADR-012 accepted — three apps: `discover`, `portal`, `admin` | ☑ |
| Add Auth0 SPA configs: discover, portal, admin origins + callback/logout URLs | ☑ wired in Angular env; **Auth0 tenant registration deferred** until staging (dev subs OK) |
| Choose OpenAPI → TypeScript client for Angular (regenerate in CI from `src/Adeni.Api`) | ☐ |
| Document staging URL map (which paths hit Next vs each Angular app) | ☐ |
| Copy market resolution rules from [markets.md](./markets.md) + `@adeni/shared` behavior | ☐ |

---

## 1. Repo layout (target)

```
apps/
  discover/         Angular SSR — public SEO, booking, Ask Adeni UI
  portal/           Angular — business role
  admin/            Angular — admin role only
  web/              Next.js — legacy until strangler complete
  mobile/           Expo — maintenance until Flutter
packages/
  shared/           Keep until TS types duplicated or imported by codegen only
  api-client/       Reference for parity; Angular may use generated client
```

| Step | Done |
|------|------|
| Scaffold `apps/portal` (Angular 22+; CSR or SSR only if needed) | ☑ |
| Scaffold `apps/admin` (Angular 22+; CSR typical) | ☑ |
| Scaffold `apps/discover` (SSR/prerender for `/`, `/discover`, `/businesses/:slug`) | ☑ MVP SSR (`/`, `/discover`, `/businesses/:slug`; booking flow later) |
| Root scripts: `dev:portal`, `dev:admin`, `dev:discover`, CI jobs per app | ☑ (`dev:discover` :5190; CI builds portal + admin + discover) |
| Shared eslint/prettier or Nx boundary rules (optional) | ☐ |

---

## 2. Next BFF → Angular (classify each route)

Every handler under `apps/web/app/api/**` must end as **A**, **B**, or **C**:

| Class | Meaning |
|-------|---------|
| **A — Direct API** | Angular calls `ADENI_API_URL` with Auth0 bearer; delete Next proxy |
| **B — Thin BFF** | Keep a small server only if secrets or cookie shaping require it |
| **C — Next-only temp** | Leave on Next until that app area migrates |

### Public / discover → `apps/discover`

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

### Business portal → `apps/portal`

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
| CORS verified for discover, portal, admin origins on `Adeni.Api` | ☐ |

---

## 3. `apps/portal` feature parity (migrate first)

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
| Payments / subscription / verification | | ☑ payments + plan; verification **basic form only** — see [§3a](#3a-business-verification-parity-portal--admin--api) |
| Settings + share kit / plan (if present) | | ☑ share kit on profile; booking settings on profile |
| Messaging (Sprint 18) | | ☑ WhatsApp templates + nav; in-app threads when API lands |
| Pending bookings bell (topbar) | `business-portal-bell.tsx` | ☑ |

**Cutover:** proxy `/business/*` to `apps/portal` in staging → production.

### 3a. Business verification parity (portal + admin + API)

Manual tenant verification (Draft → Pending → Verified/Rejected) is **API-complete**; Angular apps ship a **minimal** path. Next (`business-verification-status.tsx`, `admin-verification-queue.tsx`) is the **UX reference** until these rows are ☑.

**Backend / API (shared by portal and admin)**

| Item | Next / legacy | Angular / API today | Done |
|------|---------------|---------------------|------|
| Submit docs `POST /api/v1/tenant/verification` | | Portal + mobile call API directly (class **A**) | ☑ |
| Doc types CAC + National ID, reference stored in DB | | Same | ☑ |
| Profile returns submitted doc **type + date** | | Same; **reference numbers omitted** from `GET /api/v1/tenant/profile` | ☑ partial |
| Admin pending list | | `GET /api/v1/admin/businesses/pending` — name, slug, market, **tenant `createdAt`** only | ☑ partial |
| Admin sees submitted references/types for review | _(none in Next either)_ | **No admin detail endpoint**; queue is approve/reject without in-app evidence | ☐ |
| Rejection reason returned to business owner | | Reason only in **audit log** metadata, not profile API | ☐ |
| Audit on submit | `BusinessVerificationSubmitted` constant exists | Not written on submit | ☐ |
| Audit on approve/reject | | ☑ | ☑ |
| File upload for verification PDFs | Planned in [media-storage.md](./media-storage.md) | Reference-only; no blob upload | ☐ (Sprint 19+ / ops) |
| Reference validation | Next form ≥ 4 chars | API **≥ 5**; portal UI **≥ 4** — align to 5 | ☐ |

**`apps/portal` — business owner UX**

| Item | Next reference | Angular `profile` today | Done |
|------|----------------|-------------------------|------|
| Status label (Draft / Pending / Verified / Rejected / Suspended) | `business-verification-status.tsx` | Text via `formatTenantStatus` | ☑ |
| Verification **stepper** (Draft → Pending → Verified; rejected state on step 2) | `business-verification-status.tsx` | Not ported | ☐ |
| **Suspended** callout (hidden from customers, contact support) | same | Not ported | ☐ |
| Submitted documents list (type + submitted date) | same | Not shown after submit | ☐ |
| Rejection banner + “update and resubmit” | same | Generic Rejected status only | ☐ |
| Submit form (type + reference) | `business-verification-form.tsx` | ☑ on profile when Draft/Rejected | ☑ |
| Field validation (min length, errors) | `ui/input` error state | Basic inline check; mismatch vs API | ☐ |
| Multi-document submit (CAC + ID in one request) | API supports array; UI one doc | Same one-doc UX | ☐ optional |
| Post-submit toast / success message | toast + inline message | Inline `verificationMessage` | ☑ |
| Profile lock when Pending/Verified | | ☑ `canEdit()` Draft/Rejected only | ☑ |
| Prerequisites copy (complete location/slug before submit) | register + profile copy | API error only if location missing | ☐ |

**`apps/admin` — reviewer UX**

| Item | Next reference | Angular `/pending` today | Done |
|------|----------------|----------------------------|------|
| Pending queue list | `admin-verification-queue.tsx` | ☑ | ☑ |
| Empty state | `EmptyState` + ShieldCheck | Plain “Queue is empty” | ☐ |
| **Approve** with confirm dialog | `useConfirm` — “go live in discovery” | One-click approve, no confirm | ☐ |
| **Reject** with textarea (min 10 chars) | inline textarea + confirm | `prompt()` | ☐ |
| Show **submittedAt** (verification time) | uses `createdAt` in Next too | Same — prefer doc submit time when API exposes it | ☐ |
| **Detail panel**: doc types, reference numbers, link to public slug | _(missing in Next)_ | Required for meaningful review | ☐ |
| Toast on success/error | | Error banner only | ☐ |
| Link from dashboard | | ☑ “Review pending businesses” | ☑ |

**`apps/discover` — customer trust (verification outcome)**

| Item | Notes | Done |
|------|-------|------|
| Only **Verified** tenants in discovery/booking | Backend enforced | ☑ |
| Copy: “verified businesses” on home/discover | Marketing only | ☑ |
| **Verified badge** on discover cards | Sprint 19 tiered badges; v1 could be single shield on Verified | ☐ |
| Verified indicator on `/businesses/:slug` | | ☐ |

**Suggested implementation order**

1. **API:** `GET /api/v1/admin/businesses/pending/{id}` (or expand list DTO) with documents + `submittedAt`; optional `lastRejectionReason` on tenant profile for Rejected status.
2. **Admin:** detail drawer/modal + reject modal (port Next confirm/textarea patterns to Angular shared dialog).
3. **Portal:** port stepper + submitted-docs + rejection banner; align reference min length with API.
4. **Discover:** single “Adeni verified” badge when status is Verified (before Sprint 19 tiered badges).

**Quality gate (add to §6 when ready)**

| Check | Done |
|-------|------|
| Admin can approve/reject only after viewing submitted references in UI | ☐ |
| Owner sees rejection reason (or generic + support) after reject | ☐ |
| E2E: register → submit verification → admin approve → business appears on discover | ☐ |

---

## 4. `apps/admin` feature parity (second)

| Area | Next reference | Done |
|------|----------------|------|
| Auth0 login / role gate (admin only) | `/admin/*` | ☐ Auth0 SPA later; dev `auth0\|local-admin` |
| Pending businesses approve/reject | | ☑ queue + actions; **review UX incomplete** — see [§3a](#3a-business-verification-parity-portal--admin--api) |
| Business list + subscription tier | | ☑ |
| Customers list / export / delete | | ☑ `/customers` search, export, erasure |
| Markets CRUD + go-live | | ☑ go-live toggle (CRUD later) |

**Cutover:** proxy `/admin/*` to `apps/admin`. Prefer **separate origin or subdomain** in production (e.g. `admin.adeni.com`) if convenient.

---

## 5. `apps/discover` feature parity (third)

| Area | Next reference | Done |
|------|----------------|------|
| `/` landing + categories | SSR metadata | ☑ title/description + OG via `SeoService` |
| `/discover` search + filters | | ☑ category, q, sort, minRating; market/geo cookies |
| `/businesses/[slug]` profile SSR | JSON-LD / OG tags parity | ☑ LocalBusiness JSON-LD + OG on profile |
| Verified trust badge on listing + profile | | ☐ — see [§3a](#3a-business-verification-parity-portal--admin--api) |
| Booking flow + waitlist | | ☑ booking wizard, waitlist, deposit redirect; quote flow |
| `/my-bookings` | | ☑ list + cancel |
| Market cookies (`adeni_market`, `adeni_coords`, `?market=`) | | ☑ shared constants in `@adeni/shared` |
| Quote requests (Sprint 19) | | ☐ |
| Ask Adeni (rule-based → Sprint 20 LLM UI) | API-first widget | ☐ |
| Legal / market cookie / `?market=` | | ☐ |

**Cutover:** proxy `/`, `/discover`, `/businesses/*` to `apps/discover`.

---

## 6. Quality gate before removing Next

| Check | Done |
|-------|------|
| E2E smoke: book, cancel, business accept, admin approve, payment stub path | ☐ |
| SSR crawl spot-check (discover slug pages, sitemap if applicable) | ☐ |
| Auth0 production callbacks updated for all three apps | ☐ |
| No remaining **C** routes on Next | ☐ |
| Delete or archive `apps/web`; update [frontend.md](./frontend.md) | ☐ |
| Update Confluence: ADR-010 web stack superseded by ADR-012 | ☐ |

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
| Chat component in `apps/discover` | ☐ |
| Angular MCP / dev skills optional for maintainer productivity | ☐ |

---

_Last updated: October 2026_
