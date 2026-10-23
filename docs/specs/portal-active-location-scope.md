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

**Gap (post–Phase 3 closeout, fixed on dev):**

| Area | Issue | Fix |
|------|--------|-----|
| Queue staff picker | `listTenantStaff` without `locationId` | Portal passes active branch |
| Plan / bookings usage | `getTenantSubscriptionUsage` tenant-wide | Optional `locationId` on subscription API + portal |
| Staff copy business hours | `getTenantAvailability()` ignored branch | Active branch on copy |
| Payments ledger | Ledger API had no branch filter | `GET .../finance/ledger?locationId=` + portal |
| Customer detail | History ignored branch | `GET .../customers/{id}?locationId=` + reload on switch |
| Messaging | Inbox / unread / dashboard widget unscoped | `locationId` on conversations + unread (booking-context filter) |
| Quotes | No `BusinessLocationId` on `QuoteRequestRecord` | `BusinessLocationId` on quotes + `GET .../tenant/quotes?locationId=` + portal reload |
| List APIs (2+ branches, no `multiLocationReporting`) | Omitted `locationId` returned all rows | `TenantLocationScope.ResolveListLocationAsync` defaults to primary |
| Kiosk | All-branch arrivals | `?locationId=` from bookings “Open kiosk” + scoped list |
| Staff roster | Unassigned + wrong-branch staff visible; create without branch | Strict roster filter; create defaults to active branch; all staff API calls pass `locationId` |

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

### Phase 3 (P2) — ship

- [x] Staff↔branch assignment (`StaffMember.BusinessLocationId`; portal roster filter + form field; `GET /api/v1/tenant/staff?locationId=`).
- [x] Reviews attributed via booking branch (public profile reviews use active branch slug; tenant list supports `locationId` via booking FK).
- [x] Tenant customer export + list `locationId` (customers with bookings or queue visits at branch).
- [x] Per-branch weekly hours + closed dates (`BusinessLocationId` on `weekly_availability` / `business_closed_dates`; `GET`/`PUT` `/api/v1/tenant/availability?locationId=`; portal Hours reloads on branch switch).
- [x] Calendar APIs: optional `locationId` on `GET .../staff/{id}/calendar` and `GET .../staff/team-calendar`; portal calendars pass active branch.
- [x] Branch switch loading panel via `PortalActiveLocationService.runOnBranchSwitch` + `AdeniGlobalLoadingPanelComponent`.
- [x] Wider branch switch trigger (min/max width + `title` on truncated labels).

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
| GET | `/api/v1/tenant/staff` | Staff | **`locationId?`** | Roster: assigned branch or unassigned (all branches). |
| GET | `/api/v1/tenant/staff/team-calendar` | Staff | `from`, `to`, **`locationId?`** | Team calendar bookings/staff scoped by branch. |
| GET | `/api/v1/tenant/staff/{id}/calendar` | Staff / self | `from`, `to`, **`locationId?`** | Practitioner calendar bookings scoped by branch. |
| GET | `/api/v1/tenant/customers` | Bookings | …, **`locationId?`** | Customers with activity at branch. |
| GET | `/api/v1/tenant/reviews` | Portal | **`locationId?`** | Reviews whose booking is at branch. |
| GET | `/api/v1/tenant/availability` | Hours | **`locationId?`** | Weekly rules for branch (defaults to primary when omitted). |
| PUT | `/api/v1/tenant/availability` | Hours | **`locationId?`** | Replace weekly rules for branch. |
| GET | `/api/v1/tenant/closed-dates` | Hours | **`locationId?`** | Closed ranges for branch. |
| POST | `/api/v1/tenant/closed-dates` | Hours | **`locationId?`** | Add closed range for branch. |
| GET | `/api/v1/tenant/reporting/exports/{key}` | Per report | `from`, `to`, **`locationId?`** | Existing gate: multi-branch tenants without entitlement must send `locationId`. |
| GET | `/api/v1/finance/ledger` | Finance | `tenantId`, `page`, `pageSize`, **`locationId?`** | Ledger rows tied to bookings at branch; unlinked payments omitted when scoped. |
| GET | `/api/v1/tenant/messages/conversations` | Messages | `page`, `pageSize`, **`locationId?`** | Booking-context threads filtered by booking branch; profile/quote threads unchanged. |
| GET | `/api/v1/tenant/messages/unread-count` | Messages | **`locationId?`** | Same scope as conversation list. |
| GET | `/api/v1/tenant/customers/{id}` | Bookings | **`locationId?`** | Recent bookings/queue history scoped to branch. |
| GET | `/api/v1/tenant/subscription` | Plan / overview | **`locationId?`** | Monthly booking usage counted at branch when set. |
| GET | `/api/v1/tenant/quotes` | Quotes | **`locationId?`** | Quote inbox filtered by branch when set. |
| POST | `/api/v1/bookings` | Customer JWT | body **`businessLocationId?`** | Persist branch on create (must belong to tenant). |

