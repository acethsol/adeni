# Backend module — Notifications

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Event-driven notifications** (email/push/SMS adapters). Dispatches on domain events such as booking confirmed, waitlist slot opened. Respects **tenant notification preferences**.

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Notifications/TenantNotificationPreferences` |
| Application | `Adeni.Application/Notifications/` — `INotificationDispatcher`, preferences |
| Infrastructure | `Adeni.Infrastructure/Notifications/` — logging stub, preference-aware dispatcher, booking/waitlist handlers |

Registration: `AddNotificationsModule()`.

---

## Handlers (examples)

- `BookingNotificationHandler`
- `WaitlistNotificationHandler`

Registered as `IDomainEventHandler<T>` implementations.

---

## Configuration

Production channels (Resend, Twilio, etc.) per `docs/tooling-policy.md`. Current stub logs events for verification.

---

## Clients

No direct UI module — affects customer/business via email/push. Portal may expose preference toggles when wired.

---

## Related

- [Booking](./booking.md), platform [Events](./platform-cross-cutting.md)
