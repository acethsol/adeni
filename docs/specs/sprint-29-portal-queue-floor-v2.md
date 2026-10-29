# Spec: Sprint 29 — Portal queue floor v2

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 29 follow-up (portal UX) |
| **Status** | Implemented |
| **Created** | 2026-10-16 |

## 1. Goal

Make `/queue` a usable **front-desk floor board** (live refresh, full staff actions, public join link, payment handoff) using existing Sprint 29 APIs — no new backend scope.

## 2. In scope

- [x] Auto-refresh floor list (~15s) + manual refresh
- [x] Staff add walk-in: party size, notes, optional assign staff
- [x] Cancel waiting tickets; assign staff on Start
- [x] Column board: Waiting | Called | In service
- [x] Public join URL (`discoverWebUrl/q/{slug}`) + copy
- [x] CTA to Bookings when ticket has `bookingId` after complete
- [x] Practitioner scope copy when `portal.bookings.self` only

## 3. Out of scope

- SignalR / push; kiosk shell (Sprint 30); new API routes; Flutter parity

## 4. Files

| Layer | Files |
|-------|-------|
| Portal | `apps/business/src/app/pages/queue/*` |
| Docs | this spec |
