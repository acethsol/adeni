# Spec: Sprint 30 — Kiosk mode

> Standing play 1 — approved to implement (follow-on from Sprint 29).

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 30 — Kiosk mode |
| **Author** | Cloud Agent |
| **Status** | Implemented |
| **Created** | 2026-10-11 |

---

## 1. Goal (one sentence)

Front-desk tablets can run a **fullscreen join queue shell** with minimal chrome and an optional **staff PIN to exit** — presentation mode of Sprint 29 queue APIs, not a second product.

---

## 2. Context

- Queue APIs shipped: [sprint-29-queue-v1.md](./sprint-29-queue-v1.md)
- Deep dive: [roadmap-workflows-payments-queue.md](../roadmap-workflows-payments-queue.md) §5
- Flutter business skeleton notes a front-desk shell; **this sprint ships portal web kiosk first** (tablet browser / guided access)

---

## 3. In scope

- [x] **30a** Portal route `/kiosk` outside shell — fullscreen, large touch targets, no nav
- [x] **30b** Guest join UI (name, phone, optional service) calling existing public queue join via tenant primary slug
- [x] **30c** Success state: position + ETA; “Join another” resets form
- [x] **30d** Optional staff exit PIN: store hash on `BusinessProfile`; set from Queue page; verify to leave kiosk
- [x] **30e** APIs: set PIN + verify PIN (portal.bookings)
- [x] **30f** Discover `/q/:slug?kiosk=1` — hide footer chrome / larger touch (public tablet option)
- [x] **30g** Docs + shared schemas; light unit tests for PIN hash/verify
- [x] **30h** Capability gate: kiosk only useful when `walk_in_queue` enabled

## 4. Out of scope

- Flutter native kiosk (reuse later)
- Device MDM / Android locked task mode packaging
- Hardware PIN pads
- SMS “you’re up”
- Separate kiosk Auth0 device credentials

---

## 5. API contract

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `PUT` | `/api/v1/tenant/kiosk/pin` | Portal bookings | `{ "pin": "1234" }` or `{ "pin": null }` to clear |
| `POST` | `/api/v1/tenant/kiosk/verify-pin` | Portal bookings | `{ "pin": "1234" }` → `{ "ok": true }` |
| `GET` | `/api/v1/tenant/kiosk/status` | Portal bookings | `{ "hasPin": bool, "slug": "…" }` |

PIN: 4–8 digits. Stored as SHA-256 hex of `{tenantId:N}:{pin}` (no plaintext).

---

## 6. Acceptance criteria

- [x] Authenticated staff can open fullscreen kiosk and guests can join the queue
- [x] Without PIN, exit requires confirm; with PIN, wrong PIN denied
- [x] Public `?kiosk=1` join page is usable fullscreen
- [x] No new queue business logic — reuses Sprint 29 APIs
