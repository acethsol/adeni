# Spec: Reporting module boundaries

| Field | Value |
|-------|-------|
| **Sprint** | Cross-cutting — Reporting (documents render + analytics + exports) |
| **Author** | Team |
| **Status** | Approved — implementation phased (see §4) |
| **Created** | 2026-10-19 |

---

## 1. Goal (one sentence)

Establish **Reporting** as the extractable bounded context for **print-quality documents**, **owner/admin analytics UI data**, and **multi-format exports** — while **Finance** and **Booking** remain systems of record for money and quotes.

---

## 2. Context

- [roadmap-modules.md](../roadmap-modules.md) §3.5 Dashboards & reporting
- [finance-module-boundaries.md](./finance-module-boundaries.md) — ledger, billing document **persistence**, compensation CSV
- [sprint-28-billing-documents.md](./sprint-28-billing-documents.md) — receipts/invoices (implemented in Finance + QuestPDF today)
- [sprint-32-portal-dashboard-revisit.md](./sprint-32-portal-dashboard-revisit.md) — portal widgets (`ITenantDashboardService`)
- [tooling-policy.md](../tooling-policy.md) — no DevExpress/BI suite in V1; add **ClosedXML** when Excel export ships

---

## 3. Decision record

| # | Decision |
|---|----------|
| 1 | **Reporting** is the peel-off module for **render + query + export**; not for posting payments or mutating bookings. |
| 2 | **Documents** (invoice, receipt, quote PDF, statement) use **QuestPDF** only — one renderer port, multiple templates. |
| 3 | **Interactive reporting UI** = **Angular + Carbon** → read-only **.NET query endpoints** → **SQL Server** (tenant-scoped). Shared list/grid shell: [shared-staff-data-grid.md](./shared-staff-data-grid.md). |
| 4 | **Exports:** **CSV** (native .NET), **Excel** (ClosedXML), **PDF** (QuestPDF) from the **same report definition** where possible. |
| 5 | **Finance** keeps **BillingDocument** entities, sequences, public tokens, void/audit; Reporting **renders** PDFs (migrate from inline `BillingDocumentService.BuildPdf` over time). |
| 6 | **Quote PDF** renders from **Booking/Quotes** data via Application port — Reporting does not own quote workflow state. |
| 7 | **Statement** = period summary for a **tenant customer** (activity + balances narrative) — **not** a wallet/custody balance; handoff to external accounting only. |
| 8 | **No DevExpress** in V1; revisit only if ops needs designer-owned `.repx` without deploys. |
| 9 | Dashboard HTTP prefix may stay **`/api/v1/tenant/dashboard/*`** in Phase 1; optional alias **`/api/v1/tenant/reporting/*`** in Phase 2 (dev clean cut OK). |
| 10 | Document **snapshots** at issue time; analytics endpoints return **live aggregates** (clearly labeled in UI). |

---

## 4. In scope (by phase)

### Phase 1 — Boundaries + extract PDF from Finance

- [x] This spec approved
- [ ] `AddReportingModule()` in composition root (may wrap existing `Dashboard` + QuestPDF helpers initially)
- [ ] Application ports: `IReportRenderer`, `ITenantReportingQueryService` (or retain `ITenantDashboardService` as alias)
- [ ] Move QuestPDF layout code from `BillingDocumentService` → `Reporting.Infrastructure` behind `IReportRenderer`
- [ ] Finance calls Reporting for `GetPdfAsync` / email attachment bytes
- [ ] Docs: [reporting.md](../reporting.md) runbook (create when Phase 1 starts)

### Phase 2 — Documents expansion

- [ ] **Quote** PDF template (from accepted/sent quote + tenant branding via `@adeni/brand` paths)
- [ ] **Statement** template + `POST/GET` issue flow (tenant-scoped; customer on statement)
- [ ] Unify **compensation export** PDF option (optional) with same export orchestrator as CSV

### Phase 3 — Export orchestrator

- [ ] `IReportExportService` — `reportKey`, date range, `csv` | `xlsx` | `pdf`
- [ ] ClosedXML for Excel; add package + [tooling-policy.md](../tooling-policy.md) row
- [ ] Portal: download buttons on dashboard widgets where applicable

### Phase 4 — Admin reporting

- [ ] Admin read models: GMV, MRR, active tenants (Sprint 20e alignment) under `/admin/reporting/*` or existing admin metrics services
- [ ] Extract module to separate deployable when scale justifies

---

## 5. Out of scope

- DevExpress / Stimulsoft / embedded Power BI / Looker
- Full accounting, tax e-file, QuickBooks/Xero sync
- Replacing **Finance** payment webhooks or ledger writes
- Customer-editable report builder (drag-drop designer for tenants)
- Using PDF engine for **live dashboard charts** (charts stay JSON + Carbon)

---

## 6. Architecture stack (canonical)

