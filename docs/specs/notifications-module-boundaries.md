# Spec: Notifications module boundaries

| Field | Value |
|-------|-------|
| **Sprint** | Cross-cutting — Notifications (transactional delivery) |
| **Author** | Team |
| **Status** | Approved — boundaries; **implementation authority:** [sprint-38-notifications-module.md](./sprint-38-notifications-module.md) |
| **Created** | 2026-10-19 |

---

## 1. Goal (one sentence)

Establish **Notifications** as the extractable bounded context for **transactional outbound messages** (email, SMS, later push) — driven by domain events and tenant preferences — without coupling controllers to vendors.

---

## 2. Context

- [tooling-policy.md](../tooling-policy.md) — **Resend** (email), **Twilio** (SMS), **FCM+APNs** (push)
- [architecture.md](../architecture.md) — Notifications module; domain events → handlers
- [roadmap-modules.md](../roadmap-modules.md) #12 Notifications / reminders — **Partial** (code complete; staging send verification = launch gate)
- **Messaging** (Sprint 36) — in-app SignalR conversations; **not** replaced by Notifications
- **Reporting/Finance** — receipt PDF bytes via Application **`IReportRenderer`** (38B only); Notifications sends, does not render PDF
- **Implementation:** [sprint-38-notifications-module.md](./sprint-38-notifications-module.md) — **38A** (no Reporting dependency), **38B** (receipt PDF after Reporting 39A)

**Shipped (Sprint 38A):**

- `INotificationDispatcher` stack: preferences → entitlements (`smsEnabled`) → idempotency → **`ChannelNotificationDispatcher`**
- **`IEmailNotificationProvider`** (Resend) + **`ISmsNotificationProvider`** (Twilio); logging fallback when keys absent (Dev/Testing)
- **`notification_deliveries`** dedupe; **`INotificationRecipientResolver`** (booking/customer ports)
- Handlers: booking confirmed/rejected/cancelled, waitlist slot opened, reminders, payment receipt, staff invite, conversation customer email, queue called, billing document email (**38B**)
- Tenant pref **`SmsRemindersEnabled`** (SMS-only; no WhatsApp API)

---

## 3. Decision record

