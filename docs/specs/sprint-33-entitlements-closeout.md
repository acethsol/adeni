# Spec: Sprint 33 — Entitlements v2 closeout

> Copy lineage: [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md) (MVP done). **This spec closes the entitlements loop before Notifications (38), Reporting (39), and Retention (37).**

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 33 — Entitlements v2 closeout (`33-closeout`) |
| **Author** | |
| **Status** | Done |
| **Created** | 2026-10-21 |
| **Depends on** | [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md), [sprint-33-saas-subscription-billing-mrr.md](./sprint-33-saas-subscription-billing-mrr.md) |

---

## 1. Goal (one sentence)

Finish entitlements v2 **parity, portal UX, and matrix hygiene** so tier gates are trustworthy and marketing comparison reflects **enforced wedges only**—before building Notifications, Reporting, and Retention on top of the same keys.

---

## 2. Context

- **Sprint tracker:** [docs/sprints.md](../sprints.md) — **33-closeout** done; implementation order: **33 closeout → 38 notifications → 39 reporting → 37 retention**.
- **MVP spec (done):** [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md) — seed-driven matrix, location/practitioner caps, waitlist, messaging, analytics gates.
- **Machine-readable seed:** [plan-entitlements.seed.json](./adeni-subscription-pricing-v1/plan-entitlements.seed.json) ↔ `packages/shared/src/data/plan-entitlements.json` ↔ `JsonPlanEntitlementsMatrix` (runtime load; no duplicated C# seed).
- **Marketing matrix:** `PLAN_COMPARISON` / `PLAN_COMPARISON_ROW_DEFS` in [plan-entitlements.ts](../../packages/shared/src/plan-entitlements.ts) — **enforced + honest informational rows only**.
- **Admin full matrix:** `PLAN_ADMIN_MATRIX_ROW_DEFS` (includes deferred Sprint 38/39 keys with admin notes).

---

## 3. In scope (shipped)

- [x] **Portal parity** — waitlist entry points gated on profile `entitlements.waitlist`; SMS toggle respects `smsEnabled`; staff/location cap UX on `/plan` and list headers.
- [x] **JSON ↔ C# parity** — `PlanEntitlementsParityTests` + shared vitest; C# reads shared JSON via `JsonPlanEntitlementsMatrix` at startup.
- [x] **Sprint status reconciliation** — [sprints.md](../sprints.md) 33-closeout done; module order 38 → 39 → 37 in Next up.
- [x] **Label matrix rows** — enforced vs deferred vs informational documented; deferred reminder/export keys **removed from `PLAN_COMPARISON`** (remain in seed + admin matrix).
- [x] **Matrix cleanup** — §5 decision table applied (deposits, online payments, invoices, basic analytics, SMS billing, additional location/practitioner sales flags removed from comparison).
- [x] **Portal audit** — authenticated gating uses `profile.entitlements` / `hasTenantEntitlement` (portal-nav, guards, dashboard, settings, staff, locations, messages). **`PLAN_COMPARISON` / `PLAN_ENTITLEMENTS` used only on `/plan` (marketing) and admin pricing matrix (seed display)** — not for ops gates.

---

## 4. Out of scope

- Notifications (38), Reporting (39), Retention (37) product behavior.
- DB-persisted entitlement catalog or admin hot-edit of flags.
- Per-tenant entitlement overrides beyond commercial price/trial (33b).

---

## 5. Matrix cleanup (summary)

| Key group | `PLAN_COMPARISON` | Seed / API | Notes |
|-----------|-------------------|------------|-------|
| Reminders (`basicReminders`, `configurableReminders`, `followUpAutomation`) | **Removed** | Keep | Enforce in Sprint 38 |
| Exports (`basicExports`, `advancedReports`, `excelExport`, `multiLocationReporting`) | **Removed** | Keep | Enforce in Sprint 39 |
| Caps, messaging, waitlist, SMS, analytics widgets | **Keep** | Keep | Enforced today |
| Deposits / online payments / invoices | **Removed** | API deprecated always-true or capability-based | Portal uses `capabilities.deposits` |

---

## 6. Enforced wedge keys (canonical list post-closeout)

- `maxLocations`, `maxPractitioners`
- `messaging`, `waitlist`, `smsEnabled` (prefs + mutations)
- `advancedAnalytics`, `customerRetentionAnalytics`, `staffPerformanceAnalytics` (dashboard/report surfaces that exist today)

---

## 7. API contract

| Method | Route | Change |
|--------|-------|--------|
| `GET` | `/api/v1/tenant/business-profile` / `profile` | `entitlements` + `usage` complete |
| `GET` | `/api/v1/tenant/subscription` | Same entitlements + usage |
| `GET` | `/api/v1/tenant/entitlements` | **Added** — same `TenantEntitlements` DTO as profile slice; JWT + `X-Tenant-Id` |
| Mutations | staff, locations, waitlist, messaging, dashboard | Stable `entitlements.*` error codes |

---

## 8. Catalog single source

- **TS / marketing:** `packages/shared/src/data/plan-entitlements.json` imported by `@adeni/shared`.
- **C# runtime:** `JsonPlanEntitlementsMatrix` loads `Catalog/plan-entitlements.json` (copied from shared JSON in Api/Infrastructure csproj); `PlanEntitlementsMatrixStartupService` initializes before traffic.
- **Tests:** Infrastructure.Tests copy shared + seed JSON to `Catalog/` for parity assertions.

Codegen remains optional; parity tests are required.

---

## 9. Tests

- [x] Parity: JSON seed = shared catalog = C# matrix for all keys × SKUs
- [x] Comparison: deferred module rows absent from `PLAN_COMPARISON`
- [x] Portal gates on profile entitlements (manual + existing component tests)
- [x] `dotnet test Adeni.slnx -c Release` + shared vitest `plan-entitlements.test.ts`

---

## 10. Acceptance criteria

- [x] Deposits / online payments / invoices are **not** SKU comparison rows; portal uses **capabilities** for deposit UX
- [x] `PLAN_COMPARISON` contains only enforced or honest informational rows
- [x] Waitlist, staff cap, and SMS toggle UX match server enforcement
- [x] JSON ↔ C# parity test green; runtime JSON load documented
- [x] [sprints.md](../sprints.md) reflects 33-closeout done
- [x] Admin pricing matrix uses `PLAN_ADMIN_MATRIX_ROW_DEFS` (full seed + deferred notes)