**Tiers**

| Capability | Starter / Pro (no `multiLocationReporting`) | Pro+ / Enterprise with `multiLocationReporting` |
|------------|---------------------------------------------|--------------------------------------------------|
| Export all branches | Blocked (403) when 2+ active locations | Omit `locationId` = all branches |
| Portal “All locations” | Hidden in switch | Available; APIs omit `locationId` |
| Dashboard/bookings list | Always single-branch filter (active or primary) | Branch or all |

---

## 6. Portal route matrix

| Route | Uses active `locationId` query | Scope subtitle / reload on switch |
|-------|-------------------------------|-----------------------------------|
| `/dashboard` | summary, series, breakdown | Yes (multi-branch) |
| `/bookings` | list | Yes (multi-branch) |
| `/queue` | floor | Yes |
| `/calendar`, `/my-calendar`, `/staff/:id/calendar` | team/staff calendar | Yes |
| `/staff` | roster list | Yes |
| `/customers` | list + export | Yes |
| `/profile?tab=reviews` | public reviews by active branch slug | Yes |
| `/payments` (exports) | export (existing) | Yes |
| `/finance` (ledger tab) | ledger list | Yes |
| `/messages` | conversations + unread | Yes |
| `/plan` | subscription usage | Yes |
| `/customers/:id` | detail history | Yes |
| `/kiosk` | arrivals list | Yes (`locationId` query from bookings) |
| `/quotes` | list | Yes |
| Shell pending bookings badge | pending list | Yes |
| Shell messages badge | unread count | Yes |
| `/availability` | hours + closed dates | Yes (branch picker when “All locations”) |

---

## 7. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `BookingRecord`, `QueueTicket`, `StaffMember`, `QuoteRequestRecord` |
| Application | `IStaffService`, `IReviewService`, `ITenantCustomerService`, `ITenantReportExportQueryService`, … |
| Infrastructure | `StaffService`, `ReviewService`, `TenantCustomerService`, `TenantReportExportQueryService`, `TenantLocationScope` |
| Api | `TenantStaffController`, `TenantCustomersController`, `TenantReviewsController` |
| DB | `staff_members.sql`, `quote_requests.sql`, `db/scripts/staff-location-scope.sql`, `db/scripts/quote-location-scope.sql` |
| Tests | `StaffServiceTests`, export integration tests |
| Shared / client / portal | `schemas.ts`, `api-client`, branch switch + `PortalActiveLocationService`, page reload subscribers |

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
- [x] Tenant quotes list respects `locationId`.
- [x] Staff roster list respects branch assignment + `locationId` query.
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

### P2

- [x] Staff branch assignment and roster filter.
- [x] Calendar + customers + reviews honor active branch.
- [x] Global loading panel during branch-switch refetch.

---

## 11. Open questions

| Question | Decision |
|----------|----------|
| Payments without `BookingId` in collected series when scoped? | Exclude from branch-scoped collected totals. |
| Legacy rows with null `BusinessLocationId`? | Backfill to primary; branch filter matches only explicit FK. |
| Per-branch business hours? | Shipped — `BusinessLocationId` on weekly + closed date rows; migration duplicates legacy rows per active branch. |
