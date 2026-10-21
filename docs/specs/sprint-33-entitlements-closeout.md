# Spec: Sprint 33 — Entitlements v2 closeout

> Copy lineage: [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md) (MVP done). **This spec closes the entitlements loop before Notifications (38), Reporting (39), and Retention (37).**  
> Use standing play 1 in [AGENTS.md](../../AGENTS.md). Do not write code until this spec is reviewed.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 33 — Entitlements v2 closeout (`33-closeout`) |
| **Author** | |
| **Status** | Draft |
| **Created** | 2026-10-21 |
| **Depends on** | [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md), [sprint-33-saas-subscription-billing-mrr.md](./sprint-33-saas-subscription-billing-mrr.md) |

---

## 1. Goal (one sentence)

Finish entitlements v2 **parity, portal UX, and matrix hygiene** so tier gates are trustworthy and marketing comparison reflects **enforced wedges only**—before building Notifications, Reporting, and Retention on top of the same keys.

---

## 2. Context

- **Sprint tracker:** [docs/sprints.md](../sprints.md) — add **33-closeout** under Sprint 33; implementation order: **33 closeout → 38 notifications → 39 reporting → 37 retention**.
- **MVP spec (done):** [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md) — seed-driven matrix, location/practitioner caps, waitlist, messaging, analytics gates.
- **Machine-readable seed:** [plan-entitlements.seed.json](./adeni-subscription-pricing-v1/plan-entitlements.seed.json) ↔ `packages/shared/src/data/plan-entitlements.json` ↔ `PlanEntitlementsCatalog.cs`.
- **Marketing matrix:** `PLAN_COMPARISON` / `PLAN_COMPARISON_ROW_DEFS` in [plan-entitlements.ts](../../packages/shared/src/plan-entitlements.ts).
- **Related billing:** [sprint-33b-admin-commercial-config.md](./sprint-33b-admin-commercial-config.md) — price/trial only; entitlements remain SKU-driven.

---

## 3. In scope

- [ ] **Portal parity** — waitlist entry points hidden/disabled when `waitlist` false; SMS toggle in notification settings respects `smsEnabled` (hide or read-only with upgrade copy on `/plan` only, not ops nag banners); **staff cap UX** — add practitioner at cap disabled + usage on `/plan` and staff list header (`practitionersActive` / `maxPractitioners`).
- [ ] **JSON ↔ C# parity** — automated test (and optional **codegen** per v2 open question) asserting `plan-entitlements.json`, seed file, and `PlanEntitlementsCatalog` / `SubscriptionEntitlements` expose the same keys and values for all SKUs.
- [ ] **Sprint status reconciliation** — align [sprints.md](../sprints.md) 33-entitlements-v2 row, this closeout row, and v2 spec §9 acceptance checkboxes with shipped code; cross-ref **38/39/37** sequence in Next up note.
- [ ] **Label matrix rows** — document and implement three row classes in shared + admin pricing UI:
  - **Enforced** — server `IEntitlementsService` + portal gate (caps, waitlist, SMS tier, analytics/export/reminder keys when modules exist).
  - **Deferred** — key stays in **seed** for future enforcement; **shown on `/plan`** as tier differentiation with “coming soon” or module backlog link; **not** fake-enforced in API until module ships.
  - **Informational** — sales/support only; **removed from `PLAN_COMPARISON`** (and optionally from runtime payload if unused).
- [ ] **Matrix cleanup** — apply §5 decision table (remove / merge / keep deferred / keep enforced).

---

## 4. Out of scope

- New product features (scheduled reminders, export orchestrator, retention SKUs) — those sprints own behavior; closeout only **prepares keys and comparison rows**.
- **DB-persisted entitlement catalog** or admin hot-edit of flags (stay seed + codegen).
- Per-tenant entitlement overrides beyond commercial price/trial (33b).
- Re-opening Sprint 33 billing/webhook work except docs cross-links.

---

## 5. Matrix cleanup (deposits and related keys)

### 5.1 Deposits — Finance capability vs tier gate

