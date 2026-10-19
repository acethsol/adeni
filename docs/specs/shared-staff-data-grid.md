# Spec: Shared staff list shell & data grid (`@adeni/ui`)

| Field | Value |
|-------|-------|
| **Sprint** | Cross-cutting — Reporting UI + portal/admin parity |
| **Author** | Team |
| **Status** | Approved — Phase 1 pilot (Staff portal) in progress |
| **Created** | 2026-10-19 |

---

## 1. Goal (one sentence)

Give **portal and admin** a reusable **list shell** (search, filters slot, card/table switcher, export slot) and **data table** so roster-style pages can switch from cards to a sortable grid and later hook **Reporting** exports without duplicating markup.

---

## 2. Context

- [reporting-module-boundaries.md](./reporting-module-boundaries.md) — server exports (`IReportExportService`); this spec is **client presentation**
- [notifications-module-boundaries.md](./notifications-module-boundaries.md) — unrelated to grid; no overlap
- Admin today: copy-paste `.data-table` in `_admin-list-page.scss`
- Portal **Staff** today: card grid only (`staff.component.html`)
- **Pilot:** Portal `/staff` card ↔ table switcher

---

## 3. In scope

### Phase 1 — `@adeni/ui` primitives + Staff pilot

- [x] This spec approved
- [x] `AdeniListToolbarComponent` — pill count, rounded search + clear, filter chip slot, segmented view switcher, export pill slot
- [x] `AdeniDataTableComponent` — column defs, `adeniDataTableCell` templates, `adeniDataTableActions`, sticky header shell
- [x] `AdeniPillComponent`, `AdeniIconBtnComponent` — status chips and compact row actions
- [x] `AdeniListViewMode` + `readPersistedListViewMode` / `persistListViewMode` (localStorage per route key)
- [x] Shared SCSS export `@adeni/ui/styles/data-grid` (toolbar + table; aligned with admin `.data-table`)
- [x] Portal **Staff** — toolbar, client-side name/role filter, table columns, persisted `portal.staff` view key
- [x] Export toolbar buttons **disabled** with title “Reporting export — coming soon” until `GET /api/v1/tenant/reporting/exports/*` ships

### Phase 2 — Admin + more portal lists

- [ ] Migrate one admin page (e.g. customers) to shared table + toolbar
- [ ] Portal **Customers**, **Bookings** list modes
- [ ] Wire CSV/Xlsx/PDF to Reporting export API when `reportKey` defined per screen

### Phase 3 — Server-backed grid features

- [ ] Pagination, sort query params, column visibility (optional)
- [ ] Shared filter bar component (date range, status chips) driven by `@adeni/shared` filter schemas

---

## 4. Out of scope

- DevExpress / AG Grid / Carbon Data Table package import (plain HTML table + tokens for V1)
- Inline editing in grid cells
- Discover consumer UI
- Replacing dashboard **widget** layout grid

---

## 5. Component API (target)

### `AdeniListToolbarComponent`

| Input | Type | Notes |
|-------|------|--------|
| `storageKey` | `string` | localStorage key for view mode; omit to disable persistence |
| `viewMode` | `'cards' \| 'table'` | Two-way via `viewModeChange` |
| `count` | `number` | Row count after filters |
| `countLabel` | `string` | e.g. `teammate` / `teammates` |
| `searchPlaceholder` | `string` | Optional |
| `searchQuery` | `string` | Two-way via `searchQueryChange` |
| `showViewSwitcher` | `boolean` | default `true` |
| `showSearch` | `boolean` | default `true` |

**Content slots:** `[listFilters]`, `[listExports]`

### `AdeniDataTableComponent<T>`

| Input | Type | Notes |
|-------|------|--------|
| `rows` | `T[]` | |
| `columns` | `AdeniDataTableColumn<T>[]` | `cell: (row) => string` |
| `trackRow` | `(row: T) => string` | |
| `rowClass` | `(row: T) => string \| undefined` | e.g. inactive row |

**Template:** `ng-template adeniDataTableActions let-row` → actions column

### Types (`adeni-list-view.types.ts`)

```typescript
export type AdeniListViewMode = "cards" | "table";

export type AdeniDataTableColumn<T> = {
  id: string;
  header: string;
  align?: "left" | "right";
  cell: (row: T) => string;
};
```

---

## 6. Reporting export integration (Phase 2+)

| Screen | `reportKey` (planned) | Formats |
|--------|------------------------|---------|
| Staff roster | `staff-roster` | csv, xlsx |
| Customers | `tenant-customers` | csv, xlsx |
| Bookings register | `bookings-register` | csv, xlsx, pdf |
| Compensation | `compensation` | delegate to Finance export initially |

Client pattern:

```typescript
// api-client (future)
getReportingExport(reportKey, { from, to, format })
```

Toolbar `[listExports]` hosts format buttons; permission-gated per route (`portal.finance`, etc.).

---

## 7. Files to touch

| Layer | Files |
|-------|-------|
| Spec | This file |
| `@adeni/ui` | `adeni-list-toolbar.component.ts`, `adeni-data-table.component.ts`, `adeni-list-view.types.ts`, `styles/_data-grid.scss`, `index.ts`, `package.json` exports |
| Portal | `apps/portal/.../staff/staff.component.*` |
| Admin | Phase 2 — `apps/admin/...` |
| Docs | [reporting.md](../reporting.md), [sprints.md](../sprints.md) |

---

## 8. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Grid displays API data only; exports must pass tenant scope server-side |
| **Auth** | Export buttons respect portal permissions when wired |
| **PII** | Search/filter client-side on already-loaded lists in Phase 1; server search later for large tenants |
| **a11y** | View switcher as `role="group"` with `aria-pressed`; table `scope="col"` |

---

## 9. Acceptance criteria (Phase 1)

- [x] Staff page toggles cards ↔ table; preference survives reload
- [x] Table shows name, role, title, bookable status, portal access, row actions comparable to cards
- [x] Search filters roster by display name, role label, title
- [x] Styles importable from `@adeni/ui/styles/data-grid` for admin convergence
- [x] No duplicate export implementation until Reporting Phase 3 API exists

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| Carbon Data Table npm package? | No for V1 — lightweight table + tokens |
| Sort in Phase 1? | Client sort on displayed columns optional; Staff pilot may skip sort until Phase 3 |
| Move admin `_admin-list-page.scss` into `@adeni/ui`? | Phase 2 — re-export from data-grid SCSS |
