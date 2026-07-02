# Markets

Adeni launches city by city (GTM), but the **platform is market-agnostic**. Lagos is our first live market — not a hard-coded default in application logic.

## Model

| Layer | What it is | Example |
| --- | --- | --- |
| **Market catalog** | Data in `@adeni/shared` | `lagos`, `ottawa`, `houston` with `isLive` (GTM supply flag), currency, timezone, copy |
| **Runtime context** | Which market the user is in | Cookie, query param, geo, or deployment override |
| **Deployment config** | Ops choice for a given environment | `Market:DefaultTimeZoneId` in appsettings for the API host |
| **Tenant data** | Where a business operates | `BusinessLocation` branches under tenant profile |

Opening the app **in Lagos** should show Lagos-specific discovery, copy, and supply. Opening it **in Ottawa** (once live) should behave the same way — no code fork.

## Frontend resolution

Shared helper: `resolveMarket()` in `packages/shared/src/market-resolver.ts`.

Priority:

1. **Explicit** — cookie `adeni_market` (from `?market=` only), or header `x-adeni-market`
2. **Geo** — browser/device coordinates → nearest catalog market within range (`adeni_coords` cookie on web)
3. **Env fallback** — `NEXT_PUBLIC_ADENI_MARKET` / `EXPO_PUBLIC_ADENI_MARKET` when location is unavailable
4. **Fallback** — first GTM live market (`isLive: true`), or first catalog entry if none are live yet

All catalog markets are browsable via `?market=` or geo. `isLive` only marks where GTM supply is active — not a platform gate.

Discovery search uses device coordinates when available, otherwise the active market center.

Web:

- `apps/web/lib/market.ts` — reads cookie/header/env per request
- `?market=lagos` on any page sets the cookie (via middleware)
- No city name baked into components; copy comes from the resolved `MarketConfig`

## Tenant vs location

```
Tenant (brand)           e.g. "Lekki Cuts"
  └── BusinessProfile    category, phone, description
  └── BusinessLocation   slug, market_id, address, lat/lng, timezone (per branch)
      └── lekki-cuts     (primary, Lagos)
      └── lekki-cuts-vi  (Victoria Island branch)
```

- Public URLs stay `/businesses/{location-slug}` — slug is per branch.
- Discovery returns one row per **active location** (same brand can appear twice if it has two nearby branches).
- Booking/services remain tenant-scoped for Sprint 4; slot timezone uses the **location** when accessed via slug.

## Backend

- `BusinessProfile` — brand fields only (category, phone, description).
- `BusinessLocation` — branch fields (`market_id`, geo, slug, optional `time_zone_id`).
- Onboarding register creates profile + primary location; additional branches via `POST /api/v1/tenant/locations`.
- Discovery accepts optional `?market=` and filters by location `market_id`.

## Adding a new market

1. Add an entry to `packages/shared/src/markets.ts`
2. Onboard supply in that geography
3. Set `isLive: true` when supply is ready for GTM (optional banner via `launchNote`)
4. Configure deployment timezone/currency as needed

No new routes, no forked UI, no Lagos-specific branches.

## Local dev

```bash
# Optional: pin market for SSR without a cookie
NEXT_PUBLIC_ADENI_MARKET=lagos

# Or set via URL once (persists in cookie)
http://localhost:3000/?market=lagos
```

To preview a non-live market for copy/layout: `?market=ottawa` (cookie stores the id; supply APIs may still be empty until live).
