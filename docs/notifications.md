# Notifications — transactional outbound delivery

Async **email**, **SMS**, and (later) **push** driven by **domain events** and **tenant preferences**. In-app chat and live UI updates stay in **Messaging** and **Operations** (SignalR).

**Boundaries:** [specs/notifications-module-boundaries.md](./specs/notifications-module-boundaries.md)  
**Roadmap:** [roadmap-modules.md](./roadmap-modules.md) #12  
**Vendors:** [tooling-policy.md](./tooling-policy.md) — **Resend** (email), **Twilio** (SMS / WhatsApp API later), **FCM** (push, Phase 4)

---

## Shipped today (development behavior)

| Piece | Status |
|-------|--------|
| `INotificationDispatcher` | Application port |
| `INotificationPreferencesService` | Tenant toggles: email, push, SMS/WhatsApp reminders |
| `PreferenceAwareNotificationDispatcher` | Gates by channel + prefs |
| **Delivery** | **`LoggingNotificationDispatcher`** — logs only; no Resend/Twilio send |
| **Appointment reminder scheduler** | **Not built** — no cron/background job for “24h before” |

Pro/Business tiers expose a **`Reminders`** entitlement flag; it is not yet wired to scheduled sends.

---

## Architecture

```
Domain events (Booking, Waitlist, Messaging, Finance, Queue, Identity, …)
        ↓
  IDomainEventHandler<*>  (Notifications.Infrastructure)
        ↓
  INotificationDispatcher
        ↓
  PreferenceAwareNotificationDispatcher
        ↓
  ChannelNotificationDispatcher (Phase 1 target)
     ├─ email  → IEmailNotificationProvider → Resend
     ├─ sms    → ISmsNotificationProvider   → Twilio
     ├─ push   → IPushNotificationProvider  → FCM (Phase 4)
     └─ (dev)  → Logging when API keys absent
```

| Layer | Location |
|-------|----------|
| Application | `INotificationDispatcher`, `INotificationPreferencesService`, provider ports |
| Infrastructure | Handlers, `AddNotificationsModule()`, Resend/Twilio adapters |
| API | `GET/PATCH` tenant notification preferences (settings) |

**Reporting / Finance:** Notifications **sends** messages; **Reporting** (or Finance) supplies **PDF attachment bytes** for receipt/invoice email — do not embed QuestPDF in the dispatcher.

---

## Event → notification matrix (current handlers)

| Event | Default channel | Notes |
|-------|-----------------|--------|
| `BookingConfirmed` | email | Recipient key still **customer id** — fix in Phase 1 |
| `BookingRejected` | email | Same |
| `BookingCancelled` | email | Same |
| `SlotOpened` (waitlist) | email | Stub recipient; SMS when prefs + Twilio land |
| `ConversationMessageCreated` | email | Customer ↔ business alerts |
| `PaymentCompleted` / `PaymentFailed` | email | Tenant-scoped copy |
| Billing document email | email | Real customer email when snapshot present |
| Staff portal invite | email | Accept link in body |
| Queue ticket called | email | Should move to SMS when guest phone present |

Realtime **SignalR** events are **not** part of this module.

---

## Tenant preferences

Stored on `TenantNotificationPreferences`:

- `EmailEnabled`
- `PushEnabled`
- `SmsWhatsAppReminderEnabled` — gates `sms` / `whatsapp` channels when adapters exist

Portal **Settings** persists prefs via tenant settings API; prefs apply even though delivery is still log-only in Development.

---

## Configuration (Phase 1 target)

| Key | Purpose |
|-----|---------|
| `Resend:ApiKey` | Resend API |
| `Resend:From` | From address |
| `Twilio:AccountSid` | Twilio |
| `Twilio:AuthToken` | Twilio |
| `Twilio:FromNumber` | SMS sender |

Keys in Key Vault for staging/production — never commit.

---

## Reminders (product backlog)

| Capability | Status |
|------------|--------|
| Booking confirmation email | Handler exists; needs Resend + recipient resolution |
| Cancellation / rejection email | Same |
| **Scheduled appointment reminder** (email/SMS) | Spec Phase 2–3; requires job + idempotent `notification_deliveries` |
| Waitlist slot opened | Event exists; recipient resolution incomplete |

**Murphy / security:** Payment webhooks remain on Finance — not Notifications. Outbound sends: mask PII in logs (`PiiMasker`), idempotency per event + channel + recipient.

---

## HTTP surface

| Method | Route | Auth |
|--------|-------|------|
| `GET/PATCH` | `/api/v1/tenant/settings/notifications` (or settings aggregate) | Business |

No public Resend/Twilio inbound webhooks in V1 unless product adds SMS reply handling.

---

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **1** | Resend + Twilio adapters; `ChannelNotificationDispatcher`; fix recipient keys; delivery idempotency table |
| **2** | HTML templates; receipt email with PDF attachment; queue/waitlist SMS |
| **3** | Twilio WhatsApp templates for reminders (legal/opt-in) |
| **4** | FCM push registration + fan-out |

---

## Tests

- Preference gating skips disabled channels
- Mock provider integration on `BookingConfirmed`
- Duplicate domain event does not double-send (idempotency)

Run: `dotnet test Adeni.slnx -c Release`
