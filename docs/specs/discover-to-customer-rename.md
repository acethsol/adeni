# Spec: Discover app → Customer app (repo / npm rename)

| Field | Value |
|-------|-------|
| **Status** | **Done** (2026-10-29) — clean cutover, no `dev:discover` / `build:discover` aliases |
| **Created** | 2026-10-29 |
| **Authority** | Aligns three Angular **product names**: **admin**, **customer**, **business** (today’s `apps/business` / `@adeni/business`) |

---

## 1. Recommendations

| Decision | Answer | Rationale |
|----------|--------|-----------|
| Rename repo folder + npm package `discover` → `customer`? | **Yes** | Matches C1 “consumer app” language and `customer-*` shell/guards already in the codebase; reduces confusion with the public **Discover** marketplace surface. |
| Rename public URL paths (`/discover`, `/businesses`, `/app/discover`)? | **No (keep)** | SEO, sitemaps, canonicals, shared links, and C1 IA explicitly keep `/discover` as the marketplace route and `/app/discover` as the in-app tab ([sprint-c1-consumer-app-experience.md](./sprint-c1-consumer-app-experience.md) §4–§6). |
| Rename `apps/business` → `apps/business` in the same PR? | **Defer** | User-facing label is already “business portal”; folder rename is a second mechanical pass (~similar file count). Do customer app first. |
| Auth0 Dashboard application **display name** | **Optional** | Callback URL stays `http://localhost:5190` (and prod customer origin); no callback change required for folder rename. Rename SPA in Auth0 for operator clarity only. |
| Azure / deploy slot names | **Review at deploy** | Pipeline artifact paths may reference `dist/discover`; update app service / static web app root to `dist/customer` when rename ships. |

### Product language (after rename)

| Term | Meaning |
|------|---------|
| **Customer app** | Angular SSR deployable (`apps/customer`, `@adeni/customer`, `:5190`) — public + signed-in surfaces on one origin. |
| **Discover** (capital D in copy) | Marketplace **verb/surface**: route `/discover`, components `discover.component`, “Explore / Discover” in UI — **not** the npm package name. |
| **Discovery** (API/domain) | Backend module (`DiscoveryController`, `discovery-api.ts`, SQL “discovery tests”) — **unchanged**. |

---

## 2. Scope table

| Layer | Rename? | Notes |
|-------|---------|-------|
| Folder `apps/discover` → `apps/customer` | **Yes** | `git mv` |
| npm package `@adeni/discover` → `@adeni/customer` | **Yes** | `apps/customer/package.json` |
| Root workspaces + scripts | **Yes** | `dev:customer`, `build:customer` only (no legacy npm script aliases) |
| Angular `angular.json` project key + `outputPath` | **Yes** | `discover` → `customer`, `dist/discover` → `dist/customer` |
| `serve:ssr` script | **Yes** | `node dist/customer/server/server.mjs` |
| `scripts/sync-brand-assets.mjs` | **Yes** | App id in `apps[]` array |
| CI (`.github/workflows/ci.yml`) | **Yes** | Step name + `npm run build:customer` |
| Docs: `frontend.md`, `README.md`, `AGENTS.md`, `auth0-setup.md` | **Yes** | Paths and script names; Auth0 section title “Angular customer” |
| Historical specs / closeouts under `docs/specs/` | **Best-effort** | Bulk replace path/script refs; leave narrative “Discover app” where it means the old name |
| Route `/discover`, page `DiscoverComponent`, `discovery-*` UI | **No** | Public SEO + feature naming |
| `@adeni/business`, `apps/business` | **No** | Separate “portal → business” track |
| Flutter `apps/mobile` | **No code** | Update `apps/mobile/README.md` one line: “Consumer **customer** web (ADR-012)” |

---

## 3. Execution steps (mechanical)

1. **Pre-flight**
   - Commit or stash in-progress C1 changes on `apps/discover` (see §5). Renaming mid-flight loses `git` path tracking for ~40 modified files.
2. **Move tree**
   ```bash
   git mv apps/discover apps/customer
   ```

3. **Package + workspace**
   - Root `package.json`: workspace `apps/customer`; scripts `dev:customer`, `build:customer`.
   - `apps/customer/package.json`: `"name": "@adeni/customer"`, `serve:ssr` path.
   - `apps/customer/.npmrc`: comment path `apps/customer/node_modules/...`.
   - Regenerate lockfile: `npm install` at repo root (updates `package-lock.json` workspace links).

