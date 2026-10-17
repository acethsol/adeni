# Frontend — Overview (Angular monorepo + Flutter)

> **Parent:** [Technical Documentation](./technical-documentation-index.md)  
> **Repo:** `apps/*`, `packages/*`

---

## 1. Purpose

Adeni clients implement **audience-specific UX** on top of one API. They do not duplicate domain rules: validation and authorization are enforced server-side; clients use shared Zod schemas and the typed API client for contracts.

---

## 2. Application map

| App | Package name | Mode | Port (dev) | Auth |
|-----|--------------|------|------------|------|
| Discover | `@adeni/discover` | Angular SSR | 5190 | Auth0 SPA (customer); optional dev bypass |
| Portal | `@adeni/portal` | Angular SPA | 5173 | Auth0 SPA (business) + `X-Tenant-Id` |
| Admin | `@adeni/admin` | Angular SPA | 5180 | Auth0 SPA (admin) + MFA policy |
| Mobile | Flutter project | iOS/Android | — | Planned Auth0 PKCE |

Commands (repo root): `npm run dev:discover`, `dev:portal`, `dev:admin`.

---

## 3. Shared packages

| Package | Role |
|---------|------|
| `@adeni/shared` | Zod schemas, roles, capabilities, markets, i18n error keys |
| `@adeni/api-client` | Typed HTTP methods for `/api/v1/*` |
| `@adeni/ui` | Staff-oriented Angular components (portal + admin) |
| `@adeni/brand` | Logo assets, color tokens |

Dependency rule: apps → ui (optional) → api-client → shared. **No Angular imports in shared.**

Detail: [shared-packages.md](./frontend/shared-packages.md).

---

## 4. Configuration

Each Angular app has:

- `src/environments/environment*.ts` — API base URL, Auth0 domain/client/audience
- `.env.example.md` — documented env vars for local setup

Auth0: three separate SPA applications recommended for discover, portal, admin. Local dev can use `Auth0:Enabled: false` with dev subs — see `docs/auth0-setup.md`.

---

## 5. Routing overview

| App | Entry routes | Doc |
|-----|--------------|-----|
| Discover | `/`, `/discover`, `/businesses/:slug`, `/my-bookings`, `/q/:slug` | [discover-app.md](./frontend/discover-app.md) |
| Portal | `/dashboard`, `/bookings`, `/queue`, `/staff`, … | [portal-app.md](./frontend/portal-app.md) |
| Admin | `/dashboard`, `/pending`, `/businesses`, `/markets`, … | [admin-app.md](./frontend/admin-app.md) |

Portal uses **guards**: `portalAccessGuard`, `businessAuthGuard`, `portalPermissionGuard`, `staffCalendarGuard`.

---

## 6. UI conventions

- **Carbon Design System** patterns in staff apps (portal/admin)
- **Brand:** `@adeni/brand` PNG lockups via `@adeni/ui` — do not recreate wordmark in CSS
- **Capabilities:** branch features on API `businessType` / `capabilities[]` from `@adeni/shared` — not hard-coded category slugs

---

## 7. Build & CI

Root `package.json` workspaces: discover, portal, admin, packages/*.

Scripts: `npm run build:discover|portal|admin`, `npm run lint`, `npm run typecheck`.

Angular 22 with pinned versions via root `overrides`.

---

## 8. Legacy clients

Next.js and Expo were removed from the monorepo (October 2026). Reference: `docs/legacy-clients-archive.md`.

---

## 9. Application documentation

| App | Page |
|-----|------|
| Discover | [discover-app.md](./frontend/discover-app.md) |
| Portal | [portal-app.md](./frontend/portal-app.md) |
| Admin | [admin-app.md](./frontend/admin-app.md) |
| Mobile | [mobile-app.md](./frontend/mobile-app.md) |
| Packages | [shared-packages.md](./frontend/shared-packages.md) |

Canonical topology: `docs/target-client-architecture.md`.

---

_Last updated: October 2026_
