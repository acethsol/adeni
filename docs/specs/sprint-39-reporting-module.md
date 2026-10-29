# Spec: Sprint 39 — Reporting module (documents, exports, analytics)

> Implements [reporting-module-boundaries.md](./reporting-module-boundaries.md) and [reporting.md](../reporting.md) as a **sprint-gated delivery track**.  
> **Note:** Sprint 39 was also used for Finance module boundaries (Phase 1) in [sprints.md](../sprints.md) (**39-finance** row); this spec is **Reporting module implementation**.

## IMPLEMENTATION ORDER

1. **Entitlements closeout** ([sprint-33-entitlements-closeout.md](./sprint-33-entitlements-closeout.md))
2. **Notifications Foundation — 38A** ([sprint-38-notifications-module.md](./sprint-38-notifications-module.md) §3.1)
3. **Reporting Foundation — 39A** (this spec §3.1) — **`IReportRenderer`**; Finance delegates PDF; **no Notifications dependency**
4. **Notifications Completion — 38B** — receipt PDF email via Application **`IReportRenderer`**
5. **Reporting Completion — 39B** (this spec §3.2) — exports, admin metrics, Retention analytics presentation
6. **Retention staged delivery — 37.1–37.5** ([sprint-37-retention-module.md](./sprint-37-retention-module.md))

**Dependency rule:** Reporting **does not** depend on Notifications. Finance and Notifications **call into** Reporting via Application ports only. Reporting **must not** query Retention EF tables — use **`IRetentionAnalyticsService`** when Retention metrics appear in exports (39B / 37.5).

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 39 — Reporting module |
| **Author** | |
| **Status** | 39A ✅ / 39B ✅ (retention export deferred 37.5) |
| **Created** | 2026-10-21 |
| **Depends on** | [sprint-33-entitlements-closeout.md](./sprint-33-entitlements-closeout.md) (export/report entitlement keys), Finance boundaries (39-finance), Sprint 32 dashboard |

---

## 1. Goal (one sentence)

Extract **QuestPDF rendering and export orchestration** into a peel-off Reporting module, gate **basic/advanced/Excel/multi-location** outputs by entitlements and **per-report permissions**, and keep Finance/Booking as systems of record while portal and admin consume read-only reporting APIs under `/api/v1/`.

---

## 2. Context

- **Boundaries (approved):** [reporting-module-boundaries.md](./reporting-module-boundaries.md)
- **Runbook:** [reporting.md](../reporting.md)
- **Finance peer:** [finance-module-boundaries.md](./finance-module-boundaries.md) — billing document persistence, payment webhooks unchanged
- **Dashboard:** [sprint-32-portal-dashboard-revisit.md](./sprint-32-portal-dashboard-revisit.md) — `ITenantDashboardService` today
- **Shared UI:** [shared-data-grid.md](./shared-data-grid.md) — export slot on list toolbars
- **Entitlements (exports):** `basicExports`, `advancedReports`, `excelExport`, `multiLocationReporting` — §6
- **Entitlements (analytics widgets):** `advancedAnalytics`, `customerRetentionAnalytics`, `staffPerformanceAnalytics`; Retention module adds **`advancedRetentionAnalytics`** (see [sprint-37-retention-module.md](./sprint-37-retention-module.md) §6.4)
- **Tooling:** QuestPDF (documents); **ClosedXML** when Excel ships ([tooling-policy.md](../tooling-policy.md))

### MRR terminology (disambiguation)

| Concept | Name in APIs/domain | Owner |
|---------|---------------------|-------|
| Adeni SaaS subscription revenue | **`PlatformSubscriptionMrr`** or **`AdeniMrr`** | Subscriptions / Admin reporting |
| Tenant’s customer membership recurring revenue | **`TenantMembershipMrr`** or **`MembershipMrr`** | Retention calculations; Reporting presents/exports |

Do **not** use unqualified **`MRR`** in new routes, DTOs, or admin UI where both could apply.

---

## 3. In scope

### 3.1 Reporting Foundation — **39A** (unblocks Notifications 38B)

- [x] `AddReportingModule()` in composition root
- [x] Application port: **`IReportRenderer`** — `RenderDocumentAsync(kind, data)` → PDF bytes (QuestPDF in Reporting.Infrastructure only)
- [x] Application port: `ITenantReportingQueryService` (alias/wrap `ITenantDashboardService` initially)
- [x] Move QuestPDF layout from `BillingDocumentService` → `Reporting.Infrastructure` behind `IReportRenderer`
- [x] Finance calls **`IReportRenderer`** for `GetPdfAsync` / HTTP PDF routes — Finance keeps persistence, sequences, tokens
- [x] Tests: PDF smoke + tenant denial unchanged behavior for receipt/invoice
- [x] **No** export orchestrator, ClosedXML, or admin GMV in 39A

### 3.2 Reporting Completion — **39B**

#### Documents expansion

