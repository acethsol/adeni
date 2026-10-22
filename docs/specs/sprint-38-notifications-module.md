# Spec: Sprint 38 — Notifications module (Resend + Twilio)

> Implements [notifications-module-boundaries.md](./notifications-module-boundaries.md) and [notifications.md](../notifications.md) as a **sprint-gated delivery track**.

## IMPLEMENTATION ORDER

1. **Entitlements closeout** ([sprint-33-entitlements-closeout.md](./sprint-33-entitlements-closeout.md))
2. **Notifications Foundation — 38A** (this spec §3.1) — **no Reporting dependency**
3. **Reporting Foundation — 39A** ([sprint-39-reporting-module.md](./sprint-39-reporting-module.md) §3.1) — QuestPDF extract + `IReportRenderer`
4. **Notifications Completion — 38B** (this spec §3.2) — receipt PDF email via **Application** `IReportRenderer` port only
5. **Reporting Completion — 39B** ([sprint-39-reporting-module.md](./sprint-39-reporting-module.md) §3.2)
6. **Retention staged delivery — 37.1–37.5** ([sprint-37-retention-module.md](./sprint-37-retention-module.md))

**Dependency rule:** No **Reporting → Notifications** dependency. Notifications must **not** reference Reporting Infrastructure or EF. Notifications may call **Application-layer** ports (`IReportRenderer` for PDF bytes in 38B; recipient resolution via Booking/Customer ports).

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 38 — Notifications module |
| **Author** | |
| **Status** | **38A done** (2026-10-22); **38B** next (**39A** unblocks receipt PDF) |
| **Created** | 2026-10-21 |
| **Depends on** | Entitlements closeout (reminder/SMS keys), Sprint 36 messaging (events). **38B** depends on **39A** (`IReportRenderer`). |

---

## 1. Goal (one sentence)

Replace log-only notification delivery with **production Resend email and Twilio SMS**, wired through domain events, tenant preferences, and **entitlement-gated reminder tiers**—without coupling controllers to vendors, duplicating Messaging’s SignalR role, or creating circular module dependencies with Reporting.

---

## 2. Context

- **Boundaries (approved):** [notifications-module-boundaries.md](./notifications-module-boundaries.md) — **WhatsApp Cloud/API is out of scope for Sprint 38** (see §4); client `wa.me` links stay outside this module.
- **Runbook:** [notifications.md](../notifications.md)
- **Tooling:** [tooling-policy.md](../tooling-policy.md) — Resend, Twilio SMS; FCM when Flutter consumer ships
- **Messaging (36):** In-app conversations + SignalR; Notifications handles **async outbound** only
- **Reporting (39A/39B):** Owns QuestPDF rendering; Notifications **requests PDF bytes** via **`IReportRenderer`** (Application port) in **38B only**
- **Entitlements:** Reminder and SMS keys from [plan-entitlements.json](../../packages/shared/src/data/plan-entitlements.json) — see §6
- **Shipped (38A):** Resend/Twilio adapters + log fallback; `ChannelNotificationDispatcher`; idempotent **`notification_deliveries`**; entitlement + preference stack; **`SmsRemindersEnabled`**; booking/staff/conversation email paths; `NotificationSenderConfig` (CA/NG). **38B:** reminders, receipt PDF via `IReportRenderer`, waitlist SMS polish.

---

## 3. In scope

### 3.1 Notifications Foundation — **38A** (no Reporting dependency)

