# Reporting — documents, analytics queries, exports

Read-only and render responsibilities: **PDF documents**, **tenant/admin analytics data**, and **multi-format exports**. Money and booking **state** stay in Finance and Booking; Reporting **queries and renders**.

**Boundaries:** [specs/reporting-module-boundaries.md](./specs/reporting-module-boundaries.md)  
**Shared list UI:** [specs/shared-staff-data-grid.md](./specs/shared-staff-data-grid.md)  
**Roadmap:** [roadmap-modules.md](./roadmap-modules.md) §3.5

---

## What Reporting owns

| Area | V1 status | Notes |
|------|-----------|--------|
| **Receipt / invoice PDF** | Implemented in Finance infra (QuestPDF inline) | Phase 1: extract to `IReportRenderer` |
| **Quote / statement PDF** | Planned | Phase 2 |
| **Portal dashboard KPIs** | Shipped | `ITenantDashboardService` → `/api/v1/tenant/dashboard/*` |
| **CSV / Excel / PDF exports** | Partial | Compensation CSV on Finance; unified export orchestrator Phase 3 |
| **Admin GMV / MRR** | Backlog | Phase 4, Sprint 20e alignment |

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
| API | Dashboard routes today; `/api/v1/tenant/reporting/exports/*` Phase 3 |

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

### Exports (planned)

| Method | Route | Query |
|--------|-------|-------|
| `GET` | `/api/v1/tenant/reporting/exports/{reportKey}` | `from`, `to`, `format=csv\|xlsx\|pdf` |

`reportKey` examples: `sales-summary`, `bookings-register`, `compensation`, `staff-roster`.

---

## Frontend

- **Dashboard:** portal widgets consume dashboard JSON; charts stay client-side (not PDF).
- **List pages:** shared **`AdeniListToolbarComponent`** + **`AdeniDataTableComponent`** in `@adeni/ui` — filters, card/table switcher, export slot → Reporting API ([shared grid spec](./specs/shared-staff-data-grid.md)).
- **Downloads:** use `@adeni/api-client` when export endpoints land; until then Finance compensation export remains on `/api/v1/finance/compensation-export`.

---

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **1** | Module boundaries; QuestPDF extracted from `BillingDocumentService`; `reporting.md` + tests |
| **2** | Quote + statement PDF; tenant branding from `@adeni/brand` paths |
| **3** | `IReportExportService` + ClosedXML; portal export buttons on grid toolbars |
| **4** | Admin cross-tenant reporting under `/admin/reporting/*` |

---

## Tests & ops

- Unit smoke on PDF renderer fixtures; integration tests for PDF content-type and tenant denial on exports.
- Log `reportKey` + duration — not row-level PII.
- Run: `dotnet test Adeni.slnx -c Release`
