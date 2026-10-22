# Spec: Shared data grid (`@adeni/ui`)

| Field | Value |
|-------|-------|
| **Sprint** | Cross-cutting — Reporting UI + portal/admin list parity |
| **Author** | Team |
| **Status** | Approved — Phase 1 shipped on portal Staff (reference implementation) |
| **Created** | 2026-10-19 |

> **Authority:** Any portal/admin **list page** (Staff, Customers, Bookings, admin tables) should compose this toolkit — not bespoke toolbars, export buttons, or table markup. Staff is the **pilot**, not a separate pattern.

---

## 1. Goal (one sentence)

One **Carbon-aligned, shareable list/grid shell** in `@adeni/ui` for portal and admin: search, filter dropdowns, export menu, card/table switcher, sortable table, pills, and row actions — wired later to [Reporting](./reporting-module-boundaries.md) exports.

---

## 2. Toolkit map (`@adeni/ui`)

| Piece | Selector / export | Role |
|-------|-------------------|------|
| Toolbar shell | `adeni-list-toolbar` | Search, `[listFilters]`, count, `[listExports]`, Cards/List switcher |
| Filter field | `adeni-filter-select` | Carbon `<select>` + label (dropdown filters) |
| Export | `adeni-export-menu` | Carbon overflow-style **Export** → CSV / Excel / PDF |
| Table | `adeni-data-table` | Sortable headers, custom cells, actions column |
| Cell template | `[adeniDataTableCell]="columnId"` | Avatar stacks, pills, etc. |
| Actions template | `[adeniDataTableActions]` | Row action bar |
| Status tag | `adeni-pill` | Square **tag** (`shape="tag"`) in grids |
| Row control | `adeni-icon-btn` | Dense 32px; `variant="table"` or `danger-soft` |
| Person column | `adeni-table-person-cell` | Fixed 32px avatar + name stack (**use this**, not raw `<img>` in templates) |
| Sort helper | `sortDataRows()` | Same order for cards + table when needed |
| View persistence | `readPersistedListViewMode` / `persistListViewMode` | Per-route `storageKey` |
| Styles | `@adeni/ui/styles/data-grid` | Toolbar, table, filters, export menu (do **not** duplicate in app SCSS) |

Types: `AdeniDataTableColumn`, `AdeniListViewMode`, `AdeniExportMenuItem`, `AdeniFilterSelectOption`, `AdeniPillTone`, `AdeniIconBtnVariant`.

---

## 3. Canonical page recipe

Every list page follows the same structure:

```html
<adeni-list-toolbar
  [storageKey]="'<app>.<route>'"
  [(viewMode)]="viewMode"
  [count]="displayRows().length"
  [searchQuery]="search()"
  (searchQueryChange)="search.set($event)"
>
  <div listFilters class="adeni-filter-row">
    <adeni-filter-select label="…" [options]="…" [(value)]="filterA" />
    <adeni-filter-select label="…" [options]="…" [(value)]="filterB" />
  </div>
  <div listExports>
    <adeni-export-menu label="Export" [items]="exportItems" (exportSelect)="onExport($event)" />
  </div>
</adeni-list-toolbar>

@if (viewMode() === 'cards') {
  <!-- optional card layout -->
} @else {
  <adeni-data-table
    [rows]="filteredRows()"
    [columns]="columns"
    [trackRow]="trackRow"
    [(sortColumnId)]="sortColumnId"
    [(sortDirection)]="sortDirection"
  >
    <ng-template adeniDataTableCell="…" let-row>…</ng-template>
    <ng-template adeniDataTableActions let-row>
      <div class="adeni-row-action-bar">… adeni-icon-btn …</div>
    </ng-template>
  </adeni-data-table>
}
```

**Component logic (app):**

1. Load rows from API (tenant-scoped).
2. `filteredRows` = search + **filter dropdowns** (client V1).
3. `displayRows` = `sortDataRows(filteredRows, sortColumnId, sortDirection, columns)` when cards must match table order.
4. `exportItems`: three entries (`csv`, `xlsx`, `pdf`); disabled until Reporting API; `(exportSelect)` → `api-client` when live.

