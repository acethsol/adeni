# Backend module — Messaging

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**In-app message threads** between customers and businesses, optional **FAQ auto-responder**, and **WhatsApp deep links** (bridge — not full WhatsApp API in v1).

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Messaging/MessageThread` |
| Application | `Adeni.Application/Messaging/` — `IMessageThreadService`, `IFaqAutoResponder` |
| Infrastructure | `Adeni.Infrastructure/Messaging/` |
| API | `MessagesController`, `TenantMessagesController` |

Registration: `AddMessagingModule()`.

---

## HTTP routes

| Method | Route | Side |
|--------|-------|------|
| GET/POST | `/api/v1/messages/threads` | Customer |
| GET/POST | `/api/v1/tenant/messages/threads` | Business |
| GET | `.../whatsapp-link` | Generate deep link |

Realtime: SignalR planned/enhanced per sprint specs; REST is source of truth for history.

---

## Clients

Discover (customer threads), Portal **Messages** page.

---

## Tests

- `MessagingIntegrationTests`

---

## Related

- [Notifications](./notifications.md) — may alert on new messages
