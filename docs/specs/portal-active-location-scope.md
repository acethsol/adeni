# Spec: Portal active location scope (multi-branch)

| Field | Value |
|-------|-------|
| **Sprint** | 38–39 launch closeout / location gap |
| **Author** | Agent |
| **Status** | Implemented |
| **Created** | 2026-10-22 |

---

## 1. Goal (one sentence)

Make the portal **branch switch** drive tenant APIs and exports honestly — filter bookings, dashboard, queue, and reporting by optional `locationId`, with booking/queue rows tied to a branch.

---

## 2. Context

- [sprint-38-39-launch-closeout.md](./sprint-38-39-launch-closeout.md) **R-U3** — shell branch switch shipped; data paths were incomplete.
- Module(s): Tenancy (`BusinessLocation`), Booking, Dashboard/Reporting, Portal, Discover booking create.

**Gap (pre-fix):** `TenantReportExportQueryService` ignored `locationId`; dashboard/bookings/list APIs had no branch filter; `BookingRecord` had no branch FK.

---

## 3. In scope

### Phase 1 (P0) — ship

- [x] Honor `locationId` on **bookings-register**, **sales-summary**, **compensation** export queries.
- [x] Optional `locationId` on `GET /api/v1/tenant/dashboard/{summary,series,breakdown}`.
- [x] Portal passes active branch; scope subtitle on dashboard/bookings when multi-branch.
- [x] Integration tests for export + dashboard location filter.

### Phase 2 (P1) — ship

- [x] `BusinessLocationId` on `BookingRecord` (+ SQL script + dacpac table).
- [x] Discover create booking persists `businessLocationId` from public profile.
- [x] `GET /api/v1/tenant/bookings?locationId=`.
- [x] `BusinessLocationId` on `QueueTicket`; list/floor filter; join-by-slug sets branch.
- [x] `@adeni/api-client` + portal: active location query param; bookings list; queue/calendar reload on `selectionChanged$`; shell pending badge scoped when filter applies.

### Phase 3 (P2) — backlog

- [ ] Staff↔branch assignment (portal + domain).
- [ ] Reviews attributed via booking branch (discovery aggregates per location already slug-scoped).
- [ ] Tenant customer export `locationId` (visit history per branch).
- [ ] Per-branch weekly hours (today: tenant-level hours on profile).

---

## 4. Out of scope

- Per-branch entitlements or billing SKUs.
- Elasticsearch / discovery index changes.
- Kiosk PIN flow redesign (slug on kiosk URL remains follow-up).

---

## 5. API contract

| Method | Route | Auth | Query | Description |
|--------|-------|------|-------|-------------|
| GET | `/api/v1/tenant/dashboard/summary` | Portal overview | `from`, `to`, **`locationId?`** | Counts/metrics for one branch or all (omit = all). |
| GET | `/api/v1/tenant/dashboard/series` | Portal overview | `metric`, `from`, `to`, **`locationId?`** | Time series scoped by branch when set. |
| GET | `/api/v1/tenant/dashboard/breakdown` | Portal overview | `dimension`, `from`, `to`, **`locationId?`** | Breakdown scoped by branch when set. |
| GET | `/api/v1/tenant/bookings` | Bookings / self | `tab`, `page`, …, **`locationId?`** | Paginated list filtered by branch. |
| GET | `/api/v1/tenant/queue` | Bookings / self | **`locationId?`** | Floor board for branch. |
| GET | `/api/v1/tenant/reporting/exports/{key}` | Per report | `from`, `to`, **`locationId?`** | Existing gate: multi-branch tenants without entitlement must send `locationId`. |
| POST | `/api/v1/bookings` | Customer JWT | body **`businessLocationId?`** | Persist branch on create (must belong to tenant). |

**Tiers**

| Capability | Starter / Pro (no `multiLocationReporting`) | Pro+ / Enterprise with `multiLocationReporting` |
|------------|---------------------------------------------|--------------------------------------------------|
| Export all branches | Blocked (403) when 2+ active locations | Omit `locationId` = all branches |
| Portal “All locations” | Hidden in switch | Available; APIs omit `locationId` |
| Dashboard/bookings list | Always single-branch filter (active or primary) | Branch or all |

---

## 6. Portal route matrix

| Route | Uses active `locationId` query | Scope subtitle |
|-------|-------------------------------|----------------|
| `/dashboard` | summary, series, breakdown | Yes (multi-branch) |
| `/bookings` | list | Yes (multi-branch) |
| `/queue` | floor | Reload on switch (filter when API param sent) |
| `/payments` (exports) | export (existing) | — |
| Other list pages | export only (P2: customers) | — |

---

## 7. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `BookingRecord`, `QueueTicket` |
| Application | `ITenantDashboardService`, `IBookingService`, `IQueueService`, `ITenantReportExportQueryService`, `IStaffCompensationService`, `CreateBookingRequest` |
| Infrastructure | `TenantDashboardService`, `TenantReportExportQueryService`, `BookingService`, `QueueService`, `StaffCompensationService`, `AdeniDbContext`, `TenantLocationScope` |
| Api | `TenantDashboardController`, `TenantBookingsController`, `TenantQueueController` |
| DB | `db/Adeni.Database/booking/Tables/bookings.sql`, `queue_tickets.sql`, `db/scripts/booking-location-scope.sql` |
| Tests | `TenantReportingExportIntegrationTests`, `TenantDashboardIntegrationTests` |
| Shared / client / portal | `createBookingRequestSchema`, `report-export-location`, `api-client`, dashboard/bookings/queue/shell |

---

## 8. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | `locationId` validated against tenant active locations; 400 when unknown. |
| **Auth / roles** | Unchanged permission gates. |
| **Cache** | None. |
| **Audit** | None. |
| **PII** | Unchanged. |

---

## 9. Tests

- [x] Export bookings-register with `locationId` returns subset.
- [x] Dashboard summary respects `locationId`.
- [ ] Booking create persists location (unit — optional).
- Run: `dotnet test Adeni.slnx -c Release`, shared vitest, portal typecheck.

---

## 10. Acceptance criteria

### P0

- [x] Changing branch changes dashboard metrics (not only exports).
- [x] Bookings register CSV differs by `locationId` in integration test.

### P1

- [x] New bookings from Discover store `BusinessLocationId`.
- [x] Tenant bookings list filters by `locationId`.
- [x] Queue join via location slug stores branch on ticket.

### P2 (deferred)

- Documented in § Phase 3 backlog.

---

## 11. Open questions

| Question | Decision |
|----------|----------|
| Payments without `BookingId` in collected series when scoped? | Exclude from branch-scoped collected totals. |
| Legacy rows with null `BusinessLocationId`? | Backfill to primary; branch filter matches only explicit FK. |