**Reference:** `apps/portal/src/app/pages/staff/staff.component.*`

---

## 4. Design rules (Carbon)

- **No rounded marketing chrome** on toolbar/table: `var(--cds-radius-none)`, flat surfaces, `--border` / `--cds-layer-02`.
- **Filters:** dropdowns (`adeni-filter-select`), not chip toggles, unless a spec exception is approved.
- **Export:** single **`adeni-export-menu`** (secondary button + menu), not separate format buttons.
- **Row actions:** all use **`adeni-icon-btn`** — `variant="table"` (neutral fill) or `variant="danger-soft"` (destructive). Use **`dense="true"`** in tables. Prefer semantic Carbon icons (e.g. `logout` revoke, `view--off` deactivate).
- **Do not** `@use "data-grid"` inside app page SCSS (bloats budgets); styles come from component `styleUrl`s.

---

## 5. Column & sort contract

```typescript
export type AdeniDataTableColumn<T> = {
  id: string;
  header: string;
  align?: "left" | "right";
  cell: (row: T) => string;
  sortable?: boolean;
  sortValue?: (row: T) => string | number;
};
```

- Header click toggles asc/desc when `sortable: true`.
- Custom render: `ng-template` with `[adeniDataTableCell]="id"` overrides `cell()` for that column.

---

## 6. Reporting export (Phase 2+)

| Screen | `reportKey` | Formats |
|--------|-------------|---------|
| Staff roster | `staff-roster` | csv, xlsx, pdf |
| Customers | `tenant-customers` | csv, xlsx |
| Bookings | `bookings-register` | csv, xlsx |
| Locations | `tenant-locations` | csv |
| Services | `tenant-services` | csv, xlsx |
| Staff roster | `staff-roster` | csv |

**Admin list exports** (MFA + audit `admin.reporting.read`; menu via `buildAdminReportExportMenuItems` — CSV + Excel only):

| Screen | `reportKey` | Formats | Query notes |
|--------|-------------|---------|-------------|
| Businesses | `admin-business-directory` | csv, xlsx | `q` matches list search |
| Pending verifications | `admin-pending-verifications` | csv, xlsx | `q` |
| Transactions | `admin-transactions` | csv, xlsx | `q`, `kind`, `from`, `to` |
| Pricing catalog entries | `admin-pricing-catalog-entries` | csv, xlsx | `catalogVersionCode` required |

`AdeniExportMenuItem`: `{ format, label, disabled?, hint? }` → tenant: `GET /api/v1/tenant/reporting/exports/{reportKey}?format=…`; admin: `GET /api/v1/admin/reporting/exports/{reportKey}?format=…`

---

## 7. Phases

### Phase 1 — Toolkit + Staff pilot ✅

- [x] All toolkit components in `@adeni/ui` + `styles/data-grid`
- [x] Staff: filters, export menu, sort, table actions, card/list switcher

### Phase 2 — Admin + more portal lists

- [ ] One admin page migrated off raw `.data-table` + `_admin-list-page.scss` duplicate
- [ ] Customers, Bookings use **same recipe** (copy Staff wiring, not markup)
- [ ] Enable export menu items via Reporting API

### Phase 3 — Server-backed

- [ ] Pagination, server sort/filter query params
- [ ] Optional date-range filter component

---

## 8. Anti-patterns

- Custom export button groups per page
- Filter chip rows when dropdown spec applies
- Ghost/icon mix without `table` / `danger-soft` variants
- Inline `<table class="data-table">` in new portal pages
- Staff-only SCSS for grid controls (use `adeni-icon-btn` + shared components)
- Raw avatar `<img>` in `adeniDataTableCell` templates (styles won’t apply — use `adeni-table-person-cell`)

---

## 9. Related docs

- [reporting-module-boundaries.md](./reporting-module-boundaries.md)
- [reporting.md](../reporting.md)
- [shared-staff-data-grid.md](./shared-staff-data-grid.md) — legacy filename pointer
