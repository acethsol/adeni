# Web testing checklist

Manual and automated testing guide for **Adeni web** (`apps/web`) before mobile parity passes. API and shared packages serve both clients.

**Strategy:** Test web thoroughly first; any **client** fix that applies to mobile must be mirrored under `apps/mobile` (see [Mobile parity](#mobile-parity-after-web-fixes)).

Related: [frontend.md](./frontend.md), [payments.md](./payments.md), [auth0-setup.md](./auth0-setup.md), [AGENTS.md](../AGENTS.md).

---

## Local environment

```powershell
cd C:\DEV\Aceth\adeni
docker compose up -d
dotnet run --project src\Adeni.Api --launch-profile http
npm run dev:web
```

| Service | URL |
|---------|-----|
| Web | http://localhost:3000 |
| API | http://localhost:5169 |
| API docs (Scalar) | http://localhost:5169/scalar/v1 |
| Health | http://localhost:5169/health |

Copy `apps/web/.env.local.example` → `.env.local`. Typical dev:

- `ADENI_API_URL=http://localhost:5169`
- `DEV_CUSTOMER_AUTH0_SUB=auth0|local-customer` (when Auth0 disabled on API)
- `DEV_BUSINESS_AUTH0_SUB=auth0|local-business` (dev seed → **lekki-cuts** tenant)
- `NEXT_PUBLIC_ADENI_MARKET=lagos` (optional)

---

## How to use this doc

- [ ] = manual check during a test pass
- Mark **Pass / Fail / Skip** and link bugs or PRs
- After a web fix, complete the [parity row](#mobile-parity-after-web-fixes) for that area

**Dev seed business slug:** `lekki-cuts` (business sub `auth0|local-business`).

---

## A — Public & customer

### Discovery & home

- [x] `/` — home loads, market context, navigation *(agent: 200, 2026-09-19)*
- [x] `/discover` — business list, geo/distance, empty state *(agent: 200; lekki in geo discovery)*
- [x] Filters — sort (distance/featured), price/rating, clear filters *(agent: `?sort=featured`, `?minRating=4` → 200)*
- [ ] Business card — cover image, category, reviews count, CTA *(manual: visual)*

### Public profile & booking

- [x] `/businesses/lekki-cuts` — profile, services, reviews panel *(agent: 200; reviews + booking UI in HTML)*
- [x] Booking panel — service → slots → confirm → success *(agent: API + `/api/bookings` POST 201; **click-through in browser** still recommended)*
- [x] Deposit flow (if `deposits` capability + deposit %) — redirects to stub checkout, receipt *(agent: initialize 200, stub page 200, stub confirm 200, receipt 200; barbers seed now includes `deposits` capability — **restart API** to reload catalog)*
- [ ] Quote flow (quote-capable category) — quote request panel, validation *(Skip lekki — `scheduled_appointment`; use plumber/electrician seed or add quote tenant)*
- [x] Masked phone on public profile (no full PII) *(API `phoneMasked` redacted)*

### Customer account

- [x] `/my-bookings` — list, status, cancel with confirmation *(agent: GET `/api/bookings` OK; cancel API 200 — **confirm dialog** manual)*
- [x] Review submission after completed booking (if applicable) *(agent: POST review 201 on dev seed booking)*
- [x] Auth0 login **or** dev customer sub when Auth0 off *(agent: `DEV_CUSTOMER_AUTH0_SUB` via web proxy)*

### UX & errors

- [ ] Toasts on save/copy success *(manual)*
- [ ] Confirm dialogs — cancel booking, destructive actions *(manual)*
- [ ] API errors — localized messages (`ApiErrorResponse` / `useApiErrorMessage`), not raw English `title` only *(manual: trigger slot conflict / 401)*
- [x] `error.tsx` / `not-found` — friendly fallbacks *(agent: invalid slug → not-found UI; API 404; verify HTTP 404 in browser if needed)*

---

## B — Business portal

Requires business role / `DEV_BUSINESS_AUTH0_SUB` in dev.

### Overview & navigation

- [ ] `/business` — overview metrics/charts, usage meter
- [ ] Portal nav — all sections reachable, active state
- [ ] Unsaved changes guard — profile, availability, modals

### Profile & verification

- [ ] `/business/profile` — edit draft/rejected only when policy applies
- [ ] Submit verification — status messaging
- [ ] Cover upload (if enabled)

### Catalog & operations

- [ ] `/business/services` — CRUD, deactivate, validation
- [ ] `/business/locations` — add/edit, multi-location (Business tier)
- [ ] `/business/availability` — multiple blocks per day, save

### Bookings inbox

- [ ] `/business/bookings` — pending/confirmed, accept/reject with confirm
- [ ] Booking settings — deposit % when deposits enabled

### Monetization (Sprint 16)

- [ ] `/business/plan` — tier comparison, usage limits, upgrade stub
- [ ] Free tier — booking limit warning near 20/month

### Payments (Sprint 17)

- [ ] `/business/payments` — ledger list
- [ ] Create payment link — amount, description, copy link
- [ ] WhatsApp share affordance
- [ ] Refund (stub provider) — status updates in ledger
- [ ] Stub checkout — `/checkout/stub/{reference}` → receipt

### Onboarding

- [ ] `/business/register` — new tenant registration
- [ ] Share kit / booking link / QR on plan or profile (where exposed)

---

## C — Admin

Requires admin role / dev admin auth per [auth0-setup.md](./auth0-setup.md).

- [ ] `/admin` — verification queue, approve/reject + audit
- [ ] Markets admin — toggle live, validation
- [ ] Subscription tier override for pilot tenants

---

## D — API & security smoke (any tester)

Run while API is up:

```powershell
Invoke-RestMethod http://localhost:5169/health
Invoke-RestMethod http://localhost:5169/api/v1/discovery?marketId=lagos
```

- [ ] Health — database + cache healthy
- [ ] Discovery — only verified tenants
- [ ] Cross-tenant — business A cannot access business B data (403)

Automated: `dotnet test` on `tests/` (Domain, Application, Infrastructure, Api).

---

## Mobile parity after web fixes

When you fix a web bug, classify and mirror:

| Class | Web location | Mobile mirror |
|-------|----------------|---------------|
| **Shared contract** | `packages/shared`, `packages/api-client` | Automatic |
| **API / backend** | `src/Adeni.*` | Automatic |
| **Customer booking** | `apps/web/components/booking-panel.tsx` | `apps/mobile/components/adeni/BookingPanel.tsx` |
| **Business flows** | `apps/web/app/business/**` | `apps/mobile/app/business/**` |
| **Auth / tokens** | Auth0 session + dev headers | `apps/mobile/lib/auth/*`, `secure-storage.ts` |
| **Errors / i18n** | `apps/web/lib/api-error.ts` | Mobile alert/copy using same `errors.*` keys |
| **Web-only** | Layout, admin, payments portal pages | Track as backlog for mobile UI |

**Known web-first gaps (Sprint 17+):** full `/business/payments` portal may not exist on mobile until explicitly built.

---

## How the agent can help test

| Method | What it covers | Today |
|--------|----------------|--------|
| **`dotnet test`** | Services, HTTP contracts, tenant rules, payments unit tests | ✅ 155+ tests |
| **Vitest (`packages/shared`)** | API error parsing, i18n keys | ✅ |
| **HTTP smoke** | Health, discovery, OpenAPI, public JSON routes | ✅ via shell |
| **Manual checklist** | This doc — you click; agent fixes from your notes/screenshots | ✅ |
| **Playwright E2E** | Real browser — booking, portal, checkout | ❌ not set up yet |

The agent **cannot** see your browser like a human; it **can** run API/integration tests, probe URLs, read code paths, and fix issues you report. For repeatable browser regression, add Playwright (below).

---

## Playwright (recommended, phased)

[Playwright](https://playwright.dev/) is **not required** to start web testing, but it is the right choice for **automated E2E** on Next.js:

1. **Phase 1 — Smoke (CI-friendly)**  
   Home, discover, public profile loads, health via web proxy if used.

2. **Phase 2 — Dev-auth critical paths**  
   Booking on `lekki-cuts`, business inbox accept/reject, stub payment link (requires API + DB seed in CI).

3. **Phase 3 — Auth0**  
   Optional tagged tests with test tenant credentials (secrets in CI only).

Suggested layout when you adopt it:

```
apps/web/
  e2e/
    discovery.spec.ts
    booking-dev.spec.ts
  playwright.config.ts
```

Run against `webServer: { command: 'npm run dev', url: 'http://localhost:3000' }` and ensure Docker Postgres + API start in CI (or use `Testing` environment + in-memory DB for API-only E2E).

**Until Playwright lands:** use this checklist manually + agent-driven API tests and fixes.

---

## Test pass log (template)

| Date | Tester | Areas | Result | Notes / PR |
|------|--------|-------|--------|------------|
| 2026-09-19 | Agent + user | A (partial) | Mixed | API/discovery/profile/slots OK; web hung until Next restart; **POST booking 500** — investigate; manual UI booking/deposit/reviews still needed |
| 2026-09-19 | Agent | A (continued) | Pass (automated) | Booking 500 fixed (UTC slots); web+API booking/deposit/review/cancel smoke; **manual**: filters UX, card visuals, booking click-through, toasts, confirm dialogs, quote category |

---

## Quick commands

```powershell
dotnet test tests\Adeni.Api.Tests\Adeni.Api.Tests.csproj
dotnet test tests\Adeni.Infrastructure.Tests\Adeni.Infrastructure.Tests.csproj
npm run typecheck --prefix C:\DEV\Aceth\adeni
npm test --workspace @adeni/shared --prefix C:\DEV\Aceth\adeni
```