4. **Angular**
   - `apps/customer/angular.json`: project name `customer`, all `discover:` build targets → `customer:`, `outputPath`: `dist/customer`.
   - Search `apps/customer` for string `dist/discover` (SSR server, tests).

5. **Tooling**
   - `scripts/sync-brand-assets.mjs`: `customer` in `apps` array + log message.

6. **CI / docs**
   - `.github/workflows/ci.yml`: build step.
   - `docs/frontend.md`, `docs/auth0-setup.md`, `README.md`, `AGENTS.md`, `docs/architecture.md`, `docs/angular-migration-checklist.md`, `docs/target-client-architecture.md`, `docs/adr/ADR-012-angular-web-deferred-flutter.md`.

7. **C1 spec**
   - Update [sprint-c1-consumer-app-experience.md](./sprint-c1-consumer-app-experience.md): `apps/discover` → `apps/customer`, `build:discover` → `build:customer`, `@adeni/discover` → `@adeni/customer`; keep “Discover marketplace” wording for routes/copy.

8. **Validate**
   ```bash
   npm run typecheck
   npm test --workspace @adeni/customer
   npm run build:customer   # expect inherited _data-grid.scss budget failure unchanged
   dotnet test Adeni.slnx -c Release   # optional; rename should not touch API
   ```

9. **Do not commit** until the above pass (per team rule for this change).

---

## 4. File checklist (must-touch)

| File / area | Change |
|-------------|--------|
| `apps/customer/` (was `discover/`) | entire tree |
| `package.json` | workspaces + scripts |
| `package-lock.json` | workspace paths + nested keys |
| `apps/customer/package.json` | name, serve:ssr |
| `apps/customer/angular.json` | project id, outputPath, buildTarget |
| `apps/customer/.npmrc` | comment |
| `scripts/sync-brand-assets.mjs` | apps array |
| `.github/workflows/ci.yml` | build script |
| `docs/frontend.md` | dev command, paths |
| `docs/auth0-setup.md` | section paths; localhost **5190 unchanged** |
| `README.md`, `AGENTS.md` | app list |
| `docs/specs/sprint-c1-consumer-app-experience.md` | paths/scripts |
| `apps/customer/.env.example.md` | `npm run dev:customer` |

### Bulk reference update (ripgrep-driven)

Run after mechanical edits:

```bash
# Paths and npm (review each match — exclude discovery-api, DiscoveryController, /discover routes)
rg -l 'apps/discover|@adeni/discover|dev:discover|build:discover|dist/discover' docs .github package.json scripts
```

**Estimate:** ~**35–45 files** for strict workspace/build/path references; ~**120+ files** contain the substring `discover` (routes, i18n, API, components) — most **must not** change.

---

## 5. Risks and blockers

| Risk | Mitigation |
|------|------------|
| **Large uncommitted C1 batch** on `dev` (2026-10-29): many modified/deleted files under `apps/discover`, new `app-surface/`, loyalty/retention API in same tree | **Do not `git mv` until C1 is committed or rename is the only active task.** Otherwise conflicts and lost renames. |
| IDE / Angular cache | Delete `apps/customer/.angular/cache` if build paths break after rename. |
| `dist/discover` artifacts | Add `dist/customer` to `.gitignore` if needed; remove stale `dist/discover` locally. |
| Bookmarks / muscle memory | `localhost:5190` and public paths unchanged; only npm scripts and repo paths change. |
| Auth0 | Callback URLs unchanged; update internal docs and optional SPA display name. |
| Azure / GitHub Actions artifacts | Any deploy script referencing `dist/discover` must move with angular outputPath. |
| Parallel agents | Single owner for rename PR; others rebase onto rename before editing old paths. |

---

## 6. Optional follow-ups (out of scope here)

- Portal app rename is a separate checklist: [portal-to-business-rename.md](./portal-to-business-rename.md).
- Rename Auth0 SPA “Adeni Discover” → “Adeni Customer” in dashboard.

---

## 7. Status (2026-10-29)

| Item | State |
|------|-------|
| Spec written | Done |
| Mechanical rename in repo | **Done** — `apps/customer`, `@adeni/customer`, `dist/customer` |
| Tests after rename | Run `npm test --workspace @adeni/customer`, `npm run build:customer` |
