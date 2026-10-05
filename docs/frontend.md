# Adeni frontend monorepo

Clients for the Adeni marketplace API. Backend is a **modular monolith** — see [architecture.md](./architecture.md). **Target topology:** [target-client-architecture.md](./target-client-architecture.md).

## Structure

```
apps/
  discover/            Angular SSR — public SEO, consumers (`npm run dev:discover`, :5190)
  portal/              Angular — business portal (`npm run dev:portal`, :5173)
  admin/               Angular — admin portal (`npm run dev:admin`, :5180)
packages/
  api-client/          Typed .NET API client
  shared/              Zod schemas, roles, constants, markets, wellness catalog
```

Consumer **Flutter** (iOS + Android) is planned after web GA (ADR-012). Archived **Next.js + Expo** live in [legacy-clients-archive.md](./legacy-clients-archive.md).

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
npm run dev:discover   # http://localhost:5190
npm run dev:portal     # http://localhost:5173
npm run dev:admin      # http://localhost:5180
```

Environment files: `apps/discover/.env.example.md`, `apps/portal/.env.example.md`, `apps/admin/.env.example.md`. Auth0 SPA keys can stay empty locally; use dev Auth0 subs — [auth0-setup.md](./auth0-setup.md).

Protected routes: portal (business role), admin (admin role). Without Auth0 env vars, portals show setup instructions instead of login.

Public booking and discovery: **discover** app (`/businesses/{slug}`, `/discover`, …).

## Docs

- [Target client architecture](./target-client-architecture.md) — canonical diagram
- [Legacy Next/Expo archive](./legacy-clients-archive.md)
- [Frontend Architecture v1 (Confluence)](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065)
- [auth0-setup.md](./auth0-setup.md)
- [ADR-012](./adr/ADR-012-angular-web-deferred-flutter.md), [angular migration checklist](./angular-migration-checklist.md)
