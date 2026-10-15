# Spec: Sprint 30 — Front-desk check-in kiosk

> Standing play 1 — reworked: appointment check-in first (walk-in join secondary).

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 30 — Front-desk check-in kiosk |
| **Author** | Cloud Agent |
| **Status** | Implemented |
| **Created** | 2026-10-11 |
| **Updated** | 2026-10-11 |

---

## 1. Goal (one sentence)

Front-desk tablets run a **fullscreen check-in shell** so arriving clients can find today’s appointment and check in — with optional **staff PIN to exit**; walk-in queue join is secondary when `walk_in_queue` is enabled.

---

## 2. Context

- Check-in API shipped: [sprint-27-payment-loop.md](./sprint-27-payment-loop.md) (`Confirmed` → `CheckedIn`)
- Queue APIs: [sprint-29-queue-v1.md](./sprint-29-queue-v1.md)
- Deep dive: [roadmap-workflows-payments-queue.md](../roadmap-workflows-payments-queue.md) §5 — *“Kiosk = presentation mode of check-in”*
- Flutter business skeleton notes a front-desk shell; **this sprint ships portal web kiosk first**

---

## 3. In scope

- [x] **30a** Portal route `/kiosk` outside shell — fullscreen, large touch targets, Adeni Carbon styling, no nav
- [x] **30b** Today’s confirmed appointments with search (name / phone / service)
- [x] **30c** Client taps **Check in** → existing `POST …/bookings/{id}/check-in`; success then “Next guest”
- [x] **30d** Optional staff exit PIN: SHA-256 of `{tenantId:N}:{pin}` on `BusinessProfile`; set from Bookings; verify to leave
- [x] **30e** APIs: set / verify / status PIN (portal.bookings)
- [x] **30f** Secondary walk-in join on kiosk when `walk_in_queue` enabled; discover `/q/:slug?kiosk=1` kept for public tablet walk-ins
- [x] **30g** Docs + shared schemas (`customerName` / `customerPhoneMasked` on tenant bookings); PIN unit tests
- [x] **30h** Launch from Bookings (“Open check-in”), not Queue-first

---

## 4. Out of scope

- Flutter native kiosk (reuse shell pattern later)
- Device MDM / Android locked task mode
- Public unauthenticated appointment check-in (needs staff-session tablet)
- SMS “you’re up”
- Separate kiosk Auth0 device credentials

---

## 5. API contract

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `PUT` | `/api/v1/tenant/kiosk/pin` | Portal bookings | `{ "pin": "1234" }` or `{ "pin": null }` to clear |
| `POST` | `/api/v1/tenant/kiosk/verify-pin` | Portal bookings | `{ "pin": "1234" }` → `{ "ok": true }` |
| `GET` | `/api/v1/tenant/kiosk/status` | Portal bookings | `{ "hasPin": bool, "slug": "…", "queueEnabled": bool }` |
| `POST` | `/api/v1/tenant/bookings/{id}/check-in` | Portal bookings | Existing Sprint 27 transition |

PIN: 4–8 digits. Stored as SHA-256 hex of `{tenantId:N}:{pin}` (no plaintext).

---

## 6. Acceptance criteria

- [x] Authenticated staff can open fullscreen check-in; clients see today’s appointments and check in
- [x] Without PIN, exit requires confirm; with PIN, wrong PIN denied
- [x] UI uses Adeni Carbon tokens (IBM Plex, purple interactive, no custom cream/serif theme)
- [x] Walk-in join remains optional when queue capability is on
