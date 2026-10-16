# Spec: Sprint 32 — Business dashboard (Zenoti-style)

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 32 — Business dashboard |
| **Status** | In progress (32a) |
| **Created** | 2026-10-15 |
| **Updated** | 2026-10-15 |
| **Blocks (minimal)** | [sprint-33-saas-subscription-billing-mrr.md](./sprint-33-saas-subscription-billing-mrr.md) needs only **32a** (plan strip + honest KPIs) |

---

## 1. Goal (one sentence)

Replace the static portal home with a **widget-based, chart-rich dashboard** owners can **customize** (add, remove, reorder, resize) — Zenoti-inspired operations center, scoped to Adeni v1 data (bookings, payments, queue, reviews), not full spa ERP.

---

## 2. Context

### Today

`apps/portal/src/app/pages/dashboard/` — hero, plan meter, four KPI tiles, simple bar chart (week counts), booking mix strip, shortcut grid. **No** server-side analytics aggregates, **no** user layout persistence, **no** date-range control.

Sprint 16 **16d** (analytics v2) is marked done in [sprints.md](../sprints.md) but **not surfaced** on the live dashboard — treat as **product debt**; this sprint delivers the UI + API those metrics need.

### Zenoti reference (patterns, not parity)

Zenoti’s owner dashboard emphasizes **at-a-glance KPIs**, **time-series charts**, **utilization**, and **personalized layouts** per role. Adeni should match the **UX pattern** (composable widgets + graphs + “edit dashboard”) while staying in [product-direction.md](../product-direction.md) scope:

| Zenoti-like | Adeni v1 |
|-------------|----------|
| Custom widget grid | ✅ Target |
| Revenue / sales charts | ✅ From **payment ledger** + completed bookings (not list-price fiction) |
| Appointment volume / funnel | ✅ Booking statuses |
| Staff utilization / productivity | ✅ Phase 32d (Business tier + staff data) |
| Memberships, payroll, inventory | ❌ Out of scope |
| 50+ report packs | ❌ Defer; widget catalog grows incrementally |

Competitor note: [roadmap-modules.md §5](../roadmap-modules.md) — Zenoti wins on deep OS; Adeni wins on trust + floor + orchestrated payments. Dashboard closes the **“owner today”** gap without becoming BI ERP.

### Entitlements

- **`analytics`** (Professional+ SKU): advanced widgets (trends, funnel, repeat rate, service mix, collected revenue).
- **Trialing** tenants receive the **trial SKU’s** entitlements (often Professional) — not a separate “free layout.”
- **Starter** (entry paid SKU): slimmer default widget set; customize rules in §12.
- **Capabilities** (`quotes`, `walk_in_queue`, `deposits`): widget availability, same as nav — hidden if not applicable.

### Relationship to Sprint 33

- **32a** — quick fixes + plan strip → enough for billing UX.
- **32b–32d** — full dashboard program; can run **in parallel** with Sprint 33 after 32a ships.

---

## 3. Phased delivery

### Phase 32a — Foundation (ship first)

- [ ] Fix misleading **“Confirmed” / revenue** KPI (ledger-backed **Collected** when payments enabled; else honest **Booked value (list price)** label).
- [ ] **Plan usage** strip with link to `/plan`, near-limit messaging (`@adeni/shared` helpers).
- [ ] **Entitlement-aware** shortcuts / remove always-on Messages link.
- [ ] Practitioner vs owner **default view** (existing session permissions).

### Phase 32b — Widget platform + default layout

- [ ] **Dashboard shell**: responsive grid (12-col), widget chrome (title, optional range, loading/error).
- [ ] **Edit mode**: toggle “Customize dashboard”; drag-and-drop reorder (CDK or lightweight grid library); add widget picker; reset to default.
- [ ] **Global date range** control: Today | 7d | 30d | Month to date (persist with layout).
- [ ] **Charting**: Carbon Charts (align with `@adeni/ui` / Carbon patterns) for line, bar, donut.
- [ ] **Default owner layout** (widgets below) pre-installed for new tenants.

### Phase 32c — Persist layout

- [ ] Store layout JSON per **BusinessUser** (preferred) or per tenant role template.
- [ ] API: `GET/PUT /api/v1/tenant/dashboard/layout` — tenant-scoped, auth required.
- [ ] Schema version field for widget migrations.

### Phase 32d — Widget catalog expansion

- [ ] Additional widgets after core ship (staff leaderboard, queue depth, quote pipeline, messages unread, verification status).
- [ ] Optional **role templates** (owner / manager / front desk) — pick layout preset.

---

## 4. Widget catalog (v1)

Each widget: stable `id`, `minTier` / `requiredEntitlements[]`, `requiredCapabilities[]`, `defaultSize`, data source.

| Widget id | Title | Visualization | Data source | Gate |
|-----------|--------|---------------|-------------|------|
| `plan_usage` | Plan & usage | Meter + link | `GET /tenant/subscription` | All |
| `kpis_row` | Key numbers | Stat tiles | Aggregates API | All (subset by role) |
| `today_schedule` | Today’s chair | List | Bookings (today) | `calendar` |
| `pending_inbox` | Needs response | Count + link | Bookings pending | `calendar` |
| `week_appointments` | This week | Bar chart | Bookings by day | `calendar` |
| `booking_mix` | Booking mix | Stacked bar / legend | Status counts | `calendar` |
| `collected_revenue` | Collected | Line + total | Payment ledger aggregates | `deposits` + **analytics** |
| `booked_value` | Booked (list) | Line + total | Bookings × service prices | **analytics** |
| `service_mix` | Top services | Donut | Bookings by service | **analytics** |
| `booking_funnel` | Funnel | Funnel/bar | Status transitions | **analytics** |
| `repeat_clients` | Returning clients | Stat + sparkline | Distinct customer ids | **analytics** |
| `rating_summary` | Reputation | Stat | Public profile reviews | All |
| `queue_now` | Walk-in queue | List/count | Queue tickets | `walk_in_queue` |
| `quick_actions` | Run the floor | Link grid | Static + gates | All |

