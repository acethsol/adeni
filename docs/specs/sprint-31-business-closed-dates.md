# Spec: Sprint 31 — Business closed dates (calendar open / closed)

> Critical floor control — Airbnb-style date blocking for the whole business.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 31 — Business closed dates |
| **Author** | Cloud Agent |
| **Status** | Implemented |
| **Created** | 2026-10-11 |

---

## 1. Goal (one sentence)

Owners can **open or close specific dates / date ranges** on a business calendar so those days cannot be booked — on top of weekly hours and staff leave.

---

## 2. Context

- Today: recurring **weekly hours** (Availability) + **staff leave** (per person). No business-wide “closed Dec 24–Jan 2”.
- Slot engine: [AvailabilityService](../../src/Adeni.Infrastructure/Booking/AvailabilityService.cs) already intersects business hours ∩ staff hours ∩ leave.
- Product need: salon/spa holidays, renovations, inventory pause — same mental model as Airbnb host calendar.

---

## 3. In scope

- [x] **31a** Entity `BusinessClosedDate` (tenant-scoped): `Id`, `TenantId`, `StartDate` (date), `EndDate` (date, inclusive), optional `Reason` (short, owner-only), `CreatedAt`
- [x] **31b** APIs (portal.hours or portal.profile — prefer `portal.hours`):
  - `GET /api/v1/tenant/closed-dates`
  - `POST /api/v1/tenant/closed-dates` `{ startDate, endDate, reason? }`
  - `DELETE /api/v1/tenant/closed-dates/{id}`
- [x] **31c** Slot + create-booking guards: no public/tenant slots on closed dates; create booking fails with clear conflict if start falls in a closed range
- [x] **31d** Portal **Availability** page: month calendar (Adeni Carbon styling) — tap day to toggle closed/open; list of ranges with remove; create range (from→to)
- [x] **31e** Soft warning when new closed range overlaps existing confirmed/pending bookings (return conflicting booking ids; no auto-cancel)
- [x] **31f** Docs + shared schemas + unit tests (overlap, slot exclusion, inclusive end date)
- [x] **31g** Discover/public: closed days simply show no slots (no special messaging required in this sprint)

---

## 4. Out of scope

- Per-location closed dates (use primary / all locations same until multi-location depth)
- Partial-day closed windows (e.g. closed mornings only) — use weekly hours or later slice
- Airbnb-style “price by date” / min-stay
- Auto-cancel / auto-reschedule bookings inside new closed range
- Customer notification campaign when dates close
- Staff leave UX changes (already shipped)

---

## 5. Rules

| Rule | Detail |
|------|--------|
| **Inclusive dates** | `EndDate` inclusive in tenant market calendar date (store as `date` / midnight UTC date-only semantics — match leave pattern) |
| **Weekly hours still apply** | Closed date = fully off that day; open days still use weekly open/close times |
| **Staff leave** | Still applies; closed date is business-wide (all staff) |
| **Overlap merge** | Optional: reject overlapping POST with clear error, or merge adjacent — **reject overlap** in V1 (simplest) |
| **Max range** | Cap e.g. 366 days per entry; max ~100 active ranges (validation) |

---

## 6. Acceptance criteria

- [x] Owner closes a date range → those days produce **zero** bookable slots
- [x] Opening (delete closed range) restores slots subject to weekly hours
- [x] Portal month calendar shows closed days clearly; toggle/range create works on mobile + desktop
- [x] Existing confirmed booking inside a newly closed range is **warned**, not auto-cancelled
- [x] Practitioners without `portal.hours` cannot edit closed dates

---

## 7. Suggested UX

**Availability** page becomes two sections:

1. **Weekly hours** (existing)
2. **Closed dates** — month grid + “Add closed range” + list

Closed days: filled with support/error tint or charcoal band (Carbon tokens — not a new theme).

---

## 8. Sequencing

Implement after Sprint 30 merge (or parallel on `dev`). Small, high-leverage; should land before heavy booking SEO / discover polish that assumes continuous calendars.
