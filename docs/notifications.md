# Notifications — transactional outbound delivery

Async **email**, **SMS**, and (later) **push** driven by **domain events** and **tenant preferences**. In-app chat and live UI updates stay in **Messaging** and **Operations** (SignalR).

**Boundaries:** [specs/notifications-module-boundaries.md](./specs/notifications-module-boundaries.md)  
**Implementation:** [sprint-38-notifications-module.md](./specs/sprint-38-notifications-module.md) (38A / 38B), [sprint-38c-notification-templates-i18n.md](./specs/sprint-38c-notification-templates-i18n.md) (templates + locale)  
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
| **Appointment reminder scheduler (38B)** | **Hangfire** recurring job → `AppointmentReminderProcessor` — 24h/2h email/SMS; entitlement-gated follow-up email |
| **Event-driven sends (38B+)** | App code → **`INotificationDispatcher.SendAsync`** only → after-commit → Hangfire `NotificationSendHangfireJob` (automatic retry); inline worker in **Testing** |
| **Receipt email (38B)** | `PaymentCompleted` → same dispatcher with `PaymentReceipt` metadata → worker issues receipt + PDF via **`IReportRenderer`**, then **`INotificationDeliveryDispatcher`** |
| **Templates + i18n (38c)** | **`INotificationTemplateRenderer`** in worker; catalog in `@adeni/shared`; tenant **`defaultNotificationLocale`** + Pro+ overrides; portal **Settings → Notifications** |

---

## Architecture

```
Domain events (Booking, Waitlist, Messaging, Finance, Queue, Identity, …)
        ↓
  IDomainEventHandler<*> / QueueService / StaffAccess / BillingDocument — resolve recipients, build message
        ↓
  INotificationDispatcher (DeferredNotificationDispatcher) — after-commit enqueue (Hangfire) or inline worker (Testing)
        ↓
  NotificationSendHangfireJob → NotificationSendWorker → INotificationTemplateRenderer (locale + overrides) → INotificationDeliveryDispatcher
        ↓
  PreferenceAwareNotificationDispatcher
        ↓
  ChannelNotificationDispatcher
     ├─ email  → IEmailNotificationProvider → Resend
     ├─ sms    → ISmsNotificationProvider   → Twilio
     ├─ push   → IPushNotificationProvider  → FCM (Phase 4)
     └─ (dev)  → Logging when API keys absent
```

**After-commit:** `AdeniDbContext.SaveChangesAsync` dispatches collected domain events only after the database write succeeds. All outbound sends call **`INotificationDispatcher.SendAsync`**, which queues Hangfire work (or runs **`NotificationSendWorker`** inline) via scoped **`IAfterCommitWorkQueue`** (nested `SaveChanges` depth tracking) so jobs are not enqueued until the outer save completes. **Testing** has no Hangfire — the outer dispatcher invokes the worker on the request thread after commit. Hangfire workers resolve **`INotificationDeliveryDispatcher`** only (no double enqueue).

| Layer | Location |
|-------|----------|
| Application | `INotificationDispatcher`, `INotificationDeliveryDispatcher`, `INotificationPreferencesService`, provider ports |
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
| Queue ticket called (`QueueService`) | email | Same scheduler + worker path; dedupe `queue-called:{ticketId}`; SMS when product switches channel |

Realtime **SignalR** events are **not** part of this module.

---

## Tenant preferences

Stored on `TenantNotificationPreferences`:

- `NotificationLanguageMode`: `single` \| `bilingual` — bilingual merges en+fr in **one email**; SMS uses `DefaultNotificationLocale` only.

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

## Reminders & transactional (38A/38B)

| Capability | Status |
|------------|--------|
| Booking confirmed / rejected / cancelled email | Shipped (Resend + recipient resolution) |
| Staff invite, conversation customer email | Shipped |
| **Payment receipt email + PDF** | Shipped — PDF via Application **`IReportRenderer`** (38B) |
| **Scheduled appointment reminders** (24h / 2h email + SMS) | Shipped — Hangfire recurring job (`notifications:appointment-reminders`); idempotent `notification_deliveries`; entitlement tiers (`basicReminders`, `followUpAutomation`) |
| **Waitlist slot opened** | Shipped — email + SMS; burst cap `MaxWaitlistNotificationsPerSlot` |
| **Walk-in queue “you’re up”** (`QueueService` Call) | Shipped — `INotificationDispatcher` + after-commit worker (same stack as domain handlers) |
| No-show follow-up email | Shipped when `followUpAutomation` entitlement |
| Retention-triggered notifications | Deferred until Sprint 37 domain events |

**Config (`Notifications` section):**

| Key | Default | Notes |
|-----|---------|--------|
| `Reminders:Enabled` | `true` | Job no-ops when `false`; Hangfire **not registered** in **Testing** |
| `Reminders:RecurringCron` | `*/5 * * * *` | Hangfire cron for reminder scan (every 5 minutes) |
| `Reminders:NoShowGraceAfterEnd` | `2:00:00` | Follow-up window after appointment end |
| `MaxWaitlistNotificationsPerSlot` | `25` | Burst cap per opened slot |

**Hangfire:** Uses SQL Server storage on `ConnectionStrings:AdeniDb` (schema `HangFire`). Tables are created on first API start when Hangfire is enabled. Dashboard: **Development only** at `/admin/hangfire` (not exposed in Staging/Production). Requires a real SQL connection string — in-memory EF (empty `AdeniDb`) skips Hangfire registration.

**Templates (38c):** Deploy `db/scripts/notifications-sprint38c-templates.sql` (or publish dacpac) for `DefaultNotificationLocale`, override table, and `identity.customers.PreferredLocale`. Catalog: `packages/shared/src/data/notification-templates.json`.

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
| **38B** | ✅ Receipt PDF email; scheduled reminders; waitlist SMS; no-show follow-up — Retention handlers when Sprint 37 events exist |
| **Backlog** | FCM push; owner delivery status UI |

---

## Tests

- Preference gating skips disabled channels
- Mock provider integration on `BookingConfirmed`
- Duplicate domain event does not double-send (idempotency)

Run: `dotnet test Adeni.slnx -c Release`
