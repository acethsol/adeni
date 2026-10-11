# Spec: Sprint 29 — Queue v1 (walk-in floor board)

> Standing play 1 — draft for review. Do not implement until **Approved**.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 29 — Queue v1 |
| **Author** | Cloud Agent |
| **Status** | Draft |
| **Created** | 2026-10-11 |

---

## 1. Goal (one sentence)

Walk-in clients can **join a live queue** and staff can **call next / mark no-show / start service** on the same floor board as appointments — without a separate Zenoti-style queue product.

---

## 2. Context

- Deep dive: [roadmap-workflows-payments-queue.md](../roadmap-workflows-payments-queue.md) §5
- Module index: [roadmap-modules.md](../roadmap-modules.md) §3.1
- Money loop shipped: check-in → complete → paid ([Sprint 27](./sprint-27-payment-loop.md))
- Today’s **waitlist** (`WaitlistEntry`) is for “notify when a slot opens” — **not** a live walk-in queue. Keep it; do not overload it.
- Capability flag target: `walk_in_queue` (see [product-strategy.md](../product-strategy.md) business types)
- Kiosk fullscreen shell = **Sprint 30** (presentation mode of the same APIs) — out of this sprint

### Relationship to appointments

```
Booked appointment ──┐
                     ├──→ Floor board ──→ In service ──→ Done / Paid (Sprint 27)
Walk-in queue     ───┘
```

Queue tickets are first-class floor entities. When “In service” starts, optionally create / link a same-day booking so payment loop still applies.

---

## 3. In scope

### Capability & domain

- [ ] **29a** Enable `walk_in_queue` capability for categories that need it (nails, barbers, selected spas) via existing capabilities catalog — gate APIs + portal nav
- [ ] **29b** New entity `QueueTicket` (name TBD) under Booking schema — **do not** reuse `WaitlistEntry`
  - Fields: TenantId, optional ServiceOfferingId, optional StaffMemberId, CustomerId (or guest phone/name snapshot), Status, Position/JoinedAt, CalledAt, StartedAt, CompletedAt, NoShowAt, PartySize, Notes, optional BookingId
- [ ] **29c** Status enum: `Waiting | Called | InService | Done | NoShow | Cancelled`
- [ ] **29d** SQL tables + EF mapping + tenant filters

### Customer / public join

- [ ] **29e** Public (or customer-auth) join: `POST /api/v1/public/queue/{tenantSlug}/join` with phone + name + optional serviceId
  - Require phone (no anonymous walk-ins in v1)
  - Upsert/find customer by phone within tenant market rules where possible; else guest snapshot on ticket
- [ ] **29f** Customer status: `GET /api/v1/public/queue/tickets/{token}` — position, ETA, status
- [ ] **29g** Discover join page: `/q/{slug}` (or `/businesses/{slug}/queue`) — short form, one job

### Staff floor board

- [ ] **29h** Tenant APIs (portal.bookings or new portal.queue permission — prefer bookings + capability gate):
  - `GET /api/v1/tenant/queue` — active tickets (Waiting/Called/InService) + optional today’s Done/NoShow
  - `POST /api/v1/tenant/queue/{id}/call` — Waiting → Called; notify customer
  - `POST /api/v1/tenant/queue/{id}/start` — Called (or Waiting) → InService; optional assign staff; optional create/link booking
  - `POST /api/v1/tenant/queue/{id}/complete` — InService → Done (hand off to payment loop if booking linked)
  - `POST /api/v1/tenant/queue/{id}/no-show` — Waiting/Called → NoShow
  - `POST /api/v1/tenant/queue/{id}/cancel` — Waiting → Cancelled
  - `POST /api/v1/tenant/queue` — staff can add walk-in on behalf of guest
- [ ] **29i** Position = count of Waiting ahead + 1; ETA ≈ sum of avg duration of people ahead (service duration or tenant default 30m)
- [ ] **29j** Portal page `/queue` (or Floor tab) — list Waiting / Called / In service with Next / No-show / Start actions
- [ ] **29k** Practitioner scope: if `portal.bookings.self` only, filter tickets assigned to their staff member (unassigned visible to full bookings roles only)

### Notifications

- [ ] **29l** On Call: notify via `INotificationDispatcher` (email/stub + WhatsApp deep-link body if phone present) — “You’re up at {Business}”
- [ ] **29m** Preference toggle on tenant notification prefs if one already exists for waitlist-style alerts

### Docs & tests

- [ ] **29n** Update roadmap module status; shared schemas + api-client
- [ ] **29o** Unit/integration: join → position; call → Called; no-show; start → InService; capability gate; tenant isolation; practitioner scope

---

## 4. Out of scope

