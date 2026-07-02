# Adeni frontend monorepo

Next.js (web) + Expo (mobile) clients for the Adeni marketplace API.

## Structure

```
apps/
  web/                 Next.js — public, business, admin portals
  mobile/              Expo — unified customer + business app
packages/
  api-client/          Typed .NET API client
  shared/              Zod schemas, roles, constants
mobile/_archive/       Retired Flutter prototype
```

## Prerequisites

- Node.js 20+
- .NET 10 SDK + Docker (API)
- Expo Go app (mobile dev) or Android/iOS simulator

## Setup

```powershell
cd C:\DEV\Aceth\adeni
npm install

# API (separate terminal)
docker compose up -d
dotnet run --project src/Adeni.Api --launch-profile http
```

## Run web (Next.js)

```powershell
npm run dev:web
# http://localhost:3000
```

Optional env (`apps/web/.env.local` — copy from `apps/web/.env.local.example`):

```
APP_BASE_URL=http://localhost:3000
ADENI_API_URL=http://localhost:5169
# Optional dev override — otherwise first live market or ?market= cookie
NEXT_PUBLIC_ADENI_MARKET=lagos
AUTH0_DOMAIN=your-tenant.auth0.com
...
```

Market context is resolved at runtime (cookie, query param, env, future geo). See [markets.md](./markets.md). Copy and map center come from the active `MarketConfig` in `@adeni/shared`.

Protected routes: `/business` (business role), `/admin` (admin role). Without Auth0 env vars, portals show setup instructions instead of login.

Public booking: `/businesses/{slug}` loads services + slots; `POST /api/bookings` (Next proxy) requires Auth0 session or `DEV_CUSTOMER_AUTH0_SUB` in dev.

## Run mobile (Expo)

```powershell
npm run dev:mobile
# Scan QR with Expo Go, or press a for Android emulator
```

Set API URL in app config (see `apps/mobile/README.md`).

## Docs

- [Frontend Architecture v1 (Confluence)](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065)
- [auth0-setup.md](docs/auth0-setup.md)
