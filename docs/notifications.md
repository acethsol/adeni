# Notifications — transactional outbound delivery

Async **email**, **SMS**, and (later) **push** driven by **domain events** and **tenant preferences**. In-app chat and live UI updates stay in **Messaging** and **Operations** (SignalR).

**Boundaries:** [specs/notifications-module-boundaries.md](./specs/notifications-module-boundaries.md)  
**Implementation:** [specs/sprint-38-notifications-module.md](./specs/sprint-38-notifications-module.md) (38A / 38B)  
**Roadmap:** [roadmap-modules.md](./roadmap-modules.md) #12  
**Vendors:** [tooling-policy.md](./tooling-policy.md) — **Resend** (email), **Twilio** (SMS), **FCM** (push backlog). **`wa.me` links are client-only** — no Twilio WhatsApp API in V1.

---

## Shipped today (development behavior)

| Piece | Status |
|-------|--------|
| `INotificationDispatcher` | Application port |
| `INotificationPreferencesService` | Tenant toggles: email, push, SMS reminders (`SmsRemindersEnabled`) |
| `PreferenceAwareNotificationDispatcher` | Gates by channel + prefs |
| **Delivery (38A/38B)** | Resend + Twilio when configured; log fallback in Development/Testing without keys |
| **Appointment reminder scheduler (38B)** | `AppointmentReminderBackgroundService` — 24h/2h email/SMS; entitlement-gated follow-up email |
| **Receipt email (38B)** | `PaymentCompleted` → PDF via Application **`IReportRenderer`** attachment |

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

**Reporting / Finance (38B):** Notifications **sends** messages; receipt PDF bytes come from Application **`IReportRenderer`** (Reporting.Infrastructure implements the port). **38A has no Reporting dependency.** Do not embed QuestPDF in the dispatcher.

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
- `SmsRemindersEnabled` — gates SMS channel when Twilio + `smsEnabled` entitlement

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
| **38A** | Resend + Twilio; `NotificationSenderConfig` (CA/NG); idempotency; email-first flows; **no Reporting** |
| **39A** | Reporting extract + **`IReportRenderer`** (prerequisite for receipt PDF) |
| **38B** | Receipt PDF email; scheduled reminders (entitlement tiers); waitlist SMS; Retention event handlers — see [sprint-38 §7](./specs/sprint-38-notifications-module.md#7-event--notification-matrix) |
| **Backlog** | FCM push registration + fan-out |

---

## Tests

- Preference gating skips disabled channels
- Mock provider integration on `BookingConfirmed`
- Duplicate domain event does not double-send (idempotency)

Run: `dotnet test Adeni.slnx -c Release`
