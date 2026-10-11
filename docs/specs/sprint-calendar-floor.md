# Spec: Floor calendar (reusable, role-aware)

## Intent

The booking calendar is a hallmark surface — portal today, responsive/mobile later. It must stay **custom** (no FullCalendar), **shared** via `@adeni/ui` + `@adeni/shared` contracts, and **role-aware**.

## Roles

| Role | Calendar |
|------|----------|
| Practitioner (`portal.staff.self`, not full staff) | Own board only (`/my-calendar`) |
| Owner / manager (`portal.staff`) | **Team calendar** (`/calendar`) — all staff, filter chips, hours worked; plus per-member `/staff/:id/calendar` |
| Future mobile | Same APIs + shared event model; native shell wraps the same month/week/day semantics |

## Event semantics

| Flag | Meaning |
|------|---------|
| `customerSelectedStaff: true` | Customer asked for this person at booking (Carbon **user** icon on the event) |
| `customerSelectedStaff: false` | “Any available” / unassigned (no person icon) |

Today `StaffMemberId` null means any-available. When auto-assign lands, keep this boolean as the source of truth (do not infer from assignment alone).

## APIs

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| `GET` | `/api/v1/tenant/staff/{id}/calendar?from=&to=` | staff access | Single member board |
| `GET` | `/api/v1/tenant/staff/team-calendar?from=&to=` | `portal.staff` | All members + unassigned + per-staff hours summary |

Range max 45 days (unchanged).

## UI building blocks

- Portal page shells load data and choose mode (`self` / `member` / `team`).
- Presentation (month/week/day board, leave bands, now line, detail rail, selected-staff badge) lives in reusable calendar code under `@adeni/ui` over time; portal must not fork a second calendar.
- Mobile: reuse API + event schema; day view is the default under ~820px (already).

## Metrics (team)

Per staff in range:

- Booking count / confirmed count  
- **Hours worked** = sum of confirmed booking durations (`endAt - startAt`)

## Out of scope (this slice)

- Drag-reschedule  
- Auto-assign “any” bookings onto a practitioner  
- Native mobile app shell  
- Payroll / tips