- [x] Quote PDF template (Booking/Quotes data via port)
- [x] Statement template + issue flow (tenant customer period summary — not wallet/custody)
- [x] Branding paths from `@adeni/brand` where applicable (QuestPDF footer wordmark + tenant header; sales-summary PDF)

#### Export orchestrator

- [x] `IReportExportService` — `reportKey`, `from`, `to`, `format=csv|xlsx|pdf`
- [x] **`ReportDefinition`** registry (code/config — not necessarily DB) per reportKey:
  - `ReportKey`
  - `RequiredPermission` (portal RBAC)
  - `RequiredEntitlement` (SKU)
  - `SupportedFormats`
- [x] **Entitlement gates:**
  - `basicExports` — CSV operational exports (Starter+)
  - `advancedReports` — richer report definitions / breakdown exports (Pro+)
  - `excelExport` — `xlsx` branch via ClosedXML (Pro+)
  - `multiLocationReporting` — queries spanning locations when tenant has multiple (Business)
- [x] Portal: download on dashboard widgets + `@adeni/ui` list toolbar export slot (bookings + dashboard appointments widget)
- [x] Optional: delegate existing Finance compensation CSV through orchestrator (single entry point)

#### Admin reporting

- [x] `IAdminReportingQueryService` — **`AdeniMrr`** / **`PlatformSubscriptionMrr`**, GMV, active tenants under `/admin/reporting/*` (MFA + audit)
- [ ] Cross-tenant reads audited; no tenant PII bulk export without existing SOC2 patterns

#### Retention analytics presentation (after Retention **37.5** or parallel late 39B)

- [ ] Export/widget paths consume **`IRetentionAnalyticsService`** (Application) for **`TenantMembershipMrr`**, churn, utilization — **deferred to Retention 37.5** (`retention-membership-mrr` not in registry)

### Portal vs admin surfaces

| Surface | Phase | Routes / UI |
|---------|-------|-------------|
| **Portal** | 39A–39B | Existing `/api/v1/tenant/dashboard/*`; new `/api/v1/tenant/reporting/exports/{reportKey}`; Finance document PDF via Finance routes |
| **Admin** | 39B | `/admin/reporting/*` read models; no tenant mutation |

---

## 4. Out of scope

- DevExpress / Power BI / embedded BI designers
- Full accounting, tax e-file, QuickBooks/Xero sync
- Replacing Finance payment webhooks or ledger writes
- Customer-editable report builder
- PDF engine for live dashboard charts (JSON + Carbon only)
- **Retention business calculations** — owned by Retention; Reporting orchestrates format/download only

---

## 5. Architecture

```
DOCUMENTS
  Invoice | Receipt | Quote | Statement
              ↓
         IReportRenderer (Application port → QuestPDF in Infrastructure)
              ↑
    Finance / Booking ports (data only)

ANALYTICS (live aggregates)
  Portal/Admin Angular
              ↓
  GET /api/v1/tenant/dashboard/*  (39A stable)
              ↓
  ITenantReportingQueryService
              ↓
  SQL Server read-only, tenant-filtered

EXPORTS (39B)
  IReportExportService → ReportDefinition registry
              ↓
  csv | ClosedXML xlsx | QuestPDF pdf
              ↑
  EntitlementsService + portal permission per reportKey

RETENTION METRICS (39B / 37.5)
  IRetentionAnalyticsService (Retention.Application)
              ↑
  Reporting export handlers — no Retention DbSets
```

**Finance integration:** `PaymentCompleted` may trigger receipt PDF render (Finance/`IReportRenderer`) → Notifications email (38B); Reporting does not post payments.

---

## 6. Export entitlement plan behavior

| SKU | Keys | Typical access |
|-----|------|----------------|
| **Starter** | `basicExports` | CSV operational exports |
| **Professional** | + `advancedReports`, `excelExport` | Advanced reports + XLSX |
| **Business** | + `multiLocationReporting` | Cross-location report queries |

Authorization is **`RequiredPermission` AND `RequiredEntitlement`** — not broad `portal.overview` for all exports.

### `ReportDefinition` examples

| `reportKey` | `RequiredPermission` | `RequiredEntitlement` | `SupportedFormats` |
|-------------|----------------------|------------------------|--------------------|
| `sales-summary` | `portal.finance` | `basicExports` (csv); `advancedReports` (breakdown pdf) | csv, pdf |
| `bookings-register` | `portal.bookings` | `basicExports` | csv, xlsx |
| `compensation` | `portal.finance` | `advancedReports` | csv, xlsx |
| `staff-roster` | `portal.staff` | `basicExports` | csv |
| `retention-membership-mrr` | `portal.finance` or `portal.retention` | `advancedRetentionAnalytics` | csv, xlsx, pdf |

Registry lives in Reporting Application/Infrastructure as code or config per existing conventions.

---

## 7. API contract

