# Spec: SOC 2 audit hardening

| Field | Value |
|-------|-------|
| **Sprint** | SOC 2 readiness — audit trail hardening |
| **Author** | Agent |
| **Status** | Implemented (pre-staging closeout) |
| **Created** | 2026-10-23 |

---

## 1. Goal (one sentence)

Make `admin.audit_logs` a trustworthy SOC 2 trail: no false positives on failed admin calls, one row per meaningful action, and coverage for high-impact portal and webhook events.

---

## 2. Context

- Gap analysis: audit coverage review `3e5effb3`
- PRD **SOC2-01** — admin actions audit-logged with actor, timestamp, entity
- Staging launch closeout ops (item 5) **deferred** until staging push

---

## 3. In scope

- [x] `AuditMiddleware` writes only on **2xx** responses
- [x] **Dedupe:** middleware skips routes where services write rich audit rows
- [x] `ReviewService.HideAsync`, `VerificationBadgeService` grant/revoke — service audits
- [x] `subscription.webhook_received` before signature parse (no body)
- [x] Admin **GET** `/metrics/*` and `/transactions` — same pattern as reporting reads
- [x] Portal high-impact mutations: verification submit, booking status transitions, notification preferences, **tenant settings** (deposit/auto-confirm), **tenant report exports**
- [x] Admin audit log query API + demo UI (`admin.audit_logs.read`)
- [x] Runbook: retention, access, named owners, auditor export process

## 4. Out of scope

- Separate tenant-scoped audit table (backlog)
- Staging E2E export audit checklist (revisit at staging)
- Universal portal CRUD audit (menu, media, messages, etc.)

---

## 5. Audit policy

| Layer | Rule |
|-------|------|
| **Middleware** | `/api/v1/admin` mutating verbs + sensitive admin GETs (reporting, metrics, transactions); **2xx only**; **suppressed** when service owns the action |
| **Service** | Authoritative row with correct `entityType`, metadata, actor (`sub` or `system:*`) |
| **Portal** | High-impact tenant mutations listed in §3 → same `admin.audit_logs` store (v1); staff.* via `StaffAccessService` |

**Dedupe list (middleware suppressed):** business approve/reject, commercial terms PATCH, markets mutations, pricing catalog mutations, customer erasure POST, review hide DELETE, verification badge grant/revoke POST.

---

## 6. Files touched

| Layer | Files |
|-------|-------|
| Domain | `AuditActions.cs` |
| Api | `AuditMiddleware.cs`, `AdminAuditRules.cs`, `SubscriptionController.cs` |
| Infrastructure | `ReviewService`, `VerificationBadgeService`, `SubscriptionBillingOrchestrator`, `BusinessOnboardingService`, `NotificationPreferencesService`, `BookingService` |
| Tests | `MiddlewareTests`, `PortalSoc2AuditIntegrationTests`, `TenantReportingExportIntegrationTests`, `AdminAuditLogsIntegrationTests`, `AuditTestDoubles` |
| Api | `AdminAuditLogsController`, `TenantReportingController` (actor on export) |
| Admin UI | `apps/admin/.../audit-logs/*` |
| Docs | This spec, [audit-runbook.md](../audit-runbook.md), [soc2-readiness-checklist.md](../soc2-readiness-checklist.md) |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **PII** | Audit metadata: counts/flags only — no message bodies, emails, or document refs |
| **Webhooks** | Subscription receive row without payload (Murphy minimal logging) |
| **Compliance** | Fewer duplicate rows; failed admin attempts no longer logged as successes |

---

## 8. Acceptance

- `dotnet test Adeni.slnx -c Release` green
- Admin approve → **one** `business.approved` row (service, `entityType=tenant`)
- Admin PATCH commercial terms failure (4xx) → **no** middleware audit row
- Subscription webhook POST → `subscription.webhook_received` even when parse fails
- Portal booking accept → `booking.accepted` with actor JWT sub when present
