# Spec: Sprint 38–39 — Launch closeout (Notifications + Reporting)

> Tracking checklist for V1 launch gaps after **38A/38B ✅**, **38c ✅**, **39A/39B ✅**.  
> **No new modules** — runbook alignment, staging proof, portal wiring. **Retention (37.x) excluded.**

| Field | Value |
|-------|-------|
| **Sprint** | 38–39 launch closeout (Sprint A / Sprint B) |
| **Author** | |
| **Status** | Draft |
| **Created** | 2026-10-22 |

---

## 1. Goal (one sentence)

Close the gap between **shipped Notifications and Reporting code** and **staging-verified, portal-honest V1 launch** — without Retention, push, WhatsApp API, or marketing scope.

---

## 2. Context

| Doc | Role |
|-----|------|
| [sprint-38-notifications-module.md](./sprint-38-notifications-module.md) | 38A/38B module spec |
| [sprint-38c-notification-templates-i18n.md](./sprint-38c-notification-templates-i18n.md) | Templates + i18n + portal Settings |
| [sprint-38c-bilingual-notifications-addendum.md](./sprint-38c-bilingual-notifications-addendum.md) | Bilingual delivery addendum |
| [sprint-39-reporting-module.md](./sprint-39-reporting-module.md) | 39A/39B exports + admin reporting |
| [shared-data-grid.md](./shared-data-grid.md) | List toolbar + export slot |
| [notifications.md](../notifications.md) | Notifications runbook |
| [reporting.md](../reporting.md) | Reporting runbook |
| [sprints.md](../sprints.md) | Sprint tracker |

**Baseline:** Gaps are **doc drift**, **staging ops**, and **portal callers** — not missing core handlers.

---

## 3. In scope — Notifications

### 3.1 Doc fixes (N-D1–D6)

- [ ] **N-D1** — [notifications.md](../notifications.md) event matrix: remove stale “customer id recipient / Phase 1 fix” and waitlist stub; align with `NotificationRecipientResolver` + `WaitlistNotificationHandler`
- [ ] **N-D2** — Runbook routes: `/tenant/settings/notifications` → `GET/PATCH /api/v1/tenant/notification-preferences`; “Phase 1 target” → production config
- [ ] **N-D3** — Clarify Dev/Testing log fallback vs Staging/Prod when Resend/Twilio keys present
- [ ] **N-D4** — [sprint-38c-notification-templates-i18n.md](./sprint-38c-notification-templates-i18n.md): sync §3 checkboxes with Done status / addendum
- [ ] **N-D5** — [notifications-module-boundaries.md](./notifications-module-boundaries.md) + [roadmap-modules.md](../roadmap-modules.md): code complete; launch = staging sends verified
- [ ] **N-D6** — [AGENTS.md](../../AGENTS.md) / sprint-33: subscription webhook HMAC implemented when secret set; reword “unsigned stub” → staging secret + E2E (**33c**)

### 3.2 Staging / ops (N-O1–O6)

- [ ] **N-O1** — Key Vault / appsettings (Staging/Prod): `Resend:*`, `Twilio:*`, `NotificationSenderConfig` (CA/NG)
- [ ] **N-O2** — SQL: `db/scripts/notifications-sprint38c-templates.sql` (or dacpac) — locales, overrides, `PreferredLocale`
- [ ] **N-O3** — Hangfire on real SQL; `Notifications:Reminders:Enabled=true`; job `notifications:appointment-reminders`
- [ ] **N-O4** — Resend domain + Twilio senders verified for CA/NG
- [ ] **N-O5** — Staging E2E (CA + NG tenant): booking email; waitlist email+SMS; receipt+PDF; 24h/2h reminder; queue called; staff invite; FR/bilingual; `notification_deliveries` idempotency
- [ ] **N-O6** — PII spot-check: masked email/phone on send failures (App Insights when Sprint 20 lands)

### 3.3 Portal (N-U1–U4)

- [ ] **N-U1** — Push toggle: hide or “Coming soon”; no false promise until FCM (`PushEnabled` prefs-only today)
- [ ] **N-U2** — 38c discoverability QA: `/settings/notifications`, gear menu, search
- [ ] **N-U3** — Template overrides (Pro+): editors + 403 when not entitled — QA verify
- [ ] **N-U4** — Owner delivery log — **defer** unless support needs at launch

### 3.4 33c adjacent (SaaS billing — not Notifications module)

- [ ] **N-33c1** — `SubscriptionBilling:Paystack:WebhookSecret` on staging; signature enforced outside Dev/Testing
- [ ] **N-33c2** — Live checkout → webhook → tier/MRR E2E (portal `/plan`, admin Insights)
- [ ] **N-33c3** — Do not block Notifications closeout on 33c unless product ties launch to paid SaaS self-serve

### 3.5 Testing (N-T1–T4)

- [ ] **N-T1** — `dotnet test Adeni.slnx -c Release` (gating, idempotency, reminders, mock Resend)
- [ ] **N-T2** — French booking + template override integration tests after 38c SQL on staging DB
- [ ] **N-T3** — Staging manual matrix **N-O5** + Resend dashboard spot-check
- [ ] **N-T4** — No production path with unsigned subscription webhooks when secret required