- [x] `AddNotificationsModule()` registers Resend + Twilio when configured; Development/Testing falls back to logging when keys absent (no startup throw)
- [x] `IEmailNotificationProvider` + `ResendEmailNotificationProvider`
- [x] `ISmsNotificationProvider` + `TwilioSmsNotificationProvider`
- [x] `ChannelNotificationDispatcher` routes by `NotificationMessage.Channel` (`email` \| `sms` \| `push` future)
- [x] **`notification_deliveries`** table: dedupe key = `eventId + channel + recipientKey`; survive domain event replay
- [x] Fix **recipient resolution** — email/SMS to customer contact from Booking/Customer **Application ports**, not raw customer id stubs
- [x] **Country-aware sender resolution** — `NotificationSenderConfig` (§5); avoid single global Twilio number assumption for CA + NG
- [x] Config secrets: `Resend:ApiKey`, `Resend:From`, Twilio credentials, per-market sender rows — Key Vault staging/prod
- [ ] Typed template **foundation** (IDs, data bags; full flows in 38B)
- [x] Integration tests with mock HTTP handlers (no live send in CI)
- [x] **Preference rename (spec + migration):** `SmsWhatsAppReminderEnabled` → **`SmsRemindersEnabled`** on API/DTOs/entity when implemented (SMS-only; no WhatsApp in pref name)
- [x] High-value **email-only** paths where Reporting not required: booking confirmed/rejected/cancelled, staff invite, conversation email stub

**Explicitly not in 38A:** Receipt PDF attachment, scheduled reminder worker, waitlist SMS polish, follow-up automation.

### 3.2 Notifications Completion — **38B** (after Reporting **39A**)

- [ ] **Receipt email** with PDF attachment — handler calls **`IReportRenderer`** (Application) for bytes; **never** QuestPDF or Reporting Infrastructure from Notifications
- [ ] **Scheduled appointment reminders** — background job: 24h/2h windows (Starter fixed schedule); idempotent delivery rows
- [ ] **Entitlement-gated reminder behavior** (§6): Starter `basicReminders` only; Pro/Business add `configurableReminders` + `followUpAutomation` (not Business-only)
- [ ] **SMS gating:** `SmsRemindersEnabled` tenant pref **and** `smsEnabled` entitlement before Twilio send; **`smsUsageBilling`** not implemented (reserved)
- [ ] Waitlist `SlotOpened` — email + SMS when entitled + prefs on
- [ ] **Follow-up automation** (minimal v1): e.g. second-chance no-show email when `followUpAutomation` true
- [ ] Rate limits on burst-prone triggers (align Sprint 20 infra baseline)
- [ ] **Retention domain events** (§7) — handlers subscribe; Retention never calls Resend/Twilio

### 3.3 Push (backlog — after 38B or parallel Sprint 20)

- [ ] Device registration + FCM sender; fan-out from same events as email/SMS

### Portal surfaces

- [ ] Settings: existing `GET/PATCH` notification preferences — SMS toggle disabled/hidden when `smsEnabled` false ([closeout](./sprint-33-entitlements-closeout.md))
- [ ] Optional: delivery status read-only for owners (38B backlog)

---

## 4. Out of scope

- In-app chat delivery (Messaging)
- SignalR realtime badges (Operations/Messaging hubs)
- **Twilio WhatsApp API**, WhatsApp templates, automated WhatsApp reminders, WhatsApp inbox — **not V1**; **`wa.me` / client contact buttons** remain client-side only
- Marketing newsletters, drips, CRM campaigns (“double points this weekend” = marketing, not Notifications)
- Auth0 password-reset email (Auth0-owned unless product unifies later)
- Inbound Resend/Twilio webhooks / SMS reply handling (V1)
- Payment provider webhooks (Finance — Murphy rules stay on Finance)
- Metered **SMS usage billing** (`smsUsageBilling`) — flag reserved; no invoices until billing product exists
- **Email as metered add-on** — email remains core transactional channel

---

## 5. Architecture

```
Domain events (Booking, Waitlist, Messaging, Finance, Retention, Queue, …)
        ↓
  IDomainEventHandler<*>  (Notifications.Infrastructure)
        ↓
  INotificationDispatcher
        ↓
  PreferenceAwareNotificationDispatcher  (tenant prefs; SmsRemindersEnabled)
        ↓
  EntitlementAwareDecorator — smsEnabled, basicReminders / configurableReminders / followUpAutomation
        ↓
  ChannelNotificationDispatcher
     ├─ email  → IEmailNotificationProvider → Resend (From resolved via NotificationSenderConfig)
     ├─ sms    → ISmsNotificationProvider   → Twilio (sender resolved per country/market)
     └─ (dev)  → LoggingNotificationProvider
```

