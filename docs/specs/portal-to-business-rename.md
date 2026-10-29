# Spec: Portal app → Business app (repo / npm rename)

| Field | Value |
|-------|-------|
| **Status** | **Done** (2026-10-29) — folder/npm/CI, RBAC `business.*`, business app shell naming |
| **Created** | 2026-10-29 |
| **Authority** | Three Angular product names: **admin**, **customer**, **business** |

---

## Checklist

| Item | State |
|------|-------|
| Folder `apps/portal` → `apps/business` | **Done** |
| Remove legacy `apps/portal` tree from repo | **Done** |
| npm package `@adeni/portal` → `@adeni/business` | **Done** |
| Root scripts `dev:business` / `build:business` only | **Done** |
| Angular project `business`, `outputPath` `dist/business`, serve port **5173** | **Done** |
| RBAC permission strings `portal.*` → `business.*` | **Done** — `BusinessPermissions`, `@adeni/shared` `business-permissions.ts`, `RequiresBusinessPermission` |
| Business app shell: `business-shell`, `business-nav`, `business-access` guards | **Done** |
| Staff UI i18n `portal.*` → `businessApp.*` (overlay EN/FR + catalog; avoids `business.*` public profile keys in `en.ts`) | **Done** |
| Phase 2: shared dashboard helpers, `portalDashboard` → `businessApp.dashboard.presets`, localStorage `adeni.businessApp.*` | **Done** |
| Phase 2: admin `admin-shell` layout filenames (was `portal-shell`) | **Done** |
| Phase 2: customer env-adjacent vars (`businessPortalUrl`, …); keep `portalAppUrl` config key | **Done** |
| CI: test `@adeni/business`, `npm run build:business` | **Done** |
| Docs: operational paths/scripts (not historical sprint specs) | **Best-effort** |

## Left unchanged (by design)

- API routes under `/api/v1/tenant/*`
- Config section `Portal:PublicBaseUrl` (business app origin, port 5173)
- **`@adeni/ui` `PortalPageComponent`** export name (shared staff/customer page chrome)
- Public **Discover** marketplace routes (`/discover`, …) and `discoverListing` entitlement
- Auth0 callback URL `http://localhost:5173`

## Commands

```powershell
npm install
npm run dev:business   # http://localhost:5173
npm run dev:customer   # http://localhost:5190
```
