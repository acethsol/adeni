# Spec: Staff selection, multi-service cart & multi-guest

> Do not write code until this spec is reviewed (standing play 1 — [AGENTS.md](../../AGENTS.md)).

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 23 — Staff, cart & guests |
| **Author** | Agent + product |
| **Status** | **23a–23c implemented** (staff depth / hours / leave backlog) |
| **Created** | 2026-10-08 |

---

## 1. Goal (one sentence)

Let customers optionally pick a staff member, book multiple services/add-ons in one cart, and add guests — while businesses manage staff and service eligibility from the portal.

---

## 2. Context

- Deferred from [Sprint 22](./sprint-22-policies-and-service-menu.md) (policies + menu groups shipped).
- Product loop ([product-direction.md](../product-direction.md)): services → **practitioner** → availability → book.
- Strategy: Business tier “staff”; salon/spa flows often need multi-service + party size ([product-strategy.md](../product-strategy.md) §3–4).
- Modules: **Booking** (staff, cart lines, guests, slot math), **Tenancy** (capabilities if needed), **Discovery** (public staff list), **Payments** (deposit on cart total), Portal + Discover.
- Current booking is **one service + one start time + optional notes**; slots ignore staff; no line items or guests.

**Recommended slices (implement in order)**

| Slice | Name | Outcome |
|-------|------|---------|
| **23a** | Staff roster + optional pick | Portal CRUD staff; public list; booking step “Any / pick person”; slots respect staff calendars |
| **23b** | Multi-service cart + add-ons | Cart of services; duration sum; one confirmation; deposit on total |
| **23c** | Multi-guest | Party size / named guests; capacity rules; notes per guest (light) |

Slices share cart model from **23b** — if 23c lands first without cart, guest count attaches to a single-service booking only (acceptable interim, then fold into cart).

---

## 3. In scope

### Slice 23a — Staff / technicians

- [x] `StaffMember` tenant entity: display name, role title (optional), bio (optional, short), active flag, sort order, avatar image key (optional, reuse media patterns)
- [x] Link staff ↔ services they can perform (`StaffServiceLink` many-to-many)
- [ ] Optional weekly availability override (deferred — staff use business hours in 23a)
- [x] Portal: Staff page (list/create/edit/deactivate; assign services)
- [x] Public: `GET /api/v1/businesses/{slug}/staff` (active only, no PII beyond display name)
- [x] Discover booking wizard: after service, step **Staff** — “Any available” or pick one
- [x] Slot generation filters by selected staff (capacity across eligible roster)
- [x] Booking stores `StaffMemberId` nullable (= any / unassigned)
- [x] Portal bookings list shows assigned staff

### Slice 23b — Multi-service cart + add-ons

- [x] Cart model: ordered line items `{ serviceOfferingId, staffMemberId?, sortOrder }`
- [x] Add-ons = services flagged `isAddOn`; cannot book add-on alone
- [x] Total duration = sum of line durations; total price = sum of line prices
- [x] Single `startAt` for the appointment block; end = start + total duration
- [x] Redis slot lock includes tenant + start + primary service + duration (+ staff)
- [x] `POST /api/v1/bookings` accepts `lines[]` (1..N); single-service shorthand kept
- [x] Discover UI: cart / add-on chips; cart summary on confirm
- [x] Deposit initialize uses cart total × guestCount
- [x] Idempotency-Key still required on create

### Slice 23c — Multi-guest

- [x] Booking `guestCount` (default 1, hard cap 6)
- [x] Optional guest labels/names (plain text)
- [x] MVP: appointment duration scales by `guestCount` (`sum(line durations) × guests`); class capacity deferred
- [x] Discover: Guests step before confirm
- [x] Portal booking list shows guest count (+ lines)

## 4. Out of scope

- Payroll, tips, commission, clock-in
- Staff Auth0 logins / employee portal roles (owner-managed roster only in 23)
- Resource rooms / chairs as first-class inventory (beyond guest count capacity)
- Package / membership pricing
- Parallel multi-chair scheduling optimizer (advanced)
- Flutter / mobile parity
- Rich HTML staff pages; social links
- Changing quote-request flow (stays single description)

---

## 5. API contract

