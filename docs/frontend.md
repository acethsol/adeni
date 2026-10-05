# Adeni frontend monorepo

Clients for the Adeni marketplace API. Backend is a **modular monolith** — see [architecture.md](./architecture.md). **Target topology:** [target-client-architecture.md](./target-client-architecture.md).

## Structure

```
apps/
  discover/            Angular SSR — public SEO, consumers (`npm run dev:discover`, :5190)
  portal/                Angular — business portal (`npm run dev:portal`, :5173)
  admin/                 Angular — admin portal (`npm run dev:admin`, :5180)
  web/                   Next.js — legacy until strangler complete (ADR-012)
  mobile/                Expo — unified customer + business app (Flutter target deferred)
packages/
  api-client/          Typed .NET API client
  shared/              Zod schemas, roles, constants, markets
```

The Flutter prototype was retired in the July 2026 pivot (ADR-010). Consumer **Flutter** (iOS + Android) is the planned replacement for Expo after web GA (ADR-012).

## Prerequisites

- Node.js 22.22.3+ (Angular apps; see `.nvmrc`)
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

## Run Angular (target clients)

```powershell
npm run dev:discover   # http://localhost:5190
npm run dev:portal     # http://localhost:5173
npm run dev:admin      # http://localhost:5180
```

Environment files: `apps/discover/.env.example.md`, `apps/portal/.env.example.md`, `apps/admin/.env.example.md`. Auth0 SPA keys can stay empty locally; use dev Auth0 subs — [auth0-setup.md](./auth0-setup.md).

## Run web (Next.js, legacy)

```powershell
npm run dev:web
# http://localhost:3000
```

Optional env (`apps/web/.env.local` — copy from `apps/web/.env.local.example`):

```
APP_BASE_URL=http://localhost:3000
ADENI_API_URL=http://localhost:5169
NEXT_PUBLIC_ADENI_MARKET=lagos
AUTH0_DOMAIN=your-tenant.auth0.com
...
```

Market context is resolved at runtime (cookie, query param, env, geo). See [markets.md](./markets.md).

Protected routes: `/business` (business role), `/admin` (admin role). Without Auth0 env vars, portals show setup instructions instead of login.

Public booking: `/businesses/{slug}` — prefer Angular discover when cut over; Next proxy still available during strangler.

## Run mobile (Expo)

```powershell
npm run dev:mobile
```

Set API URL in app config (see `apps/mobile/README.md`).

## Docs

- [Target client architecture](./target-client-architecture.md) — canonical diagram
- [Frontend Architecture v1 (Confluence)](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065)
- [auth0-setup.md](./auth0-setup.md)
- [ADR-012](./adr/ADR-012-angular-web-deferred-flutter.md), [angular migration checklist](./angular-migration-checklist.md)
