# Spec: Sprint 29 — Unified floor board

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 29 extension — Unified floor board |
| **Status** | Implemented |
| **Created** | 2026-10-16 |

## 1. Goal

One portal floor board for **booked arrivals + walk-ins**, with **walk-in policy** (open/close, caps, calendar strictness) and **calendar-aware join** validation.

## 2. In scope

- Check-in (booking) → upsert `QueueTicket` linked by `BookingId`, source `appointment`
- `GET /api/v1/tenant/queue` → `{ items, expectedArrivals, walkInStatus }`
- Walk-in settings on `business_profiles` + portal Settings tab (when `walk_in_queue`)
- `POST /api/v1/tenant/queue/walk-in/open` — temporary open block
- Join / staff add gated when queue closed or full; strict policy rejects join when projected service overlaps confirmed bookings
- Start service: staff overlap check vs confirmed/checked-in bookings

## 3. Out of scope

- Auto check-in from calendar time
- Recurring walk-in hour editor (use open block + manual toggle for v1)
- Discover UI polish beyond closed/join errors

## 4. API

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/tenant/queue` | Floor payload |
| PATCH | `/api/v1/tenant/queue/walk-in-settings` | Policy fields |
| POST | `/api/v1/tenant/queue/walk-in/open` | `{ durationMinutes }` |
