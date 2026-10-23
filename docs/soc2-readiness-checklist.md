# SOC 2 readiness checklist (V1)

> **Authority:** [prd-v1.1-body.md §10.2](./prd-v1.1-body.md) (SOC2-01–SOC2-10)  
> **Audit trail detail:** [specs/sprint-soc2-audit-hardening.md](./specs/sprint-soc2-audit-hardening.md), [audit-runbook.md](./audit-runbook.md)  
> **Legal (separate track):** [legal-launch-checklist.md](./legal-launch-checklist.md)

This checklist splits **work that ships in repo / CI** from **deploy and ops** that require staging or production access. The dev branch closeout targets **code + docs + automated tests** only — not live Key Vault, Resend/Twilio, deploy pipelines, or remote staging E2E.

---

## A. Code, tests, and runbooks (in scope for dev branch)

| ID | Requirement | Status | Evidence / notes |
|----|-------------|--------|------------------|
| **SOC2-01** | Admin and high-impact portal actions → `admin.audit_logs` | ✅ Code | `AuditMiddleware` (2xx only, dedupe), service audits — [audit-runbook.md](./audit-runbook.md) |
| **SOC2-02** | Cross-tenant attempts → 403 + audit | ✅ Code + tests | `TenantAccessMiddleware`, `security.cross_tenant_denied` |
| **SOC2-03** | PII masked in logs | ✅ Code | `PiiMasker`, Serilog policy — [AGENTS.md](../AGENTS.md) |
| **SOC2-07** | Dependency scan on PR | ✅ CI | NuGet + npm gates in pipeline |
| **SOC2-09** | Admin customer export / delete | ✅ Code + UI | `/admin/customers/{id}/export`, `/delete`; audited |
| **Murphy webhooks** | Payment + subscription HMAC + idempotency | ✅ Code | [AGENTS.md](../AGENTS.md) webhook table; staging secret E2E **deferred** (§B) |
| **API contract** | `/api/v1/`, auth on mutations, stable `code` | ✅ Baseline | [api-errors.md](./api-errors.md) |
| **Reporting entitlements** | Export permissions match `ReportDefinitionRegistry` | ✅ Tests | R-E1 integration tests on dev |
| **PDF branding (R-D3)** | Option 3 wordmark on tenant PDFs incl. sales-summary | ✅ Code + tests | `Brand/adeni-wordmark.png`, QuestPDF footer |
| **Portal branch switch UX** | Artificial delay only in Angular dev mode | ✅ Code | `isDevMode()` gate in `portal-active-location.service.ts` |
| **Automated test gate** | `dotnet test Adeni.slnx -c Release` | ✅ Required before push | Local + CI |
| **i18n parity** | en/fr for API errors + major UI | 🟡 Tail documented | [specs/sprint-ui-i18n-full-parity.md](./specs/sprint-ui-i18n-full-parity.md) §9 residual |

---

## B. Deploy, staging, and production ops (explicitly out of scope for dev branch closeout)

Do **not** block merging dev on these; track for Sprint 20 / staging push.

| ID / item | Requirement | Owner | Notes |
|-----------|-------------|-------|-------|
| **SOC2-04** | TLS 1.2+, HSTS, HTTPS-only | Infra / hosting | App Service / Front Door config |
| **SOC2-05** | Secrets in Key Vault (staging/prod) | Infra | `Resend:*`, `Twilio:*`, Paystack webhook secrets, SQL, Redis |
| **SOC2-06** | MFA enforced for admin Auth0 + infra accounts | Auth0 + IT | [auth0-setup.md](./auth0-setup.md), [mfa-enforcement.md](./mfa-enforcement.md) |
| **SOC2-08** | DB backups + quarterly restore drill | Infra | Azure SQL PITR; runbook evidence |
| **SOC2-10** | Incident response tabletop (annual) | Security / ops | Confluence IR plan |
| **N-O1–O6** | Notifications staging sends + SQL templates + Hangfire | Ops | [sprint-38-39-launch-closeout.md](./specs/sprint-38-39-launch-closeout.md) §3.2 |
| **N-33c1–2** | Subscription webhook secret + live checkout E2E | Ops | Paystack staging |
| **R-O1–O2** | Admin reporting + tenant export smoke on staging | Ops | MFA + manual audit row verification |
| **N-T2–T3** | French/staging DB integration + manual send matrix | QA | Requires N-O2 staging DB |
| **Sprint 20** | CI/CD deploy, App Insights, Sentry, rate limits at edge | Eng + infra | [observability.md](./observability.md) |
| **Legal gate** | Lawyer-approved Privacy / Terms (not placeholders) | Legal | [legal-launch-checklist.md](./legal-launch-checklist.md) §6 |

---

## C. Staging verification script (when §B is ready)

Use [audit-runbook.md](./audit-runbook.md) **Staging spot-check** after deploy:

1. Admin MFA login → `GET /api/v1/admin/reporting/platform-subscription-mrr` → one `admin.reporting.read` row.
2. Portal booking accept → `booking.accepted` with actor `sub`.
3. `PATCH /api/v1/tenant/notification-preferences` → `tenant.notification_preferences_updated`.
4. Failed admin mutation (4xx) → **no** new middleware audit row.
5. `POST /api/v1/subscriptions/webhook` (unsigned when secret set) → reject; with valid HMAC → `subscription.webhook_received` / applied.

---

## D. Related docs

| Doc | Purpose |
|-----|---------|
| [audit-runbook.md](./audit-runbook.md) | Action catalog, dedupe rules, SQL samples |
| [tenant-isolation.md](./tenant-isolation.md) | Cross-tenant middleware |
| [legal-launch-checklist.md](./legal-launch-checklist.md) | NDPR / Terms launch gate |
| [specs/sprint-38-39-launch-closeout.md](./specs/sprint-38-39-launch-closeout.md) | Notifications + Reporting launch gaps |
