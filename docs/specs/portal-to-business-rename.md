# Spec: Portal app → Business app (repo / npm rename)

| Field | Value |
|-------|-------|
| **Status** | **Done** (2026-10-29) — clean cutover, no `dev:portal` / `build:portal` aliases |
| **Created** | 2026-10-29 |
| **Authority** | Three Angular product names: **admin**, **customer**, **business** |

---

## Checklist

| Item | State |
|------|-------|
| Folder `apps/portal` → `apps/business` | **Done** |
| npm package `@adeni/portal` → `@adeni/business` | **Done** |
| Root scripts `dev:business` / `build:business` only (no `dev:portal` / `build:portal`) | **Done** |
| Angular project `business`, `outputPath` `dist/business`, serve port **5173** | **Done** |
| `scripts/sync-brand-assets.mjs` app id `business` | **Done** |
| CI: test `@adeni/business`, `npm run build:business` | **Done** |
| Docs: `README.md`, `AGENTS.md`, `frontend.md`, `auth0-setup.md`, architecture / checklist | **Done** |
| `npm install` at repo root | Run after the move; lockfile must list `apps/business` |
| Dev servers | `npm run dev:business` → http://localhost:5173 ; `npm run dev:customer` → http://localhost:5190 |

## Left unchanged

- API routes under `/api/v1/tenant/*`
- Permission keys such as `portal.retention` and `portal.staff`
- Config section `Portal:PublicBaseUrl` (still the business app origin, port 5173)
- In-app component names (`portal-nav`, `portal-shell`) and product copy that says “portal”
- Auth0 callback URL `http://localhost:5173` (optional: rename the SPA display name in the Auth0 dashboard)

## Commands

```powershell
npm install
npm run dev:business   # http://localhost:5173
npm run dev:customer   # http://localhost:5190
```
