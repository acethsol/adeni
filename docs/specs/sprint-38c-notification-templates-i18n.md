# Spec: Sprint 38c — Notification templates, i18n, tenant portal config

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 38c — Notification templates + i18n |
| **Author** | Agent |
| **Status** | Done (2026-10-22) |
| **Created** | 2026-10-22 |
| **Depends on** | 38A/38B notifications pipeline; 33-closeout entitlements |
| **Pauses** | 39B reporting completion (exports/admin) |

---

## 1. Goal (one sentence)

Ship a **single-locale, template-driven** notification loop: central template IDs and default copy in `@adeni/shared`, tenant language + optional Pro+ text overrides in portal, rendering in `NotificationSendWorker` before Resend/Twilio.

---

## 2. Context

- Closes sprint-38 checkbox: typed template foundation ([sprint-38-notifications-module.md](./sprint-38-notifications-module.md) §3.1).
- Runbook: [notifications.md](../notifications.md)
- Entitlements: `configurableReminders` gates **custom template overrides**; **default notification locale** for all verified tenants.

---

## 3. In scope

- [ ] Stable `NotificationTemplateId` in `@adeni/shared` + `notification-templates.json` (en/fr defaults)
- [ ] `INotificationTemplateRenderer` + `INotificationLocaleResolver` (Application)
- [ ] Locale order: customer `PreferredLocale` (when set) → tenant `defaultNotificationLocale` → market default → `en`
- [ ] One locale per send (no dual-language unless future opt-in)
- [ ] Extend `NotificationMessage` / `NotificationJobPayload` with `templateId` + data; render in `NotificationSendWorker`
- [ ] Migrate send sites: booking, waitlist, reminders, finance, messaging, queue, staff invite, billing document email, payment receipt wrapper
- [ ] DB: `DefaultNotificationLocale` on preferences; `tenant_notification_template_overrides` table
- [ ] `GET/PATCH /api/v1/tenant/notification-preferences` extended contract in `packages/shared`
- [ ] Portal Settings → Notifications: language dropdown + override editors (booking confirmed, 24h/2h reminders, waitlist opened)
- [ ] Unit tests (renderer merge); integration test (French booking confirmed via mock email capture)
- [ ] Update sprint-38 foundation checkbox ✅

## 4. Out of scope

- Bilingual single message / dual sends
- Retention event templates (37.x)
- Push template catalog
- WhatsApp templates
- Marketing campaigns / drip content editor
- Customer self-service locale UI (backend reads `PreferredLocale` when populated)

---

## 5. API contract

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/tenant/notification-preferences` | Business JWT + `X-Tenant-Id` | Channel toggles + `defaultNotificationLocale` + `templateOverrides` |
| PATCH | `/api/v1/tenant/notification-preferences` | Business JWT + `X-Tenant-Id` | Same; rejects overrides when `configurableReminders` false |

**Shapes:** `notificationPreferencesSchema` in `packages/shared` — adds:

- `defaultNotificationLocale`: `en` \| `fr`
- `templateOverrides`: `{ templateId, locale, subject?, body? }[]` (max 32 rows; subject ≤ 200; body ≤ 2000 email / 320 SMS-aware validation server-side)

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `TenantNotificationPreferences`, `TenantNotificationTemplateOverride`, `Customer.PreferredLocale` |
| Application | `INotificationTemplateRenderer`, `INotificationLocaleResolver`, `NotificationTemplateIds`, prefs DTOs, `NotificationMessage` / `NotificationJobPayload` |
| Infrastructure | Renderer, locale resolver, catalog JSON loader, handlers/processors/senders, prefs service, EF config |
| Api | `TenantController` (unchanged routes) |
| db | `tenant_notification_preferences`, new overrides table, `customers.PreferredLocale`, post-deploy script |
| Tests | Renderer unit tests, booking French integration, update handler tests |
| packages/shared | `notification-templates.ts`, `data/notification-templates.json`, schemas, i18n portal labels |
| packages/api-client | prefs methods (schema parse) |
| apps/portal | settings notifications tab |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Overrides keyed by `TenantId`; query filters |
| **Auth / roles** | Business user for prefs PATCH |
| **Cache** | None (read prefs at render time in worker) |
| **Audit** | No admin mutations |
| **PII** | No full body in logs; template data masked where needed |
| **Entitlements** | Overrides require `configurableReminders`; locale pick always allowed |

---

## 8. Tests

- [ ] Unit: renderer locale + tenant override merge
- [ ] Integration: PATCH `defaultNotificationLocale=fr` + booking confirmed → French subject in email provider capture
- [ ] `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [ ] No hardcoded English subjects/bodies at enqueue sites (template id + data)
- [ ] Portal saves locale and overrides (Pro+)
- [ ] SMS uses same rendered body; templates kept concise
- [ ] sprint-38 typed template foundation checked

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| Override entitlement | `configurableReminders` (Pro/Business) |
| Customer locale source | `identity.customers.PreferredLocale` optional column; null → tenant/market fallback |
