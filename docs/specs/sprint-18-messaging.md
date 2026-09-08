# Spec: Sprint 18 — Messaging & WhatsApp bridge

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 18 — Messaging & WhatsApp bridge |
| **Author** | Cloud Agent |
| **Status** | Implemented |
| **Created** | 2026-09-07 |

---

## 1. Goal (one sentence)

Let customers and businesses communicate in-app (Pro tier) while meeting Nigeria's WhatsApp-first habit via deep links — no WABA integration in this sprint.

---

## 2. Context

- [docs/sprints.md](../sprints.md) Sprint 18
- Messaging entitlement from Sprint 16 (`SubscriptionEntitlements.Messaging`)
- Share-kit / payments already use `wa.me` client-side; this sprint adds server-built links + threads

---

## 3. In scope

- [x] **18a** Messaging module — `message_threads` + `messages` in `messaging` schema; customer + tenant APIs
- [x] **18b** WhatsApp deep links — public business + booking-context links
- [x] **18d** Message templates — tenant quick-replies (hours, location, pricing)
- [x] **18e** Unread badge + business inbox (web portal)
- [x] **18c** Notification preferences — email, push (FCM), SMS/WhatsApp reminder channel per tenant
- [x] **18f** FAQ auto-responder — rule-based replies for price/hours/availability

---

## 4. Out of scope

- WhatsApp Business API / WABA webhooks
- Push/FCM/SMS delivery adapters (18c stores prefs + gates logging stub)
- LLM auto-replies (Sprint 20)
- SignalR real-time (polling inbox for MVP)

---

## 5. API contract

See implementation:

| Audience | Prefix |
|----------|--------|
| Customer | `/api/v1/messages/*` |
| Business | `/api/v1/tenant/messages/*` |
| Public | `GET /api/v1/businesses/{slug}/whatsapp-link` |
| Customer booking | `GET /api/v1/bookings/{id}/whatsapp-link` |

Business routes return `403` + `subscription.messaging_required` on Free tier.

---

## 6. Files touched

| Layer | Files |
|-------|-------|
| Domain | `Adeni.Domain/Messaging/*` |
| Application | `IMessageThreadService`, entitlements |
| Infrastructure | `MessageThreadService`, migration |
| Api | `MessagesController`, `TenantMessagesController`, Businesses/Bookings whatsapp |
| Shared | `packages/shared` messaging schemas |
| Web | `/business/messages`, bells, WhatsApp buttons |
| Tests | `MessagingIntegrationTests.cs` |

---

## 7. Acceptance criteria

- [x] Customer creates thread, sends message; customer web/mobile inbox
- [x] Pro business lists threads, replies, uses templates
- [x] Free business gets 403 on tenant message APIs
- [x] Unread count drives portal badge
- [x] Public profile + my-bookings open WhatsApp with context
- [x] Tenant configures notification channels and FAQ auto-responder
