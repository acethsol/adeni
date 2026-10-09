# Spec: Staff ops depth (identity, hours, leave, calendar)

> Standing play 1 — [AGENTS.md](../../AGENTS.md). Do not write code until this spec is reviewed.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 24 — Staff ops |
| **Author** | Agent + product |
| **Status** | Implemented (24a–24d) |
| **Created** | 2026-10-08 |

---

## 1. Goal (one sentence)

Give salon/spa owners a practical staff ops layer — identity, roles, working hours, leave, and a staff calendar that drives booking slots — without building payroll or a finance ledger.

---

## 2. Context

- Sprint 23 shipped an owner-managed roster (`StaffMember` + service links + optional booking pick). Spec: [sprint-23-staff-cart-guests.md](./sprint-23-staff-cart-guests.md).
- Product loop: services → **practitioner** → **their availability** → book ([product-direction.md](../product-direction.md)).
- Strategy: staff management was backlog; **payroll / POS / inventory = Phase 4+**; financing referrals = later; **no wallet / custody** ([product-strategy.md](../product-strategy.md) §4.3, §8).
- Payments today = Paystack **orchestration** (deposits, SaaS checkout) — not staff pay, tips pools, or commissions.
- Module home: **Booking** (staff + availability). Remuneration/tips stay **out** until a future Payments/Finance phase (not started).

**Recommended slices (implement in order)**

| Slice | Name | Outcome |
|-------|------|---------|
| **24a** | Identity & roles | First/last name, display name, role labels; portal form polish |
| **24b** | Working hours | Per-staff weekly hours (override business hours); slots use staff hours when assigned |
| **24c** | Leave / time off | Date-range leave blocks; slots exclude leave |
| **24d** | Staff calendar (portal) | Week/day view of bookings + leave + hours for one staff member |

---

## 3. In scope

### Slice 24a — Identity & roles

- [x] Split identity: `FirstName`, `LastName` (required for create); keep `DisplayName` (default `"First Last"`, editable for public booking label)
- [x] Role: structured `roleKey` from a small beauty/wellness set (`stylist`, `barber`, `nail_tech`, `esthetician`, `therapist`, `receptionist`, `other`) + optional free-text `Title` (already exists) for custom label
- [x] Portal staff form: first/last, display name, role, title, bio, services, active
- [x] Public staff API: still **display name + title/role label only** (no first/last PII on anonymous discovery)
- [x] Migration + shared Zod + portal/Discover labels (EN/FR)

### Slice 24b — Working hours

- [x] `StaffWeeklyAvailabilityRule` (or reuse pattern from business weekly rules): `StaffMemberId`, `DayOfWeek`, `StartTime`, `EndTime`, optional `IsOff`
- [x] Portal: edit weekly hours per staff (copy-from-business shortcut)
- [x] Availability: when booking has `staffMemberId`, intersect **business hours ∩ staff hours**; “Any available” = union of eligible staff windows (existing capacity logic, hours-aware)
- [x] Cache invalidation on staff hours write (same family as availability keys)

### Slice 24c — Leave / time off

- [x] `StaffLeave` entity: `StaffMemberId`, `StartAt`, `EndAt`, `Reason` (optional short text, owner-only), `CreatedAt`
- [x] Portal: add/list/cancel leave ranges
- [x] Slots + conflict check: staff cannot be booked overlapping leave
- [x] Soft validation: warn if existing bookings fall inside new leave (list conflicting booking ids; owner confirms cancel/reassign later — no auto-cancel in 24)

### Slice 24d — Staff calendar (portal)

- [x] Portal route e.g. `/staff/:id/calendar` (or tab on staff detail)
- [x] **Custom month/week/day calendar** (no FullCalendar / 3rd-party) — hallmark floor board: month agenda grid, timed week/day blocks, leave bands, off-hour shading, now line, detail rail
- [x] Month / week / day views: confirmed/pending bookings + leave (+ hours inheritance note); tap day → day timeline; tap block → detail rail
- [x] Read-only from booking module list filtered by `staffMemberId` (+ leave API); no drag-reschedule in 24
- [x] Deep-link from staff list / edit form → calendar

## 4. Out of scope (explicit)