- **Tablet kiosk mode** (Sprint 30 — fullscreen shell of join + optional staff pin)
- Zenoti-style optimizer, room inventory, multi-location queue routing
- Anonymous walk-ins (no phone)
- SMS provider integration beyond existing notification stub (WhatsApp link text OK)
- Auto-convert every ticket to a paid booking without staff action
- POS / Terminal
- Flutter mobile parity (portal + discover web first; mobile can reuse APIs)
- Replacing appointment waitlist (`WaitlistEntry`)

---

## 5. API contract (draft)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/v1/public/queue/{slug}/join` | Public (rate-limited) | Join queue |
| `GET` | `/api/v1/public/queue/tickets/{token}` | Public (token) | Ticket status + ETA |
| `GET` | `/api/v1/tenant/queue` | Portal + bookings (+ capability) | Floor list |
| `POST` | `/api/v1/tenant/queue` | Portal + bookings | Staff add walk-in |
| `POST` | `/api/v1/tenant/queue/{id}/call` | Portal + bookings | Call next / this ticket |
| `POST` | `/api/v1/tenant/queue/{id}/start` | Portal + bookings | Start service |
| `POST` | `/api/v1/tenant/queue/{id}/complete` | Portal + bookings | Done |
| `POST` | `/api/v1/tenant/queue/{id}/no-show` | Portal + bookings | No-show |
| `POST` | `/api/v1/tenant/queue/{id}/cancel` | Portal + bookings | Cancel waiting |

**Join body (example):**

```json
{
  "displayName": "Ada Okoro",
  "phone": "+2348012345678",
  "serviceOfferingId": "uuid-optional",
  "partySize": 1,
  "notes": "optional"
}
```

**Ticket DTO (example):**

```json
{
  "id": "…",
  "status": "waiting",
  "position": 3,
  "etaMinutes": 45,
  "displayName": "Ada Okoro",
  "serviceName": "Gel manicure",
  "staffDisplayName": null,
  "partySize": 1,
  "joinedAt": "…",
  "publicToken": "…",
  "bookingId": null
}
```

---

## 6. Data model (sketch)

```
booking.queue_tickets
  Id, TenantId, ServiceOfferingId?, StaffMemberId?,
  CustomerId?, GuestName, GuestPhone,
  Status, PartySize, Notes,
  PublicToken (unique),
  BookingId?,
  JoinedAt, CalledAt?, StartedAt?, CompletedAt?, NoShowAt?,
  CreatedAt, UpdatedAt

Indexes: (TenantId, Status, JoinedAt), PublicToken unique
```

---

## 7. Security & compliance

| Topic | Requirement |
|-------|-------------|
| Tenant isolation | All staff APIs tenant-scoped; public join by slug → tenant |
| PII | Phone/name on ticket; mask in logs |
| Public token | Unguessable (128-bit+); rate-limit join + status |
| Capability | Mutations require `walk_in_queue` enabled for tenant category |
| Practitioner scope | Same pattern as bookings.self |

---

## 8. UX notes

- Discover join: one composition — business name, “Join the queue”, name/phone/service, CTA. No dashboard clutter.
- Portal floor: Waiting column + Called/In service actions; ETA shown as “~45 min”
- “Next” button = call the oldest Waiting ticket (or assigned-to-me filter)
- After Complete on a linked booking, reuse Sprint 27 payment actions

---

## 9. Open questions

1. **Start → booking** — always create a same-day booking on Start, or only when staff chooses “Start + book”?  
2. **Permission** — reuse `portal.bookings` or add `portal.queue`?  
3. **Multi-service join** — v1 single service only?  
4. **Default categories** for `walk_in_queue` — nails + barbers only, or all beauty?

**Proposed defaults if unanswered at approve:** Create booking on Start when a service is selected; reuse `portal.bookings` (+ capability); single service; enable for nails + barbers (+ opt-in others via category catalog).

---

## 10. Acceptance criteria

- [ ] Customer can join via public slug and see position + ETA
- [ ] Staff can Call → Start → Complete a ticket; No-show releases the line
- [ ] Position/ETA update when someone ahead is called/no-showed
- [ ] Capability-off tenant cannot join or see queue APIs
- [ ] Practitioner with bookings.self only sees their assigned tickets (+ cannot steal unassigned without full bookings)
- [ ] Waitlist (slot-opened) still works unchanged
- [ ] Docs state kiosk is Sprint 30, not this sprint

---

## 11. Follow-ups

1. **Sprint 30 — Kiosk mode** — fullscreen join + optional staff pin on Flutter/portal tablet route  
2. WhatsApp/SMS provider for “You’re up”  
3. Queue metrics on owner dashboard  
4. POS bridge (separate)  