---

## 4. In scope — Reporting

### 4.1 Doc alignment (R-D1–D4)

- [ ] **R-D1** — [shared-data-grid.md](./shared-data-grid.md) §7 Phase 2: update checkboxes for portal/admin list exports
- [ ] **R-D2** — [reporting.md](../reporting.md) Frontend: `sales-summary` API exists; note missing portal entry; document PDF auth vs public token
- [ ] **R-D3** — [sprint-39-reporting-module.md](./sprint-39-reporting-module.md): `@adeni/brand` on PDFs — post-39B polish unless launch requires branded sales PDF
- [ ] **R-D4** — [roadmap-modules.md](../roadmap-modules.md) #5 Reporting: shipped pending portal export UX + staging audit

### 4.2 Portal (R-U1–U5)

- [ ] **R-U1** — Wire **sales-summary** export (`csv` / `pdf`) via `adeni-export-menu` on dashboard revenue and/or Finance; reuse dashboard date range
- [ ] **R-U2** — Authenticated PDF: `downloadBillingDocumentPdf()` in `@adeni/api-client` + **Download PDF** on Finance Documents (not public View only)
- [ ] **R-U3** — Multi-location: location filter on export flows or single-location auto-pass; map 403 to upgrade copy
- [ ] **R-U4** — Finance Staff export tab → shared export menu + xlsx when `excelExport` (drop fake json)
- [ ] **R-U5** — `advancedReports` helper + PDF disabled hints on export menus

### 4.3 Staging audit (R-O1–O2)

- [ ] **R-O1** — Admin MFA → platform MRR + GMV → confirm `admin.audit_logs` (`admin.reporting.read`)
- [ ] **R-O2** — Tenant export smoke: bookings csv, sales-summary csv/pdf (after R-U1), compensation xlsx (Pro)

### 4.4 Entitlements (R-E1)

- [ ] **R-E1** — QA: `portal.finance` vs bookings-self permissions match `ReportDefinitionRegistry`

### 4.5 Already in good shape (regression-only)

List exports, dashboard appointments widget, quote/statement PDFs, admin Insights MRR/GMV, backend export 403 + audit tests.

---

## 5. Out of scope

| Area | Notes |
|------|--------|
| **Retention (37.x)** | Domain events, packages, loyalty, membership notifications, `retention-membership-mrr`, `IRetentionAnalyticsService` widgets |
| **WhatsApp API** | V1 = client `wa.me` only; no Twilio WhatsApp Cloud |
| **FCM / push** | No device registration or push provider |
| **Marketing** | Newsletters, drips, promo blasts — transactional Notifications only |
| **Inbound webhooks** | Resend/Twilio reply handling |
| **`smsUsageBilling`** | Reserved; no metering |
| **Owner delivery log (N-U4)** | Optional V1.1 unless support requires |

---

## 6. Sequencing

### Sprint closeout A — “Launch truth”

**Goal:** Staging sends + docs match code; minimal portal must-haves.

1. Doc sweep — **N-D1–D6**, **R-D1–R-D2**
2. Staging ops — **N-O1–O4**, **N-O6**, **R-O1** (parallel ops)
3. Portal must-haves — **R-U2**, **R-U1**, **N-U1**
4. QA — **N-O5**, **N-T1**, **R-O2**

**Acceptance:** Real email + SMS on staging; receipt PDF in inbox; JWT PDF download; sales-summary csv from portal; admin audit rows present.

### Sprint closeout B — “Polish & edge cases” (optional pre-launch)

1. **R-U3** multi-location export UX (if multi-branch tenants on Starter/Pro)
2. **R-U4** compensation → export menu + xlsx
3. **R-U5** `advancedReports` hints
4. **N-33c2** subscription staging E2E (parallel Sprint 20)
5. **R-D3** PDF branding (only if sales PDF needs tenant logo at launch)

**Philosophy:** No new handlers, no Reporting↔Notifications coupling, no retention registry entries. Prefer **api-client + portal buttons** and **runbook fixes**.

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Unchanged — existing export and preference routes |
| **Auth / roles** | R-E1 permission matrix; admin reporting MFA + audit |
| **PII** | N-O6 logging spot-check |
| **Webhooks** | N-33c1 subscription HMAC on staging (Murphy) |

---

## 8. Tests

- [ ] **N-T1–T4** (Notifications)
- [ ] **R-O1–O2** staging smoke (Reporting)
- [ ] `dotnet test Adeni.slnx -c Release` before each closeout PR

---

## 9. Acceptance criteria (closeout complete)

- [ ] Runbooks and module specs reflect shipped behavior (N-D*, R-D*)
- [ ] Staging transactional email/SMS + reminders verified (N-O5)
- [ ] Portal: sales-summary export + authenticated finance PDF (R-U1, R-U2); honest push UX (N-U1)
- [ ] Admin reporting read paths audited on staging (R-O1)
- [ ] No retention, WhatsApp API, FCM, or marketing work in this track

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| Block launch on 33c SaaS E2E? | **N-33c3** — product call |
| Ship R-U3 before launch? | Only if multi-location tenants on constrained SKUs |
| Owner delivery log at launch? | Default **defer** (N-U4) |