| Concept | Source | Purpose |
|---------|--------|---------|
| **Booking deposit capability** | Tenant `capabilities` (e.g. `deposits`), `depositPercent` on profile, Finance `PaymentIntentType.Deposit` | Business-type / settings: *whether this business accepts booking deposits* |
| **`deposits` seed flag** | `plan-entitlements.json` | Today **true for all SKUs** — not a wedge |

**Decision:** Remove `deposits` from **tier comparison** (`PLAN_COMPARISON_ROW_DEFS`) and from admin “plan matrix” marketing rows. Optionally **remove from seed** in a follow-up PR if C# no longer maps it; until then keep in seed as deprecated always-`true` for backward-compatible API payloads **or** drop from API `entitlements` object with deprecation note in shared Zod. Portal continues to branch on **`capabilities.deposits`**, not SKU.

If product wants a plan-row for deposits later, add **`Booking deposits`** as an **informational** row tied to copy (“Available when enabled in Payment settings”) — **not** a SKU boolean.

### 5.2 Concrete key actions

| Key | Seed (machine matrix) | `PLAN_COMPARISON` | Enforcement class | Action |
|-----|----------------------|-------------------|-------------------|--------|
| `deposits` | Remove or deprecate always-true | **Remove** | N/A (use Finance capability) | **Remove from comparison** |
| `onlinePayments` | Keep (always true) | **Remove** | Informational / table stakes | **Remove from comparison** |
| `invoicesReceipts` | Keep (always true) | **Remove** | Informational / table stakes | **Remove from comparison** |
| `unlimitedBookings` | Keep | **Keep** (shows “Unlimited”) | Informational (always true) | **Keep** — honest “no monthly cap” message |
| `unlimitedServices` | Keep | **Keep** | Informational (always true) | **Keep** |
| `publicProfile`, `discoverListing` | Keep | **Keep** | Informational (always true) | **Keep** (core value props) |
| `additionalLocationsAllowed` | Keep for Business upsell logic | **Remove** | Informational (derived from caps + SKU) | **Remove from comparison** — caps row suffices |
| `additionalPractitionersAllowed` | Keep | **Remove** | Informational | **Remove from comparison** |
| `basicAnalytics` | Keep | **Remove** | Redundant vs `advancedAnalytics` | **Remove from comparison**; gate widgets with `advancedAnalytics` / specific keys |
| `smsUsageBilling` | Keep until billing | **Remove** | Deferred (no SMS metered billing yet) | **Remove from comparison**; keep seed for future |
| `maxLocations`, `maxPractitioners` | Keep | **Keep** | **Enforced** | **Keep** |
| `messaging`, `waitlist` | Keep | **Keep** | **Enforced** | **Keep** |
| `smsEnabled` | Keep | **Keep** | **Enforced** (prefs + future Twilio) | **Keep** |
| `basicReminders`, `configurableReminders`, `followUpAutomation` | Keep | **Keep** | **Deferred → enforced** with Sprint 38 | See §5.3 |
| `advancedAnalytics`, `customerRetentionAnalytics`, `staffPerformanceAnalytics` | Keep | **Keep** | **Enforced** (dashboard widgets) / deferred widgets until built | **Keep**; closeout verifies widget map |
| `basicExports`, `advancedReports`, `excelExport`, `multiLocationReporting` | Keep | **Keep** | **Deferred** until Sprint 39 | See §5.3 |
| `advancedPermissions`, `prioritySupport` | Keep | **Keep** | Informational / partial | **Keep** on comparison; priority support informational only |

### 5.3 Reminder and export keys — comparison table approach

**Recommendation:** **Keep keys in seed and API** for Notifications (38) and Reporting (39) to gate against, but mark rows **Deferred** in closeout docs until Phase 1 of those modules ships. **Keep rows on `/plan` comparison** so Pro/Business differentiation is visible; do **not** add stub API 403s that block unrelated flows.

When Sprint **38** lands: enforce `basicReminders` / `configurableReminders` / `followUpAutomation` on scheduler + template APIs; tie SMS sends to `smsEnabled`.

When Sprint **39** lands: enforce `basicExports`, `advancedReports`, `excelExport`, `multiLocationReporting` on `IReportExportService` and cross-location query paths.

**Alternative (not recommended):** Remove reminder/export rows from comparison until modules ship — avoids over-promising but hides upgrade story; product prefers visible deferred rows.