### 5.1 `NotificationSenderConfig` (country-aware)

Conceptual model (table, config section, or seed — follow existing market/billing conventions):

| Field | Purpose |
|-------|---------|
| `CountryCode` | e.g. `CA`, `NG` |
| `Channel` | `email` \| `sms` |
| `Provider` | `resend` \| `twilio` |
| `SenderIdOrPhoneNumber` | E.164 or alphanumeric sender id for SMS |
| `FromEmail` | Resend from-address for that market |

**Resolution:** Dispatcher resolves sender from tenant **billing/operational country** (Tenancy/market registry) with env fallback. **Do not** assume one Twilio number for all tenants in V1 docs or config shape.

### 5.2 Reporting integration (38B only)

| Port | Layer | Consumer | Provider |
|------|-------|----------|----------|
| `IReportRenderer` | Application | Notifications handler (receipt email), Finance (PDF HTTP) | Reporting.Infrastructure |

Notifications **must not** take a dependency on Reporting Infrastructure projects.

**Murphy (outbound):** Not payment webhooks. For **future** inbound provider callbacks: signature verify on raw body, idempotent processing — same discipline as [AGENTS.md](../../AGENTS.md).

**PII:** `PiiMasker` on audit/log metadata; never log full email/phone/body.

---

## 6. Reminder & SMS entitlement semantics

| SKU | Reminder keys | Behavior |
|-----|---------------|----------|
| **Starter** | `basicReminders` only | Fixed schedule templates (e.g. 24h/2h); no custom windows or follow-up automation APIs |
| **Professional** | `basicReminders`, `configurableReminders`, `followUpAutomation` | Configurable reminder windows + follow-up automation hooks |
| **Business** | Same as Professional | Same reminder keys (not Business-only for `followUpAutomation`) |

| Key | Role |
|-----|------|
| `smsEnabled` | Tenant may enable SMS prefs and receive Twilio sends |
| `smsUsageBilling` | **Deferred** — no metered SMS invoicing in Sprint 38 |

Email sends are **not** entitlement-metered for V1 transactional flows.

---

## 7. Event → notification matrix

### Core (Booking / Waitlist / Messaging / Finance)

| Event | Channels | Prefs | Entitlements |
|-------|----------|-------|--------------|
| `BookingConfirmed` | email (+ SMS 38B) | EmailEnabled / SmsRemindersEnabled | Reminder tiers for scheduled follow-ups |
| `BookingRejected` / `BookingCancelled` | email | EmailEnabled | — |
| `SlotOpened` (waitlist) | email, SMS | Email / SMS prefs | `waitlist`, `smsEnabled` for SMS |
| `ConversationMessageCreated` | email digest/stub | EmailEnabled | `messaging` |
| `PaymentCompleted` | email + PDF (38B) | EmailEnabled | — (receipt) |
| Scheduled reminder job | email, SMS | prefs | §6 reminder keys |

### Retention → Notifications (38B+; Retention raises, Notifications delivers)

Transactional only — Retention owns business rules; Notifications owns channel, prefs, entitlements, templates, vendors.

| Event | Typical channels | Notes |
|-------|------------------|-------|
| `MembershipActivated` | email | |
| `MembershipRenewed` | email | |
| `MembershipPaymentFailed` | email, SMS | PastDue policy from Retention |
| `MembershipExpiring` | email, SMS | |
| `MembershipCancelled` | email | |
| `BenefitExpiring` | email | |
| `PackageExpiring` | email, SMS | |
| `LoyaltyPointsEarned` | email (optional push later) | Transactional (“You earned N points”) |
| `LoyaltyPointsExpiring` | email | |
| `LoyaltyTierChanged` | email | |
| `RewardAvailable` | email | |
| `ReferralQualified` | email | |
| `ReferralRewardIssued` | email | |

