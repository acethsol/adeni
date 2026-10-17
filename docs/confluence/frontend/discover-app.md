# Frontend — Discover app (`@adeni/discover`)

> **Parent:** [Frontend Overview](../frontend-overview.md)

---

## Purpose

**Consumer-facing web** application: SEO landing, geo discovery, business profiles, booking, queue join, quote requests, legal pages, and signed-in **my bookings**.

---

## Stack

| Item | Value |
|------|-------|
| Framework | Angular 22 |
| Rendering | SSR (`main.server.ts`, `server.ts`) |
| Dev URL | http://localhost:5190 |
| Command | `npm run dev:discover` |

---

## Directory layout

```
apps/discover/src/app/
  core/           guards, services (customer-api, seo, market-context)
  layout/         public-shell
  pages/          home, discover, business, my-bookings, queue-join, legal, document
  shared/         booking-panel, discovery-map, search, quote-request-panel
  environments/   API + Auth0 config
```

---

## Routes

| Path | Component | Notes |
|------|-----------|-------|
| `/` | Home | Hero, category stage |
| `/discover` | Discover | Search + map |
| `/businesses/:slug` | Business profile | Booking / quote panels |
| `/my-bookings` | My bookings | `customerAuthGuard` |
| `/q/:slug` | Queue join | Public walk-in |
| `/documents/:token` | Public billing doc | |
| `/privacy`, `/terms` | Legal | |
| `/checkout/stub/:reference` | Checkout stub | Dev Paystack stub |

---

## API integration

- **`CustomerApiService`** — wraps `@adeni/api-client` for discovery, bookings, messages, payments entry.
- **Market context** — resolves active market for pricing/geo defaults.
- **SEO** — `SeoService` sets title/meta for SSR pages.

---

## Auth

Auth0 SPA for customers when enabled. Local dev: see `apps/discover/.env.example.md` and `docs/auth0-setup.md`.

---

## Key shared components

- `BookingPanelComponent` — slot search + create booking
- `QuoteRequestPanelComponent` — quote business type flow
- `DiscoveryMapComponent` / `DiscoverySearchComponent`

---

## Build & deploy

- `npm run build:discover` — SSR bundle for hosting (Azure Static Web Apps / Node SSR target per infra).
- CORS: API allows origin `:5190`.

---

## Related backend modules

[Discovery](../backend/discovery.md), [Booking](../backend/booking.md), [Payments](../backend/payments.md), [Messaging](../backend/messaging.md), [Reviews](../backend/reviews.md)

---

## Related docs

- `apps/discover/README.md`
- `docs/target-client-architecture.md`
