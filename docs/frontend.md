# Adeni frontend monorepo

Clients for the Adeni marketplace API. Backend is a **modular monolith** — see [architecture.md](./architecture.md). **Target topology:** [target-client-architecture.md](./target-client-architecture.md).

## Structure

```
apps/
  customer/            Angular SSR — public SEO + signed-in consumer app `/app/*` (`npm run dev:customer`, :5190)
  business/            Angular — business portal (`npm run dev:business`, :5173)
  admin/               Angular — admin portal (`npm run dev:admin`, :5180)
  mobile/              Flutter — business skeleton (owner / employee / front desk)
packages/
  api-client/          Typed .NET API client
  shared/              Zod schemas, roles, constants, markets, wellness catalog
  ui/                  Shared Angular UI (portal + admin page chrome, staff SCSS)
```

**Customer app surfaces:** public SSR routes (`/`, `/discover`, `/businesses/:slug`, SEO hubs) stay crawlable; authenticated **consumer portal** under `/app/*` (dashboard, calendar, bookings, messages, benefits — client-rendered, `noindex`) — [sprint-c1-consumer-app-experience.md](./specs/sprint-c1-consumer-app-experience.md), [sprint-c2-consumer-portal.md](./specs/sprint-c2-consumer-portal.md). Flutter consumer stub: [specs/flutter-consumer-app.md](./specs/flutter-consumer-app.md). Business Flutter: [specs/flutter-business-mobile-skeleton.md](./specs/flutter-business-mobile-skeleton.md).

Consumer **Flutter** (discover/book) remains planned after web GA (ADR-012). Archived **Next.js + Expo**: [legacy-clients-archive.md](./legacy-clients-archive.md).

## Prerequisites

- Node.js 22.22.3+ (Angular apps; see `.nvmrc`)
- .NET 10 SDK + Docker (API)

## Setup

```powershell
cd C:\DEV\Aceth\adeni
npm install

# API (separate terminal)
docker compose up -d
dotnet run --project src/Adeni.Api --launch-profile http
```

## Run Angular clients

```powershell
npm run dev:customer   # http://localhost:5190
npm run dev:business     # http://localhost:5173
npm run dev:admin      # http://localhost:5180
```

Environment files: `apps/customer/.env.example.md`, `apps/business/.env.example.md`, `apps/admin/.env.example.md`. Auth0 SPA keys can stay empty locally; use dev Auth0 subs — [auth0-setup.md](./auth0-setup.md).

Protected routes: portal (business role), admin (admin role). Without Auth0 env vars, portals show setup instructions instead of login.

Public booking and discovery: **customer** app (`/businesses/{slug}`, `/discover`, SEO hubs). Signed-in home/bookings/messages/benefits: `/app/*`.

## Docs

- [Target client architecture](./target-client-architecture.md) — canonical diagram
- [Legacy Next/Expo archive](./legacy-clients-archive.md)
- [Frontend Architecture v1 (Confluence)](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065)
- [auth0-setup.md](./auth0-setup.md)
- [ADR-012](./adr/ADR-012-angular-web-deferred-flutter.md), [angular migration checklist](./angular-migration-checklist.md)
