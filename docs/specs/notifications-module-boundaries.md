# Spec: Notifications module boundaries

| Field | Value |
|-------|-------|
| **Sprint** | Cross-cutting — Notifications (transactional delivery) |
| **Author** | Team |
| **Status** | Approved — Phase 1+ backlog (Resend + Twilio) |
| **Created** | 2026-10-19 |

---

## 1. Goal (one sentence)

Establish **Notifications** as the extractable bounded context for **transactional outbound messages** (email, SMS, later push/WhatsApp API) — driven by domain events and tenant preferences — without coupling controllers to vendors.

---

## 2. Context

- [tooling-policy.md](../tooling-policy.md) — **Resend** (email), **Twilio** (SMS / WhatsApp API later), **FCM+APNs** (push)
- [architecture.md](../architecture.md) — Notifications module; domain events → handlers
- [roadmap-modules.md](../roadmap-modules.md) #12 Notifications / reminders — **Partial**
- **Messaging** (Sprint 36) — in-app SignalR conversations; **not** replaced by Notifications
- **Reporting/Finance** — email **attachments** (PDF) supplied as bytes; Notifications sends, does not render PDF

**Shipped today:**

- `INotificationDispatcher`, `INotificationPreferencesService`
- `PreferenceAwareNotificationDispatcher` → **`LoggingNotificationDispatcher`** (stub)
- Handlers: booking confirmed/rejected/cancelled, waitlist slot opened, new conversation message

---

## 3. Decision record

| # | Decision |
|---|----------|
| 1 | **Notifications** is the peel-off module for **async delivery**; realtime UI stays **Messaging** + **Operations** SignalR hubs. |
| 2 | **Transactional email** → **Resend** adapter behind `IEmailNotificationProvider`. |
| 3 | **SMS** (reminders, opt-in) → **Twilio** adapter behind `ISmsNotificationProvider`. |
| 4 | Keep **`INotificationDispatcher`** as the single Application entry; **no** Resend/Twilio calls from controllers. |
| 5 | **Tenant preferences** gate channels (`EmailEnabled`, `SmsWhatsAppReminderEnabled`, `PushEnabled`) — already on `TenantNotificationPreferences`. |
| 6 | **WhatsApp deep links** (`wa.me`) remain client-side for “message business” — **not** Cloud API unless user/tenant opted in (Twilio WhatsApp Phase 3+). |
| 7 | **Push (FCM/APNs)** is a separate channel and provider — Phase 4; not a Twilio substitute. |
| 8 | **Idempotency:** store outbound `notification_deliveries` (or equivalent) with dedupe key per event + channel + recipient to survive event replay. |
| 9 | **PII:** never log full email/phone/body; use `PiiMasker` in audit metadata only. |
| 10 | **Marketing drips / CRM** — out of scope V1. |

---

## 4. In scope (by phase)

### Phase 1 — Vendor adapters (staging)

- [x] This spec approved
- [ ] `AddNotificationsModule()` registers Resend + Twilio when configured (stub/logging when keys missing in Development)
- [ ] `IEmailNotificationProvider` + `ResendEmailNotificationProvider`
- [ ] `ISmsNotificationProvider` + `TwilioSmsNotificationProvider`
- [ ] Replace inner `LoggingNotificationDispatcher` with **`ChannelNotificationDispatcher`** (routes by `message.Channel`)
- [ ] Config: `Resend:ApiKey`, `Resend:From`, `Twilio:AccountSid`, `Twilio:AuthToken`, `Twilio:FromNumber`
- [ ] Integration test with mock HTTP handler (no live send in CI)

### Phase 2 — Templates + high-value flows

- [ ] Typed templates (booking confirmed, rejected, cancelled, receipt emailed, staff invite)
- [ ] Wire **Finance/Reporting** receipt email to Resend with PDF attachment
- [ ] Queue/waitlist SMS when `SmsWhatsAppReminderEnabled` (Twilio)
- [ ] Rate limits on public-adjacent triggers (align Sprint 20 infra)

### Phase 3 — WhatsApp API (optional, opt-in)

- [ ] Twilio WhatsApp templates for appointment reminders (market/legal review)
- [ ] Separate preference flag if needed beyond `SmsWhatsAppReminderEnabled`

### Phase 4 — Push

- [ ] Device registration table + FCM sender
- [ ] Fan-out from same domain events as email/SMS

---

## 5. Out of scope

- In-app chat delivery (Messaging module)
- SignalR realtime badges (Operations/Messaging hubs)
- Marketing newsletters, drip campaigns, Intercom-style CRM
- Replacing Auth0 email (password reset) — Auth0 owns auth emails unless product decides otherwise
- Sending payment webhooks or provider callbacks

---

