# Design system & client caching

Cross-platform tokens and UI primitives live in shared packages; web and mobile apps consume them consistently.

## Design tokens (`@adeni/shared`)

`packages/shared/src/design-tokens.ts` defines:

- **Colors** — background, surface, primary, accent, muted, destructive
- **Spacing / radius / typography / shadows** — used by mobile `StyleSheet` and documented for web Tailwind mapping
- **Query keys & stale times** — aligned with API Redis TTLs

## Web (`apps/web`)

| Layer | Location |
|-------|----------|
| Tailwind theme | `app/globals.css` — semantic utilities (`bg-primary`, `text-muted`, …) |
| UI primitives | `components/ui/` — Button, Card, Input, Badge, EmptyState, Skeleton, PageHeader, Callout |
| Class helper | `lib/cn.ts` |

Prefer `components/ui/*` and semantic Tailwind classes over raw hex in pages.

## Mobile (`apps/mobile`)

| Layer | Location |
|-------|----------|
| Theme re-export | `lib/theme.ts` — imports from `@adeni/shared` |
| UI primitives | `components/ui/` — mirrors web component names |

## Client caching (TanStack Query)

Both apps use `@tanstack/react-query`:

| Data | Stale time | Query key |
|------|------------|-----------|
| Categories | 1 hour | `queryKeys.categories` |
| Discovery | 2 min | `queryKeys.discovery(params)` |
| My bookings | 30 sec | `queryKeys.myBookings` |

- **Web provider:** `app/providers.tsx`
- **Web hooks:** `lib/queries/public.ts`, `components/my-bookings-list.tsx`
- **Mobile provider:** `app/_layout.tsx` + `lib/query-client.ts`
- **Mobile hooks:** `lib/queries/public.ts`, `lib/queries/bookings.ts`

Server-rendered Next.js pages also use `export const revalidate` on home (1h) and discover (2m) to align with API cache.

## Backend cache (reference)

See [caching-setup.md](./caching-setup.md) — Redis `ICacheService` on the API; client stale times mirror those TTLs.
