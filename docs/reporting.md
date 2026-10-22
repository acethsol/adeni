# Reporting — documents, analytics queries, exports

Read-only and render responsibilities: **PDF documents**, **tenant/admin analytics data**, and **multi-format exports**. Money and booking **state** stay in Finance and Booking; Reporting **queries and renders**.

**Boundaries:** [specs/reporting-module-boundaries.md](./specs/reporting-module-boundaries.md)  
**Shared list UI:** [specs/shared-data-grid.md](./specs/shared-data-grid.md)  
**Roadmap:** [roadmap-modules.md](./roadmap-modules.md) §3.5

---

## What Reporting owns

| Area | V1 status | Notes |
|------|-----------|--------|
| **Receipt / invoice PDF** | **39A** — `IReportRenderer` + QuestPDF in `Adeni.Infrastructure.Reporting` | Finance delegates `GetPdfAsync` |
| **Quote / statement PDF** | **39B** — QuestPDF + tenant logo / business name header | Footer: Option 3 wordmark (`Brand/adeni-wordmark.png`, mirrors `@adeni/brand`) |
| **Portal dashboard KPIs** | Shipped | `ITenantDashboardService` → `/api/v1/tenant/dashboard/*` |
| **CSV / Excel / PDF exports** | **39B** | `IReportExportService` + ClosedXML; `/api/v1/tenant/reporting/exports/{reportKey}` |
| **Admin GMV / MRR** | **39B** | `/api/v1/admin/reporting/platform-subscription-mrr`, `/gmv` (MFA + audit) |

---

## Architecture

```
Reporting (extractable)
├── Documents   — QuestPDF via IReportRenderer
├── Analytics   — ITenantDashboardService / ITenantReportingQueryService
└── Exports     — IReportExportService (csv | xlsx | pdf)

Peers
├── Finance     — BillingDocument persistence, calls Reporting for PDF bytes
├── Booking     — Quote workflow data for quote PDF
└── Notifications — Sends email; Reporting supplies attachment bytes
```

| Layer | Location (target) |
|-------|-------------------|
| Application | `IReportRenderer`, `IReportExportService`, `ITenantReportingQueryService` |
| Infrastructure | `AddReportingModule()` — QuestPDF templates, ClosedXML exports, dashboard queries |
| API | `/api/v1/tenant/dashboard/*`, `/api/v1/tenant/reporting/exports/{reportKey}`, quote/statement PDF routes; `/api/v1/admin/reporting/*` |

**Stack (canonical):** Angular + Carbon → .NET read APIs → SQL Server. **No DevExpress** in V1. **ClosedXML** when Excel ships ([tooling-policy.md](./tooling-policy.md)).

---

## Document types

| Kind | Data owner | Renderer |
|------|------------|----------|
| Receipt | Finance `BillingDocument` | QuestPDF |
| Invoice | Finance `BillingDocument` | QuestPDF |
| Quote | Booking quote records | QuestPDF (Phase 2) |
| Statement | Reporting + Finance read | QuestPDF (Phase 2) |

Issued documents use **snapshots** on the entity; PDF regeneration must not silently swap live profile data.

---

## HTTP surface

### Analytics (live)

| Method | Route | Auth |
|--------|-------|------|
| `GET` | `/api/v1/tenant/dashboard/summary` | Business + tenant |
| `GET` | `/api/v1/tenant/dashboard/series` | Business + tenant |
| `GET` | `/api/v1/tenant/dashboard/breakdown` | Business + tenant |
| `GET/PUT` | `/api/v1/tenant/dashboard/layout` | Business + tenant |

### Documents (Finance routes; PDF via Reporting over time)

| Method | Route |
|--------|-------|
| `GET` | `/api/v1/finance/documents/{id}/pdf` |
| `GET` | `/api/v1/public/documents/{token}/pdf` |

### Exports (39B — shipped)

| Method | Route | Query |
|--------|-------|-------|
| `GET` | `/api/v1/tenant/reporting/exports/{reportKey}` | `from`, `to`, `format=csv\|xlsx\|pdf` |

`reportKey` examples: `sales-summary`, `bookings-register`, `compensation`, `staff-roster`, `tenant-customers`, `tenant-locations`, `tenant-services`.

---

## Frontend

- **Dashboard:** portal widgets consume dashboard JSON; charts stay client-side (not PDF). No report downloads on dashboard widgets — exports live on list toolbars and Finance.
- **Finance (`/finance`, Staff export tab):** **Sales summary** export (`reportKey=sales-summary`, csv/pdf) via `adeni-export-menu`; uses the tab’s from/to date range; requires `portal.finance`, `basicExports` (csv), and `advancedReports` (pdf, Pro+).
- **List pages:** shared **`AdeniListToolbarComponent`** + **`AdeniDataTableComponent`** in `@adeni/ui` — filters, card/table switcher, export slot → Reporting API ([shared grid spec](./specs/shared-data-grid.md)).
- **Downloads:** portal/admin list toolbars call `@adeni/api-client` `downloadTenantReportExport` / `downloadAdminReportExport`; compensation uses the same orchestrator via `reportKey=compensation`.
- **Finance documents:** portal **Payments → Documents** uses **`downloadBillingDocumentPdf()`** (JWT + tenant) for staff download; **View** opens the public discover URL (`/api/v1/public/documents/{token}/pdf`) for customer sharing — do not rely on public token for authenticated staff workflows.

---

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **1** | Module boundaries; QuestPDF extracted from `BillingDocumentService`; `reporting.md` + tests |
| **2** | Quote + statement PDF; tenant logo from profile media + Powered by Adeni footer (`adeni-wordmark.png`) |
| **3** | **39B** — `IReportExportService` + ClosedXML; portal export on bookings + dashboard widget |
| **4** | **39B** — Admin `/admin/reporting/*` (platform subscription MRR + GMV) |

---

## Tests & ops

- Unit smoke on PDF renderer fixtures; integration tests for PDF content-type and tenant denial on exports.
- Log `reportKey` + duration — not row-level PII.
- Run: `dotnet test Adeni.slnx -c Release`

### Admin reporting audit (staging verification)

`GET /api/v1/admin/reporting/*` (platform subscription MRR, GMV) is **audit-logged** with action `admin.reporting.read` via `AdminAuditRules` + `AuditMiddleware` (same pipeline as admin mutations). Automated coverage: `AdminReportingAuditIntegrationTests.PlatformSubscriptionMrr_get_writes_admin_reporting_audit`.

**Staging check:** sign in as admin (MFA), call `GET /api/v1/admin/reporting/platform-subscription-mrr` and `GET /api/v1/admin/reporting/gmv?granularity=month`, then confirm `admin.audit_logs` rows with action `admin.reporting.read` and your admin `actor_id`.