| Topic | Why deferred |
|-------|----------------|
| **Payroll / salary / commission schemes** | Strategy: payroll until Phase 4+; needs finance module we do not have |
| **Tips (collection, pooling, payout)** | Ties to Payments custody/settlement; Paystack orchestration only today |
| **Clock-in / time tracking / attendance** | HR suite creep |
| **Staff Auth0 logins / employee self-service portal** | Still owner-managed roster |
| **HR docs (contracts, IDs, tax forms)** | Compliance + PII heavy; not V1 |
| **Auto-reassign bookings on leave** | Product decision; keep manual for now |
| **Room/chair inventory** | Separate backlog |
| **Financing / wage advances** | Strategy “later” partner referrals |

**Remuneration / tips — future sketch (not this sprint)**

When Payments grows past orchestration: tip line on checkout → provider split / transfer; commission reports as **read models** on settled payment events. Do **not** add balance tables or “Adeni holds tips” in V1. Track as Sprint 24+ backlog note only.

---

## 5. API contract (proposed)

### Identity (extends existing tenant staff)

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| existing | `/api/v1/tenant/staff` CRUD | Business | Request/response gain `firstName`, `lastName`, `roleKey`; `displayName` |
| existing | `/api/v1/businesses/{slug}/staff` | Public | `displayName`, `title`, `roleKey` only |

### Hours

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/staff/{id}/hours` | Business | Weekly rules |
| `PUT` | `/api/v1/tenant/staff/{id}/hours` | Business | Replace weekly rules |

### Leave

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/staff/{id}/leave` | Business | List (optional from/to) |
| `POST` | `/api/v1/tenant/staff/{id}/leave` | Business | Create range |
| `DELETE` | `/api/v1/tenant/staff/{id}/leave/{leaveId}` | Business | Cancel leave |

### Calendar (aggregate read)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/staff/{id}/calendar?from=&to=` | Business | Bookings + leave + hours summary for range |

**Errors:** `staff.hours_invalid`, `staff.leave_overlap`, `staff.leave_not_found`, existing `booking.staff_unavailable`.

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | Extend `StaffMember`; add `StaffWeeklyAvailabilityRule`, `StaffLeave`; role enum/constants |
| Application | Extend `IStaffService` DTOs; hours/leave/calendar methods |
| Infrastructure | EF + migrations; `StaffService`; `AvailabilityService` intersect staff hours/leave |
| Api | `TenantStaffController` sub-routes |
| Tests | Hours/leave slot exclusion; tenant isolation; public PII (no first/last) |
| `packages/shared` | Schemas, role keys, EN/FR |
| `packages/api-client` | Staff hours/leave/calendar methods |
| Portal | Staff form 24a; hours editor 24b; leave 24c; calendar view 24d |
| Discover | Role/title display only (no form changes required beyond labels) |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All staff/hours/leave are `ITenantEntity` + filters + `X-Tenant-Id` |
| **Auth / roles** | Business JWT only; no employee login |
| **Cache** | Invalidate availability/profile keys on hours/leave writes |
| **Audit** | Not admin routes; optional business audit later |
| **PII** | First/last owner-only; public API stays display name; never log raw leave reasons if they contain PII — treat as free text, truncate in logs |
| **Payments** | None in 24 |
| **Idempotency** | Not required for hours/leave PUT/POST (owner ops); bookings unchanged |

---

## 8. Acceptance criteria

- [ ] Owner can create staff with first + last name and role; public booking shows display name only
- [ ] Staff with custom Monday hours only appear in Monday slots for that window
- [ ] Leave blocks remove slots / reject booking create for that staff
- [ ] Portal calendar shows that staff’s bookings + leave for the selected week
- [ ] `dotnet test Adeni.slnx -c Release` green for new tests; no Mapbox/secrets in commit
- [ ] Spec/sprints updated when slices land

---

## 9. Open questions

1. **Default hours:** If staff has **no** weekly rules, inherit business hours (recommended) or treat as unavailable until configured?
2. **Role set:** Fixed enum vs free-text only? Spec proposes enum + optional title.
3. **Leave vs existing bookings:** Warn-only (recommended for 24) vs block creating leave when conflicts exist?
4. **Sprint sequencing vs Sprint 20:** Staff ops is listed ahead of deploy/AI in [sprints.md](../sprints.md) “Next up” — confirm product priority stays staff depth before Sprint 20.

---

## 10. Implementation order (after approval)

1. Approve this spec (answers to §9).
2. Implement **24a** (migration + portal form) — smallest shippable.
3. **24b** + wire slots (highest customer-facing value).
4. **24c** leave.
5. **24d** calendar UI on top of list APIs.
6. Remuneration/tips → separate future spec under Payments when product opens Phase 4 finance.