## 6. Architecture stack (canonical)

```
Domain events (Booking, Waitlist, Messaging, Finance, …)
        ↓
  IDomainEventHandler<*>  (Notifications.Infrastructure)
        ↓
  INotificationDispatcher
        ↓
  PreferenceAwareNotificationDispatcher
        ↓
  ChannelNotificationDispatcher
     ├─ email  → IEmailNotificationProvider → Resend
     ├─ sms    → ISmsNotificationProvider   → Twilio
     ├─ push   → IPushNotificationProvider  → FCM (Phase 4)
     └─ (dev)  → LoggingNotificationProvider when keys absent
```

**`NotificationMessage` (evolve carefully):**

| Field | Usage |
|-------|--------|
| `Channel` | `email` \| `sms` \| `push` \| `whatsapp` (future) |
| `RecipientKey` | Email address, E.164 phone, or device token reference — **never log raw** |
| `Subject` | Email subject; SMS preview ignored or truncated |
| `Body` | Plain/HTML email body or SMS text |
| `TenantId` | Preference gating + audit |

Phase 2+: optional `TemplateId`, `TemplateData`, `Attachment[]` on a new `SendNotificationRequest` if `NotificationMessage` is too narrow.

---

## 7. Module map

```
Notifications (extractable)
├── Dispatcher     — INotificationDispatcher, preferences
├── Providers      — Resend, Twilio, (FCM later)
├── Handlers       — subscribe to domain events
└── Deliveries     — idempotency + audit trail (Phase 1 table)

Messaging (peer)     — conversations, SignalR
Reporting (peer)     — PDF bytes for attachment
Finance (peer)       — payment completed → optional receipt email trigger
```

**Application ports:**

| Port | Responsibility |
|------|----------------|
| `INotificationDispatcher` | Send (existing) |
| `INotificationPreferencesService` | Tenant toggles (existing) |
| `IEmailNotificationProvider` | Resend send |
| `ISmsNotificationProvider` | Twilio SMS send |

---

## 8. Event → notification matrix (initial)

| Event | Channels (default) | Preference gates |
|-------|-------------------|-------------------|
| `BookingConfirmed` | email (+ SMS later) | Email / SMS prefs |
| `BookingRejected` | email | Email |
| `BookingCancelled` | email | Email |
| `SlotOpened` (waitlist) | email, SMS | Email / SMS |
| `ConversationMessageCreated` | email (digest/stub today) | Email; respect messaging entitlement |
| `PaymentCompleted` | email (receipt) | Email + Finance triggers Reporting PDF |

Handlers stay in **Notifications.Infrastructure**; they call other modules only via **Application ports** (e.g. `IBillingDocumentService` / Reporting for attachment).

---

## 9. API contract

Notifications is **mostly internal**. Tenant-facing surface today:

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET/PATCH` | `/api/v1/tenant/settings/notifications` | Business | Preferences (existing or settings page) |

**No public webhook** for Resend/Twilio inbound in V1 unless product adds SMS reply handling (out of scope).

Admin: delivery failure visibility → **admin audit / ops dashboard** (Phase 2), not customer API.

---

## 10. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Preferences per tenant; recipients must belong to booking/customer context |
| **Secrets** | Resend/Twilio keys in Key Vault staging/prod — never committed |
| **Audit** | Log `notification.sent` / `notification.failed` with masked recipient + template id |
| **Compliance** | SMS/WhatsApp opt-in; NDPR/privacy launch gate [legal-launch-checklist.md](../legal-launch-checklist.md) |
| **Murphy** | Not applicable to inbound webhooks for Notifications V1; Finance payment webhooks unchanged |

---

## 11. Tests

- [ ] Unit: preference gating skips disabled channel
- [ ] Unit: dispatcher routes `email` → mock Resend provider
- [ ] Integration: handler on `BookingConfirmed` enqueues send (mock provider)
- [ ] Idempotency: duplicate event does not double-send

Run: `dotnet test Adeni.slnx -c Release`

---

## 12. Acceptance criteria (Phase 1)

- [ ] Staging can send real email via Resend for one template (booking confirmed)
- [ ] Staging can send SMS via Twilio when preference enabled
- [ ] Development without keys still logs (no throw on startup)
- [ ] No Resend/Twilio references in `Adeni.Api` controllers
- [ ] [tooling-policy.md](../tooling-policy.md) unchanged on channel vendors (already Resend + Twilio)

---

## 13. Open questions

| Question | Decision |
|----------|----------|
| ACS Email instead of Resend? | Resend preferred per tooling policy; ACS optional alt |
| Single Twilio number per env vs per market | Start one; split NG/CA numbers when volume requires |
| Auth0 transactional emails | Out of Notifications V1 unless unified later |
