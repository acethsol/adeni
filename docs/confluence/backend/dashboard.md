# Backend module — Tenant Dashboard

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Business portal dashboard** aggregates: KPI summary, time series, breakdowns, and **persisted widget layout** per business user (Sprint 32).

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Identity/BusinessUserDashboardLayout` |
| Application | `Adeni.Application/Dashboard/ITenantDashboardService`, layout normalizers |
| Infrastructure | `Adeni.Infrastructure/Dashboard/TenantDashboardService` |
| API | `TenantDashboardController` — `/api/v1/tenant/dashboard` |

Registration: `AddDashboardModule()`.

---

## Endpoints (conceptual)

| Endpoint | Data |
|----------|------|
| Summary | Bookings, revenue signals, queue depth (honest/gated KPIs) |
| Series | Time-range charts |
| Breakdown | By service/staff/status |
| Layout GET/PUT | User-specific widget arrangement |

---

## Clients

Portal **Dashboard** — Carbon charts, layout store/sync (`dashboard-layout-store.ts`).

---

## Related

- [Booking](./booking.md), [Payments](./payments.md), [Subscriptions](./subscriptions.md) for gating