---

## 6. Enforced wedge keys (canonical list post-closeout)

Server must gate (existing or verified in closeout):

- `maxLocations`, `maxPractitioners`
- `messaging`, `waitlist`, `smsEnabled` (prefs + mutations)
- `advancedAnalytics`, `customerRetentionAnalytics`, `staffPerformanceAnalytics` (dashboard/report surfaces that exist today)
- WhatsApp / advanced reminder flags as mapped in v2 spec (`configurableReminders` / legacy `advancedReminders` helper)

---

## 7. API contract

No new routes required. Verify existing payloads:

| Method | Route | Change |
|--------|-------|--------|
| `GET` | `/api/v1/tenant/business-profile` | `entitlements` + `usage` complete; deprecated keys documented |
| `GET` | `/api/v1/tenant/subscription` | Same entitlements + usage |
| Mutations | staff, locations, waitlist, messaging, dashboard | Stable `entitlements.*` error codes |

---

## 8. Files to touch (implementation)

| Layer | Files |
|-------|-------|
| **Domain / Application** | `ErrorCodes.cs`, `IEntitlementsService.cs`, `SubscriptionEntitlements.cs` |
| **Infrastructure** | `EntitlementsService.cs`, `PlanEntitlementsLoader` / codegen output, dashboard widget filters |
| **Api** | Profile/subscription serializers |
| **Tests** | `EntitlementsServiceTests.cs`; **new** `PlanEntitlementsParityTests.cs` (C# ↔ JSON); integration staff/waitlist/SMS prefs |
| **packages/shared** | `plan-entitlements.json`, `plan-entitlements.ts` (`PLAN_COMPARISON_ROW_DEFS`), `tenantEntitlementsSchema`, `plan-entitlements.test.ts` |
| **Codegen (optional)** | Script or MSBuild target: seed → `PlanEntitlementsCatalog.cs` + copy to shared JSON |
| **apps/portal** | `portal-nav.ts`, waitlist UI, settings SMS toggle, staff/locations cap UX, `plan/*` comparison |
| **apps/admin** | `pricing-entitlements.component.ts` row list synced with shared defs |
| **apps/discover** | `plan/*` if comparison shared |
| **Docs** | [sprints.md](../sprints.md), [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md) status note |

---

## 9. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Unchanged — gates are per-tenant SKU resolution |
| **Auth / roles** | RBAC first; entitlements additive |
| **Cache** | Profile/subscription cache keys unchanged |
| **Audit** | No new admin mutations |
| **PII** | No change |
| **Observability** | Optional `entitlements.denied` metric from v2 |

---

## 10. Tests

- [ ] **Parity:** JSON seed = shared catalog = C# catalog for all keys × SKUs
- [ ] **Comparison:** `PLAN_COMPARISON` equals `buildPlanComparison()`; removed rows absent
- [ ] **Portal:** Starter waitlist hidden; staff add blocked at cap with correct error code
- [ ] **SMS prefs:** Starter cannot enable SMS channel in API when `smsEnabled` false
- [ ] Run: `dotnet test Adeni.slnx -c Release` + shared vitest for `plan-entitlements.test.ts`

---

## 11. Acceptance criteria

- [ ] Deposits / online payments / invoices are **not** SKU comparison rows; portal uses **capabilities** for deposit UX
- [ ] `PLAN_COMPARISON` contains only enforced, deferred-product, or honest informational rows per §5.2
- [ ] Waitlist, staff cap, and SMS toggle UX match server enforcement on Starter vs Pro
- [ ] JSON ↔ C# parity test green (codegen optional but documented)
- [ ] [sprints.md](../sprints.md) reflects 33-closeout done + points to 38 → 39 → 37 order
- [ ] Admin pricing matrix uses same row defs as `@adeni/shared` (single source)

---

## 12. Open questions

| Question | Decision |
|----------|----------|
| Remove `deposits` from API payload entirely? | Prefer deprecate one release, then remove from Zod |
| Codegen in closeout vs parity test only? | **Parity test required**; codegen nice-to-have same PR |
| Merge `basicReminders` into one comparison row? | Keep three rows until Notifications templates differentiate |