### Staff

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/staff` | Business JWT + `X-Tenant-Id` | List staff |
| `POST` | `/api/v1/tenant/staff` | Business | Create |
| `PATCH` | `/api/v1/tenant/staff/{id}` | Business | Update |
| `POST` | `/api/v1/tenant/staff/{id}/deactivate` | Business | Soft-disable |
| `PUT` | `/api/v1/tenant/staff/{id}/services` | Business | Replace service IDs |
| `GET` | `/api/v1/businesses/{slug}/staff` | Anonymous | Active staff (+ optional `?serviceId=`) |

```json
{
  "id": "uuid",
  "displayName": "Ada",
  "title": "Senior stylist",
  "bio": null,
  "isActive": true,
  "sortOrder": 0,
  "avatarImageUrl": null,
  "serviceOfferingIds": ["uuid"]
}
```

### Slots (extend)

`GET /api/v1/businesses/{slug}/slots?serviceId=&from=&to=&staffMemberId=`  
Optional: `serviceIds=id1,id2` (cart duration) when 23b lands.

### Create booking (extend)

```json
{
  "tenantId": "uuid",
  "startAt": "2026-10-10T14:00:00Z",
  "customerNotes": "…",
  "staffMemberId": "uuid | null",
  "guestCount": 1,
  "guests": [{ "displayName": "Optional" }],
  "lines": [
    { "serviceOfferingId": "uuid", "staffMemberId": null },
    { "serviceOfferingId": "uuid-addon", "staffMemberId": null }
  ]
}
```

Backward compatible: omit `lines` → treat `serviceOfferingId` (existing field) as single line.

**Errors (stable codes):**  
`booking.staff_unavailable`, `booking.staff_not_eligible`, `booking.cart_empty`, `booking.addon_requires_parent`, `booking.guest_limit`, `booking.capacity_full` (+ existing slot errors).

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `StaffMember`, `StaffService`, booking line/guest types; `ServiceOffering.IsAddOn`; extend `BookingRecord` |
| Application | `IStaffService`, booking DTOs (`CreateBookingRequest` lines/guests), availability signatures |
| Infrastructure | EF + migrations; `BookingService` / `AvailabilityService` / slot locks; staff service |
| Api | Tenant staff controller; discovery staff + slots query; bookings create |
| Tests | Staff CRUD + tenant isolation; slots by staff; cart duration; guest validation |
| Shared / clients | Zod + `api-client` methods |
| Portal | New Staff page; services “add-on” toggle; bookings show staff/guests |
| Discover | Booking wizard steps: Service(s) → Staff → Time → Guests → Confirm |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All staff/lines/guests tenant-scoped; EF filters + middleware |
| **Auth / roles** | Staff mutations: business JWT; booking create: customer (existing); public staff list anonymous |
| **Cache** | Invalidate staff list keys on write; profile cache if staff shown on public page later |
| **Audit** | N/A for tenant self-serve (not `/admin/*`) |
| **PII** | Guest display names are light PII — do not log; mask in structured logs |
| **Payments** | Deposit % of **cart total**; orchestration only |
| **Idempotency** | Keep `Idempotency-Key` on booking create |
| **Observability** | Correlation ID; log staff/cart/guest counts not names |

---

## 8. Tests

- [ ] Unit: staff cannot be booked for a service they don’t offer
- [ ] Unit: slots empty when staff calendar closed
- [ ] Unit: cart duration = sum; add-on alone rejected when required
- [ ] Unit: guestCount bounds; capacity for class delivery
- [ ] Integration: create booking with lines + staff + guests
- [ ] Tenant: cannot assign another tenant’s staff/service IDs

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [ ] Portal can manage staff and which services they perform
- [ ] Discover can choose Any or a named staff member; times match that choice
- [ ] Customer can add multiple services/add-ons; confirm shows total time and price
- [ ] Customer can set guest count (and optional names); portal sees them on the booking
- [ ] Deposits and idempotency still work on multi-line bookings
- [ ] EN/FR chrome for new wizard steps (business-authored staff names stay as entered)

---

## 10. Open questions

| Question | Decision (proposed) |
|----------|---------------------|
| Multi-guest capacity for appointments | **MVP:** guestCount scales duration (`duration × guestCount`) for same service; parallel chairs deferred |
| One staff for whole cart vs per line | **MVP:** one staff for cart; per-line staff in a follow-up |
| Add-on model | Boolean `isAddOn` on `ServiceOffering` + optional `addonOfServiceIds` |
| “Any available” assignment | Leave `StaffMemberId` null; business assigns later in portal (manual) |
| Entitlements | Staff count soft-capped by Business tier later; unlimited in 23 for verified tenants |
| Slice order if timeboxed | **23a → 23b → 23c** |

---

## 11. Wizard UX (Discover)

```
[ Services / cart ] → [ Staff (if any active) ] → [ Guests ] → [ Time ] → [ Confirm ]
(Guests before time so slot length matches party size.)
```

- Hide Staff step when tenant has zero active staff.
- Hide Guests step when `guestCount` capability off (default **on** for `scheduled_appointment`, off for pure class until capacity wired).
- Confirm: lines, staff label, party size, policies, deposit notice (existing).
