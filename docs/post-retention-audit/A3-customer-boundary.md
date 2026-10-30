# A3 — Customer frontend boundary

## Fixed

- **`/app` lazy boundary:** Authenticated routes moved to `apps/customer/src/app/app-surface/customer-app.routes.ts` and loaded via `loadChildren` from root `app.routes.ts`. Public marketplace routes no longer eagerly import `CustomerPortalShellComponent` or `/app` page components.

## Unchanged (verified adequate)

- `app.routes.server.ts`: `app/**` → `RenderMode.Client`, `no-store`, `noindex`.
- `customerAuthGuard` on `/app` parent.

## Deferred follow-up

- Auth0 `cacheLocation` review + CSP on `server.ts` — document in security backlog (see final report).
- Automated SSR leak test for `/app` (extend existing sitemap/SSR contract tests if present).