Handlers call Finance/Reporting only via **Application ports** (e.g. `IReportRenderer` for attachments).

---

## 8. API contract

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/settings/notifications` | Business + tenant | Read prefs (`SmsRemindersEnabled` after rename) |
| `PATCH` | `/api/v1/tenant/settings/notifications` | Business + tenant | Update prefs; reject SMS enable when `smsEnabled` false (`entitlements.sms`) |

**No public `/api/v1/` webhook routes in V1** unless inbound SMS added (out of scope).

Admin delivery visibility: 38B via audit/ops, not customer API.

**Errors:** RFC 7807 + stable codes e.g. `notifications.delivery_failed`, `entitlements.sms`.

---

## 9. Files to touch

| Layer | Files |
|-------|-------|
| **Domain** | Optional `NotificationDelivery` entity; domain events unchanged |
| **Application** | `INotificationDispatcher`, providers, `NotificationSenderConfig` resolution port, reminder scheduler port; consume **`IReportRenderer`** in 38B only |
| **Infrastructure** | `AddNotificationsModule()`, Resend/Twilio adapters, handlers, EF `notification_deliveries`, hosted reminder worker (38B) |
| **Api** | Tenant settings controller (prefs); no vendor calls in controllers |
| **Tests** | Unit: prefs + entitlement gating; integration: mock provider + idempotency; 38B: mock `IReportRenderer` |
| **packages/shared** | Notification prefs schema (`SmsRemindersEnabled`); entitlement error keys if new |
| **apps/portal** | Settings notifications section; SMS toggle UX |

---

## 10. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Prefs per tenant; recipients resolved in booking/customer context only |
| **Auth / roles** | Business JWT + `X-Tenant-Id` on settings |
| **Secrets** | Resend/Twilio in Key Vault |
| **Audit** | `notification.sent` / `notification.failed` with masked recipient + template id |
| **Compliance** | SMS opt-in; NDPR launch gate |
| **Cache** | Invalidate prefs cache on PATCH if cached |

---

## 11. Tests

- [x] Preference gating skips disabled channel
- [x] `smsEnabled` false → PATCH SMS pref rejected; dispatcher skips SMS
- [x] Duplicate domain event → single delivery row
- [x] Mock Resend on `BookingConfirmed` integration test (38A)
- [ ] Reminder job idempotency for same booking window (38B)
- [ ] Receipt handler uses `IReportRenderer` mock — no Reporting.Infrastructure reference (architecture test)

Run: `dotnet test Adeni.slnx -c Release`

---

## 12. Acceptance criteria

### 38A

- [ ] Staging sends real booking confirmed **email** via Resend
- [ ] Staging sends SMS via Twilio when pref + `smsEnabled` (where 38A includes SMS paths)
- [x] Development without keys logs only
- [x] No Resend/Twilio in Api controllers
- [x] Idempotency table prevents double-send on replay
- [x] Sender resolution documented and testable per country config

### 38B

- [ ] Scheduled reminder respects tier keys (Starter vs Pro/Business)
- [ ] Receipt email includes PDF from **`IReportRenderer`** port
- [ ] Waitlist slot opened reaches resolved phone/email
- [ ] At least one Retention event wired end-to-end (mock)

---

## 13. Open questions

| Question | Decision |
|----------|----------|
| Reminder scheduler: Hangfire vs hosted `BackgroundService` | TBD — prefer existing infra pattern in repo |
| `SmsRemindersEnabled` vs `SmsNotificationsEnabled` | **Default `SmsRemindersEnabled`** — align with reminder-centric SMS; confirm in closeout PR |
| Entitlement decorator vs checks in each handler | Central decorator recommended |
| When to migrate DB column from `SmsWhatsAppReminderEnabled` | Same PR as 38A pref rename or additive column + deprecate |
