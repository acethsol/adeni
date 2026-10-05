# Design system & client caching

Cross-platform tokens live in `@adeni/shared`. Shared staff-app UI (page layout, cards, buttons, form fields) lives in `@adeni/ui`. Discover keeps consumer-specific components locally; archived React clients documented in [legacy-clients-archive.md](./legacy-clients-archive.md).

## Design tokens (`@adeni/shared`)

`packages/shared/src/design-tokens.ts` defines:

- **Colors** — background, surface, primary, accent, muted, destructive
- **Spacing / radius / typography / shadows**
- **Query keys & stale times** — aligned with API Redis TTLs (for any TS client using TanStack Query)

## Angular apps

| App | Styles entry |
|-----|----------------|
| Discover | `apps/discover/src/styles.scss` |
| Portal | `apps/portal/src/styles.scss` |
| Admin | `apps/admin/src/styles.scss` |

Staff layout SCSS: `@use "staff-layout" as *` (from `@adeni/ui`, via `stylePreprocessorOptions.includePaths` in portal/admin). Portal-only widgets (e.g. pending bookings bell): `apps/portal/src/app/shared/`.

## Client data fetching

Angular services call the .NET API via `packages/api-client` (or thin wrappers). Prefer API cache TTLs documented in [caching-setup.md](./caching-setup.md) when choosing client stale times.

Legacy Next/Expo TanStack Query patterns remain in the archive repo for parity reference.