### Analytics (keep stable 39A)

| Method | Route | Auth | Entitlement notes |
|--------|-------|------|-------------------|
| `GET` | `/api/v1/tenant/dashboard/summary` | Business + tenant | Widget-level `advancedAnalytics`, `customerRetentionAnalytics`, etc. |
| `GET` | `/api/v1/tenant/dashboard/series` | Business + tenant | Same |
| `GET` | `/api/v1/tenant/dashboard/breakdown` | Business + tenant | Same |
| `GET/PUT` | `/api/v1/tenant/dashboard/layout` | Business + tenant | — |

### Exports (39B)

| Method | Route | Auth | Gates |
|--------|-------|------|-------|
| `GET` | `/api/v1/tenant/reporting/exports/{reportKey}` | Business + **report-specific permission** | Query: `from`, `to`, `format`; 403 `entitlements.exports` / `entitlements.excel_export` / `entitlements.multi_location_reporting` |

### Documents

| Method | Route | Notes |
|--------|-------|-------|
| Existing | `/api/v1/finance/documents/*` | Persistence in Finance |
| `GET` | `/api/v1/finance/documents/{id}/pdf` | PDF via **`IReportRenderer`** (39A) |
| `GET` | `/api/v1/public/documents/{token}/pdf` | Public token unchanged |
| New 39B | `/api/v1/tenant/quotes/{id}/pdf` | Business |
| New 39B | `/api/v1/tenant/customers/{id}/statement` | Business |

All routes under `/api/v1/`; mutating document issue flows authenticated.

### Admin (39B)

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/admin/reporting/platform-subscription-mrr` | **`PlatformSubscriptionMrr`** — not tenant membership MRR |
| `GET` | `/admin/reporting/gmv` | Audited, MFA |

---

## 8. Files to touch

| Layer | Files |
|-------|-------|
| **Domain** | Report kind enums if needed |
| **Application** | **`IReportRenderer`**, `IReportExportService`, `ReportDefinition` registry, `ITenantReportingQueryService`, `IAdminReportingQueryService`; optional consume **`IRetentionAnalyticsService`** in 39B |
| **Infrastructure** | `Reporting.Infrastructure/` QuestPDF templates, ClosedXML exporters, dashboard query move (gradual) |
| **Finance.Infrastructure** | `BillingDocumentService` — delegate PDF; remove direct QuestPDF reference (39A) |
| **Api** | `TenantReportingController`, export endpoint; admin reporting controller (39B) |
| **Tests** | Renderer smoke; export 403 when permission/entitlement missing; architecture test — Reporting does not reference Finance/Retention EF |
| **packages/shared** | Export query schemas, reportKey enum, entitlement error keys |
| **packages/api-client** | Export download methods |
| **apps/business** | Dashboard export buttons; grid toolbar wiring |
| **apps/admin** | Reporting metrics pages (39B) |

---

## 9. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All queries `tenant_id` filtered; admin cross-tenant audited |
| **Auth** | **Per-report** portal permissions; admin MFA policy |
| **PII** | Statements/quotes mask phone on public paths; exports tenant-scoped |
| **Cache** | Dashboard TTL per [caching-setup.md](../caching-setup.md) |
| **Observability** | Log `reportKey` + duration — not row-level PII |

---

## 10. Tests

- [x] Unit: QuestPDF receipt fixture (39A)
- [x] Integration: export 403 when `excelExport` false and format=xlsx
- [x] Export 403 when user lacks `RequiredPermission` for `reportKey`
- [x] Multi-location report denied when `multiLocationReporting` false and `locationId` omitted
- [x] Architecture: Reporting does not reference Finance or Retention Infrastructure EF entities

Run: `dotnet test Adeni.slnx -c Release`

---

## 11. Acceptance criteria

### 39A

- [x] QuestPDF under Reporting Infrastructure; Finance has no direct QuestPDF reference
- [x] Receipt/invoice PDF behavior unchanged for tenants
- [x] `IReportRenderer` callable from Notifications integration test (mock)

### 39B

- [x] Portal can download CSV with `basicExports`; xlsx requires `excelExport`
- [x] `ReportDefinition` enforces permission + entitlement per `reportKey`
- [x] Admin API exposes **`PlatformSubscriptionMrr`** (labeled, not ambiguous “MRR”)
- [x] Compensation export reachable via orchestrator or documented Finance delegate

---

## 12. Open questions

| Question | Decision |
|----------|----------|
| Rename `ITenantDashboardService` now vs alias | Alias in 39A OK |
| Move compensation CSV fully under Reporting | Optional delegate 39B |
| `/tenant/reporting/*` vs keep dashboard prefix only | Exports under `/tenant/reporting/exports`; analytics keep dashboard paths 39A |
| Consolidate `customerRetentionAnalytics` + `advancedRetentionAnalytics` | **Do not merge in spec** — see Retention §6.4; product decision if consolidation desired |