Widgets not entitled: **omit from picker** and drop from saved layout on load (no upgrade teasers on dashboard — consistent with portal gating).

---

## 5. API contract (new — aggregates)

Avoid loading full booking history for charts at scale.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/dashboard/summary` | JWT + tenant + `portal.overview` | KPI bundle for range query `?from=&to=` |
| `GET` | `/api/v1/tenant/dashboard/series` | Same | Time series: `metric=appointments\|collected\|booked_value` |
| `GET` | `/api/v1/tenant/dashboard/breakdown` | Same | `dimension=service\|status\|staff` |
| `GET` | `/api/v1/tenant/dashboard/layout` | Same | User layout JSON |
| `PUT` | `/api/v1/tenant/dashboard/layout` | Same | Save layout JSON |

**Entitlements:** endpoints for analytics metrics return **403** or empty contract with `code: entitlements.analytics_required` when tier lacks `analytics` (prefer 403 for series/breakdown; summary returns only non-gated KPIs).

Implement in **Application** port `ITenantDashboardService` + **Infrastructure** SQL aggregates (indexed on `tenant_id`, `start_at`, payment `completed_at`).

---

## 6. Layout JSON (sketch)

```json
{
  "version": 1,
  "range": "7d",
  "widgets": [
    { "id": "plan_usage", "x": 0, "y": 0, "w": 12, "h": 1 },
    { "id": "kpis_row", "x": 0, "y": 1, "w": 12, "h": 1 },
    { "id": "collected_revenue", "x": 0, "y": 2, "w": 8, "h": 2 },
    { "id": "today_schedule", "x": 8, "y": 2, "w": 4, "h": 2 }
  ]
}
```

---

## 7. Files to touch (expected)

| Layer | Files |
|-------|-------|
| Domain | Dashboard layout entity (optional value object) |
| Application | `ITenantDashboardService`, DTOs, widget registry constants |
| Infrastructure | Aggregate queries, layout persistence (`tenancy.business_user_dashboard_layouts` or JSON column) |
| Api | `TenantDashboardController` |
| Tests | Entitlement denial, tenant isolation, aggregate correctness |
| packages/shared | Widget ids, layout schema (Zod), metric enums |
| packages/api-client | Dashboard methods |
| packages/ui | `DashboardWidgetHost`, chart wrappers (if shared) |
| apps/portal | Replace `dashboard/*` with widget grid + edit mode |

---

## 8. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All aggregates filtered by tenant |
| **Permissions** | Practitioners: scoped widgets (my bookings, my calendar) — 32d |
| **Cache** | `tenant:{id}:dashboard:summary:{range}` short TTL; invalidate on booking/payment write |
| **PII** | Aggregates only; widget lists follow existing booking DTO rules |
| **Performance** | No full-table scan on portal load |

---

## 9. Out of scope

- Accounting exports, tax reports, payroll, inventory (Zenoti ERP depth).
- Embedded Power BI / Metabase.
- Consumer-facing analytics.
- Admin GMV/MRR (Sprint 20e / 33).
- AI-generated insights (Sprint 20 agent) — future widget type.

---

## 10. Acceptance criteria

**32a**

1. Revenue KPI honest or ledger-backed.
2. Plan strip + `/plan`; gated shortcuts.

**32b–32c**

3. Owner sees **≥3 charts** on default layout (week appointments + one analytics chart on Pro).
4. **Customize** flow: remove widget, add from picker, reorder, persists after refresh.
5. Free tenant cannot add analytics widgets; layout reset works.
6. Practitioner sees reduced default layout without owner-only financial widgets.

**32d**

7. At least two optional widgets shipped from backlog list.

---

## 11. UX notes

- **Edit dashboard** entry: top-right on home (Zenoti-like “Customize”).
- **Empty widget**: `adeni-portal-empty-state` embedded.
- **Hero**: optional — compress business name into shell title or keep slim welcome row; avoid duplicating Profile.
- Mobile: single-column stack; edit mode may be desktop-first v1 (document if so).

---

## 12. Product decisions (fill before 32b)

| # | Question | Decision |
|---|----------|----------|
| 1 | Starter SKU: lock layout entirely or allow reorder of starter widgets only? | _TBD_ |
| 2 | Global date range vs per-widget range? | _TBD_ (default: global) |
| 3 | Hero band: keep vs remove? | _TBD_ |
| 4 | Practitioner financial widgets: never vs scoped “my collected”? | _TBD_ |
| 5 | Chart library: Carbon Charts only vs add lightweight alternative? | _TBD_ (Carbon recommended) |

---

## 13. Smoke checklist

- [ ] Starter (or trialing Pro): default layout; analytics widgets only if SKU/trial entitles
- [ ] Pro: add `collected_revenue`, chart loads for 7d
- [ ] Customize persist across logout
- [ ] Queue widget only when `walk_in_queue` capability
- [ ] Cross-tenant layout API denied

---

## Related

- [sprint-33-saas-subscription-billing-mrr.md](./sprint-33-saas-subscription-billing-mrr.md)
- [product-strategy.md §4.3](../product-strategy.md) — Insights tier / financial analytics (later add-on widgets)
- Sprint 16d reconciliation — implement aggregates promised in sprint history