```
DOCUMENTS (issued artifacts)
  Invoice | Receipt | Quote | Statement
              ↓
         QuestPDF (IReportRenderer)
              ↑
    Finance / Booking / Quotes (data ports)


REPORTING UI (interactive)
  Angular portal/admin + Carbon
              ↓
  GET /api/v1/tenant/dashboard/*  (Phase 1)
  GET /api/v1/tenant/reporting/*  (Phase 2 optional)
              ↓
  ITenantReportingQueryService / ITenantDashboardService
              ↓
  SQL Server (read-only, tenant-filtered)


EXPORTS (download)
  CSV     → StreamWriter / CsvHelper (or manual)
  Excel   → ClosedXML
  PDF     → QuestPDF (same templates as documents where shared)
              ↑
  IReportExportService
```

---

## 7. Module map

```
Reporting (extractable)
├── Documents      — QuestPDF templates: receipt, invoice, quote, statement
├── Analytics      — dashboard summary, series, breakdown (existing Dashboard infra)
└── Exports        — CSV / Xlsx / Pdf for operational reports + Finance handoffs

Finance (peer)
├── BillingDocument persistence, sequences, public tokens
└── Calls Reporting for PDF bytes

Booking / Quotes (peer)
└── Quote lifecycle; Reporting renders quote PDF on demand

Notifications (peer)
└── Sends email with PDF attachment from Reporting/Finance (no inline PDF in dispatcher)
```

**Application ports (target):**

| Port | Responsibility |
|------|----------------|
| `IReportRenderer` | `RenderDocumentAsync(kind, data)` → PDF bytes |
| `ITenantReportingQueryService` | Summary, series, breakdown (may equal `ITenantDashboardService`) |
| `IReportExportService` | Named report + range + format |
| `IAdminReportingQueryService` | Cross-tenant metrics (admin MFA + audit) |

---

## 8. Document types

| Kind | Owner (data) | Renderer | Notes |
|------|--------------|----------|-------|
| **Receipt** | Finance `BillingDocument` | QuestPDF | Shipped Sprint 28 |
| **Invoice** | Finance `BillingDocument` | QuestPDF | Shipped Sprint 28 |
| **Quote** | Booking `QuoteRequestRecord` (+ lines) | QuestPDF | PDF on send/accept; optional public token later |
| **Statement** | Reporting + Finance read | QuestPDF | Period activity; not tax authority e-invoice |

**Snapshot rule:** Issued documents store business/customer/line snapshots on the entity (existing pattern). PDF regeneration uses stored snapshot, not live profile.

---

## 9. API contract (target)

### Analytics (existing — keep stable)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/dashboard/summary` | Business + tenant | KPI cards |
| `GET` | `/api/v1/tenant/dashboard/series` | Business + tenant | Time series |
| `GET` | `/api/v1/tenant/dashboard/breakdown` | Business + tenant | Dimension breakdown |
| `GET/PUT` | `/api/v1/tenant/dashboard/layout` | Business + tenant | Widget layout |

### Exports (Phase 3)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/reporting/exports/{reportKey}` | Business + `portal.finance` or `portal.overview` | Query: `from`, `to`, `format=csv\|xlsx\|pdf` |

`reportKey` examples: `sales-summary`, `bookings-register`, `compensation` (may redirect to Finance export initially).

### Documents

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| Existing | `/api/v1/finance/documents/*` | As Sprint 28 | Persistence + list |
| `GET` | `/api/v1/finance/documents/{id}/pdf` | Scoped | PDF via Reporting renderer |
| New | `/api/v1/tenant/quotes/{id}/pdf` | Business | Quote PDF (Phase 2) |
| New | `/api/v1/tenant/customers/{id}/statement` | Business | Issue/list statement (Phase 2) |

Public document tokens: **`/api/v1/public/documents/{token}`** unchanged (Finance).

---

## 10. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All queries filter `tenant_id`; admin routes audited |
| **Auth** | Reuse portal permissions (`portal.overview`, `portal.finance`, `portal.bookings`) per report |
| **PII** | Statements/quotes mask phone on public paths; exports tenant-scoped only |
| **Cache** | Dashboard TTL per [caching-setup.md](../caching-setup.md); invalidate on booking/payment events where needed |
| **Observability** | Log reportKey + duration — not row-level PII |

---

## 11. Tests

- [ ] Unit: QuestPDF renderer smoke (receipt fixture)
- [ ] Integration: PDF endpoint returns `application/pdf` + tenant denial
- [ ] Export: CSV/Xlsx row counts match query fixture
- [ ] Architecture: Reporting does not reference EF entities from Finance Infrastructure directly (ports only)

Run: `dotnet test Adeni.slnx -c Release`

---

## 12. Acceptance criteria (Phase 1)

- [ ] QuestPDF code lives under Reporting Infrastructure; Finance has no direct QuestPDF reference
- [ ] Receipt/invoice PDF behavior unchanged for tenants
- [ ] Spec + [roadmap-modules.md](../roadmap-modules.md) row #5 references **Reporting** module name

---

## 13. Open questions

| Question | Decision |
|----------|----------|
| Rename `ITenantDashboardService` → `ITenantReportingQueryService`? | Phase 1 alias OK; rename when Reporting namespace lands |
| Move compensation CSV under Reporting export? | Optional unify in Phase 3; Finance port can delegate |
| ClosedXML license | Apache 2.0 — document in tooling policy when added |