| # | Decision |
|---|----------|
| 1 | **Notifications** is the peel-off module for **async delivery**; realtime UI stays **Messaging** + **Operations** SignalR hubs. |
| 2 | **Transactional email** → **Resend** adapter behind `IEmailNotificationProvider`. |
| 3 | **SMS** (reminders, opt-in) → **Twilio** adapter behind `ISmsNotificationProvider`. |
| 4 | Keep **`INotificationDispatcher`** as the single Application entry; **no** Resend/Twilio calls from controllers. |
| 5 | **Tenant preferences** gate channels (`EmailEnabled`, **`SmsRemindersEnabled`** after rename, `PushEnabled`) on `TenantNotificationPreferences`. |
| 6 | **`wa.me` / client contact buttons** remain **client-side only** — **Twilio WhatsApp Cloud/API is not V1** (no WhatsApp templates or automated WhatsApp in this module). |
| 7 | **Push (FCM/APNs)** is a separate channel and provider — backlog after 38B; not a Twilio substitute. |
| 8 | **Idempotency:** store outbound `notification_deliveries` with dedupe key per event + channel + recipient to survive event replay. |
| 9 | **PII:** never log full email/phone/body; use `PiiMasker` in audit metadata only. |
| 10 | **Marketing drips / CRM** — out of scope V1. |
| 11 | **Country-aware senders:** `NotificationSenderConfig` per market (e.g. **CA**, **NG**) — do not assume one global Twilio number for all tenants. |
| 12 | **Entitlements:** reminder tiers and `smsEnabled` per [sprint-38-notifications-module.md §6](./sprint-38-notifications-module.md#6-reminder--sms-entitlement-semantics); [entitlements closeout](./sprint-33-entitlements-closeout.md). |

---

## 4. In scope (by sprint phase)

> Detail, checklists, and acceptance criteria: **[sprint-38-notifications-module.md](./sprint-38-notifications-module.md)**.

### 38A — Notifications Foundation (no Reporting dependency)

- Resend + Twilio adapters, `ChannelNotificationDispatcher`, `notification_deliveries` idempotency
- Recipient resolution via Booking/Customer **Application ports**
- **`NotificationSenderConfig`** — country-aware email/SMS sender resolution
- Preference rename: **`SmsRemindersEnabled`**
- High-value **email-only** flows (booking confirmed/rejected/cancelled, staff invite, conversation stub)

**Not in 38A:** receipt PDF attachment, scheduled reminder worker, Reporting Infrastructure references.

### 38B — Notifications Completion (after Reporting **39A**)

- **Receipt email** with PDF — handler calls **`IReportRenderer`** (Application port only; never QuestPDF or Reporting Infrastructure from Notifications)
- Scheduled appointment reminders (entitlement-gated: `basicReminders`, `configurableReminders`, `followUpAutomation`)
- Waitlist SMS polish, follow-up automation, **Retention** domain event handlers (Retention raises; Notifications delivers)

**Retention events (38B+):** full matrix in [sprint-38-notifications-module.md §7](./sprint-38-notifications-module.md#7-event--notification-matrix) — e.g. `MembershipActivated`, `MembershipRenewed`, `MembershipPaymentFailed`, `MembershipExpiring`, `MembershipCancelled`, `BenefitExpiring`, `PackageExpiring`, loyalty/referral events.

### Push (backlog)

- Device registration + FCM sender; fan-out from same events as email/SMS

---

## 5. Out of scope

- In-app chat delivery (Messaging module)
- SignalR realtime badges (Operations/Messaging hubs)
- **Twilio WhatsApp API**, WhatsApp templates, automated WhatsApp reminders — **`wa.me` stays client-only**
- Marketing newsletters, drip campaigns, Intercom-style CRM
- Replacing Auth0 email (password reset) — Auth0 owns auth emails unless product decides otherwise
- Inbound Resend/Twilio webhooks / SMS reply handling (V1)
- Payment provider webhooks (Finance — **Murphy** signature + idempotency unchanged on Finance routes)
- Metered **SMS usage billing** (`smsUsageBilling` reserved; not implemented in Sprint 38)

---

## 6. Architecture stack (canonical)

```
Domain events (Booking, Waitlist, Messaging, Finance, Retention, …)
        ↓
  IDomainEventHandler<*>  (Notifications.Infrastructure)
        ↓
  INotificationDispatcher
        ↓
  PreferenceAwareNotificationDispatcher  (SmsRemindersEnabled)
        ↓
  EntitlementAwareDecorator — smsEnabled, basicReminders / configurableReminders / followUpAutomation
        ↓
  ChannelNotificationDispatcher
     ├─ email  → IEmailNotificationProvider → Resend (From via NotificationSenderConfig)
     ├─ sms    → ISmsNotificationProvider   → Twilio (sender per country/market)
     ├─ push   → IPushNotificationProvider  → FCM (backlog)
     └─ (dev)  → LoggingNotificationProvider when keys absent
```

**`NotificationMessage` (evolve carefully):**

| Field | Usage |
|-------|--------|
| `Channel` | `email` \| `sms` \| `push` |
| `RecipientKey` | Email address, E.164 phone, or device token reference — **never log raw** |
| `Subject` | Email subject; SMS preview ignored or truncated |
| `Body` | Plain/HTML email body or SMS text |
| `TenantId` | Preference gating + audit |

38B+: optional `TemplateId`, `TemplateData`, `Attachment[]` on a new `SendNotificationRequest` if `NotificationMessage` is too narrow.

---

## 7. Module map

```
Notifications (extractable)
├── Dispatcher     — INotificationDispatcher, preferences, entitlement decorator
├── Providers      — Resend, Twilio, (FCM later)
├── Handlers       — subscribe to domain events (incl. Retention in 38B)
└── Deliveries     — idempotency + audit trail

Messaging (peer)     — conversations, SignalR
Reporting (peer)     — IReportRenderer → PDF bytes (38B only)
Finance (peer)       — payment completed → optional receipt email trigger
Retention (peer)     — raises transactional events; never calls Resend/Twilio
```

**Application ports:**

| Port | Responsibility |
|------|----------------|
| `INotificationDispatcher` | Send (existing) |
| `INotificationPreferencesService` | Tenant toggles (existing) |
| `IEmailNotificationProvider` | Resend send |
| `ISmsNotificationProvider` | Twilio SMS send |
| `IReportRenderer` | PDF bytes for receipt email (**38B**; implemented in Reporting.Infrastructure) |

---

## 8. Event → notification matrix (initial)

| Event | Channels (default) | Preference gates | Entitlements |
|-------|-------------------|------------------|--------------|
| `BookingConfirmed` | email (+ SMS 38B) | Email / **SmsRemindersEnabled** | Reminder tiers (38B) |
| `BookingRejected` | email | Email | — |
| `BookingCancelled` | email | Email | — |
| `SlotOpened` (waitlist) | email, SMS | Email / SMS prefs | `waitlist`, `smsEnabled` for SMS |
| `ConversationMessageCreated` | email (digest/stub) | Email | `messaging` |
| `PaymentCompleted` | email + PDF (38B) | Email | — (receipt via **IReportRenderer**) |
| Scheduled reminder job | email, SMS (38B) | prefs | §6 in [sprint-38 spec](./sprint-38-notifications-module.md#6-reminder--sms-entitlement-semantics) |

**Retention → Notifications:** see [sprint-38-notifications-module.md §7](./sprint-38-notifications-module.md#7-event--notification-matrix).

Handlers stay in **Notifications.Infrastructure**; cross-module calls only via **Application ports** (never Reporting Infrastructure from Notifications).

---

## 9. API contract

Notifications is **mostly internal**. Tenant-facing surface today:

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET/PATCH` | `/api/v1/tenant/notification-preferences` | Business JWT + `X-Tenant-Id` | Channel toggles, locales, Pro+ template overrides; reject SMS enable when `smsEnabled` false |

**No public webhook** for Resend/Twilio inbound in V1 unless product adds SMS reply handling (out of scope).

Admin: delivery failure visibility → **admin audit / ops dashboard** (38B), not customer API.

---

## 10. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Preferences per tenant; recipients must belong to booking/customer context |
| **Secrets** | Resend/Twilio keys in Key Vault staging/prod — never committed |
| **Audit** | Log `notification.sent` / `notification.failed` with masked recipient + template id |
| **Compliance** | SMS opt-in; NDPR/privacy launch gate [legal-launch-checklist.md](../legal-launch-checklist.md) |
| **Murphy** | Not applicable to inbound webhooks for Notifications V1; **Finance payment webhooks unchanged** (signature verify on raw body, idempotent processing). Future inbound provider callbacks follow same discipline per [AGENTS.md](../../AGENTS.md). |

---

## 11. Tests

- [ ] Unit: preference gating skips disabled channel
- [ ] Unit: dispatcher routes `email` → mock Resend provider
- [ ] Integration: handler on `BookingConfirmed` enqueues send (mock provider)
- [ ] Idempotency: duplicate event does not double-send
- [ ] 38B: receipt handler uses mock **`IReportRenderer`** — no Reporting.Infrastructure reference

Run: `dotnet test Adeni.slnx -c Release`

---

## 12. Acceptance criteria

See **[sprint-38-notifications-module.md §12](./sprint-38-notifications-module.md#12-acceptance-criteria)** (38A / 38B).

---

## 13. Open questions

| Question | Decision |
|----------|----------|
| ACS Email instead of Resend? | Resend preferred per tooling policy; ACS optional alt |
| Per-market Twilio/Resend senders | **`NotificationSenderConfig`** rows for CA + NG; env fallback — see Sprint 38 §5.1 |
| Auth0 transactional emails | Out of Notifications V1 unless unified later |
| `SmsRemindersEnabled` naming | **Default** — align with closeout + 38A migration |
