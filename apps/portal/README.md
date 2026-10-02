# Adeni business portal (Angular)

Angular strangler target for Next.js `/business/*`. See [ADR-012](../../docs/adr/ADR-012-angular-web-deferred-flutter.md).

## Stack

- Angular **19** (upgrade to **22** when Node ≥ 22.22 — see root engines)
- `@auth0/auth0-angular` (SPA / PKCE)
- `@adeni/api-client` + `@adeni/shared` via TypeScript path mapping

## Dev

```bash
# From repo root — API on :5169, Redis + Postgres up
npm run dev:portal
```

Open http://localhost:5173 — default dev uses `auth0|local-business` against the API with Auth0 disabled.

Configuration: [`.env.example.md`](./.env.example.md) → edit `src/environments/environment.development.ts`.

## Production build

```bash
npm run build:portal
```

Output: `apps/portal/dist/portal` with `baseHref` `/business/` for host-level routing.
