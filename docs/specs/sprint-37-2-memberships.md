# Spec: Sprint 37.2 — Memberships (plans, customer memberships, recurring billing)

> Copied from [`_template.md`](./_template.md). Standing play 1 in [AGENTS.md](../../AGENTS.md).  
> **Parent:** [sprint-37-retention-module.md](./sprint-37-retention-module.md) §3 *37.2 Memberships*, §6.4 entitlements, §7.3 API sketch.  
> **Pattern source:** [sprint-37-1-packages-benefit-ledger.md](./sprint-37-1-packages-benefit-ledger.md) (implemented) — memberships reuse its module layout, `portal.retention` permission, `RetentionActor`, audit style, idempotency and stub-gated online checkout.  
> **Delivery:** staged PRs (§3). **Only slice 37.2-A is authorized/implemented by this spec's first PR.** Dev/local only — no staging/prod rollout implied.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 37.2 — Memberships |
| **Author** | Engineering |
| **Status** | **Approved to start (dev/local).** Slices **37.2-A** through **37.2-E** implemented. Live provider billing stays stub-gated until Sprint 34. |
| **Created** | 2026-10-29 |

---

## 1. Goal (one sentence)

Let beauty & wellness businesses define **recurring membership plans** (price, billing interval, per-cycle service credits, typed rollover/freeze/cancel policies), enrol roster customers, and — in later slices — renew them **only on provider-confirmed `PaymentCompleted`**, with a Retention-owned `TenantMembershipMrr` metric — **without** wallet/custody, stored value, or a rules DSL.

---

## 2. Context

- **Sprint:** 37.1 packages shipped ([sprints.md](../sprints.md) row 37); 37.2–37.5 draft. Sprint 34 live settlement is **not** done → online recurring checkout stays **stub/sandbox-gated** exactly like 37.1 online package purchase (`retention.online_purchase_unavailable` pattern).
- **Strategy:** [product-strategy.md](../product-strategy.md) — retention is a thin bounded context; Subscriptions module = **Adeni SaaS billing only** (`PlatformSubscriptionMrr`), never customer memberships.
- **Customer anchor:** all customer memberships FK `TenantCustomer` ([sprint-35-tenant-customer-records.md](./sprint-35-tenant-customer-records.md)).
- **Benefits:** per-cycle service credits reuse the 37.1 benefit-ledger idea (granted/used/reserved + immutable transactions + `rowversion`). Decision for slice B: **extend `retention.benefit_balances` with an optional `CustomerMembershipId`/cycle key** vs. a parallel table — default: extend, keep one ledger and one `IBenefitRedemptionService` so Booking gains no new port (§10).
- **Grep results (2026-10-29, `main`/`dev` clean):** no membership entities, tables, routes or entitlement keys exist. "Membership" in code today = *business-user membership* (`IBusinessMembershipQuery`, `BusinessMembershipQuery`) — **unrelated**; do not reuse that name. `plan-entitlements` has `packages` but **no** `memberships` / `recurringMembershipBilling` key. `report-keys` mentions membership only as report catalogue placeholders. F1 customer profile Retention source = packages only.

---

## 3. Delivery slices

| Slice | Content | Status |
|-------|---------|--------|
| **37.2-A — Plan catalog (first PR)** | `memberships` entitlement key; `MembershipPlan` + benefit lines (Domain, DACPAC, EF); `IMembershipPlanService`; tenant plan CRUD API; shared Zod/i18n/api-client; portal **Memberships** plan list/create/deactivate shell route; tests | **Implemented in this PR** |
| 37.2-B — Customer memberships (in-person) | `CustomerMembership` + lifecycle enum (Pending → Active → PastDue → Frozen → CancelPending → Cancelled → Expired); enrol at till/transfer (`Idempotency-Key`); cycle benefit grant; owner cancel/freeze; audit; portal customer tab | **Implemented** (§12) |
| 37.2-C — Billing coordinator | `IMembershipBillingCoordinator`; `recurringMembershipBilling` entitlement; provider mandate/subscription **references only**; activate/renew **only** on idempotent `PaymentCompleted`; PastDue grace policy; Murphy webhook rules; stub-gated online | **Implemented** (§13) |
| 37.2-D — Discover | Public plan list, subscribe (customer JWT), my memberships, cancel/freeze requests; `/api/v1/me/retention/memberships` | **Implemented** (§13). Customer list UI is `/app/benefits/memberships` (C1), not `/my-*`. |
| 37.2-E — Booking benefits + MRR | Redeem membership credits via `IBenefitRedemptionService`; `TenantMembershipMrr` calc (port ready for 37.5 `IRetentionAnalyticsService`); `MembershipActivated/Renewed/PaymentFailed/Cancelled` events (Notifications consume in 38B+) | **Implemented** (§13) |

---

## 4. In scope (slice 37.2-A — this PR)

- [x] **Entitlement `memberships`** — Starter `false`, Professional `true`, Business `true` ([parent §6.4](./sprint-37-retention-module.md)); added to seed + shared catalog + `TenantEntitlements`; enforced on **all** plan routes (read + write → plans are an entitled feature; list returns 403 `entitlements.memberships` when not entitled so portal can hide nav).
- [x] **Domain** `Adeni.Domain/Retention/` — `MembershipPlan`, `MembershipPlanBenefit`, enums `MembershipBillingInterval` (Monthly / Quarterly / Annually), `MembershipRolloverPolicy` (None / CarryOverOneCycle), `MembershipCancellationPolicy` (EndOfCycle / Immediate); typed columns, **no DSL**.
- [x] **DACPAC** `retention.membership_plans`, `retention.membership_plan_benefits` (tenant FK, composite `(TenantId, Id)` AK, check constraints, unique service per plan). **No EF migrations** (DACPAC is schema authority).
- [x] **Application** `IMembershipPlanService` (List / Get / Create / Update / Deactivate) + DTOs + `IRetentionEntitlementGuard.EnsureMembershipsEntitledAsync`.
- [x] **Infrastructure** `MembershipPlanService`, EF config (tenant query filter), DI in `AddRetentionModule`, audit actions.
- [x] **Api** `/api/v1/tenant/retention/memberships/plans` (§6); `portal.retention` permission; error mapping.
- [x] **`@adeni/shared`** (Zod + types + i18n error keys EN/FR + entitlement catalog) and **`@adeni/api-client`** methods.
- [x] **Portal** — nav item "Memberships" (permission `portal.retention`, entitlement `memberships`), route `/retention/memberships`: plan table, create form, deactivate.
- [x] **Tests** — service unit tests (create/validate/patch/deactivate/entitlement denial/tenant isolation), HTTP integration (auth, Starter-style denial, cross-tenant), entitlement parity.

## 5. Out of scope (this PR — and 37.2 overall unless noted)

- `CustomerMembership`, enrolment, lifecycle transitions, cycle grants, freeze/cancel, rollover **execution** (policies are stored on the plan only) → slices B–E.
- Recurring billing, provider mandates/subscription references, renewals, PastDue handling, webhooks → slice C. **`recurringMembershipBilling` entitlement key is deferred to slice C** (nothing consumes it yet).
- Discover/consumer routes → slice D. Booking integration + MRR → slice E.
- Per-plan location scope (`selected_locations`, cross-location) → **37.4**; slice A plans are tenant-wide.
- Loyalty tiers/referrals/rewards (37.3), analytics export (37.5), Notifications email (38B+).
- Wallet/custody/stored value; platform fee on membership GMV (Sprint 34+); Adeni SaaS subscription code (`Subscriptions` module) — untouched.
- Admin portal write paths.

---

## 6. API contract (slice A)

All under `/api/v1/tenant/retention/memberships/plans`. Business JWT + `X-Tenant-Id` (`TenantAccessMiddleware`); tenant derived from resolved portal user, never from the body.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/memberships/plans?activeOnly=` | `portal.retention` **or** `portal.bookings` + `memberships` entitlement | List plans |
| POST | `/memberships/plans` | `portal.retention` + entitlement | Create plan + benefit lines → `201` |
| GET | `/memberships/plans/{id}` | as list | Detail |
| PATCH | `/memberships/plans/{id}` | `portal.retention` + entitlement | Update (null = unchanged; benefit list replaces when supplied). Will **not** rewrite enrolled memberships (slice B snapshots) |
| POST | `/memberships/plans/{id}/deactivate` | `portal.retention` + entitlement | Soft deactivate (idempotent) |

Error codes (stable, mirrored in `@adeni/shared` i18n): `entitlements.memberships` (403), `retention.membership_plan_not_found` (404), `retention.validation` (400, reused). Plan `POST` create is not `Idempotency-Key`-guarded in this slice (catalog authoring, same as 37.1 `POST packages`); deactivate is naturally idempotent.

```jsonc
// POST body
{
  "name": "Monthly Fade Club",
  "description": "2 cuts a month",
  "price": 25000,
  "currency": "NGN",
  "billingInterval": "monthly",          // monthly | quarterly | annually
  "benefits": [{ "serviceOfferingId": "uuid", "quantityPerCycle": 2 }],
  "rolloverPolicy": "none",               // none | carry_over_one_cycle
  "cancellationPolicy": "end_of_cycle",   // end_of_cycle | immediate
  "allowFreeze": true,
  "pastDueGraceDays": 7                   // 0–30
}
// response adds: id, isActive, createdAt, updatedAt, benefits[{ id, serviceOfferingId, serviceName, quantityPerCycle }]
```

Later slices (sketch, not built): `…/customers/{tenantCustomerId}/memberships/enrol`, `…/customer-memberships/{id}/{freeze|cancel}`, `/businesses/{slug}/retention/memberships/plans`, `/me/retention/memberships`.

---

## 7. Files to touch

| Layer | Files (slice A) |
|-------|-----------------|
| Domain | `Retention/MembershipPlan.cs`, `RetentionEnums.cs` (+3 enums), `RetentionErrorCodes.cs`, `Auditing/AuditActions.cs` |
| Application | `Retention/IMembershipPlanService.cs`, `MembershipModels.cs`, `IRetentionServices.cs` (guard), `Subscriptions/SubscriptionEntitlements.cs` |
| Infrastructure | `Retention/MembershipPlanService.cs`, `RetentionEntitlementGuard.cs`, `RetentionServiceCollectionExtensions.cs`, `Persistence/RetentionModelConfiguration.cs`, `Subscriptions/JsonPlanEntitlementsMatrix.cs` |
| DB | `db/Adeni.Database/retention/Tables/membership_plans.sql`, `membership_plan_benefits.sql` |
| Api | `Controllers/Retention/TenantMembershipPlansController.cs`, `Errors/ApiErrorResponseMapper.cs` |
| Catalog | `docs/specs/adeni-subscription-pricing-v1/plan-entitlements.seed.json`, `packages/shared/src/data/plan-entitlements.json` |
| Shared / client | `packages/shared/src/{retention.ts,plan-entitlements.ts,schemas.ts,i18n/errors.ts,i18n/messages/overlay-*.ts}`, `packages/api-client/src/retention/retention-api.ts` |
| Portal | `app.routes.ts`, `core/portal-nav.ts`, `pages/retention/retention-memberships.component.*` |
| Tests | `Retention/MembershipPlanServiceTests.cs`, `Integration/RetentionIntegrationTests.cs` (+plans), `PlanEntitlementsParityTests.cs`, shared TS tests |

---

## 8. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Entities `ITenantEntity` + `HasQueryFilter`; services always filter `TenantId`; cross-tenant id → 404 `retention.membership_plan_not_found`; tenant routes need `X-Tenant-Id` match → 403 otherwise |
| **Auth / roles** | Reuses `portal.retention` (owner/manager/receptionist per 37.1 matrix). Plan authoring is mutation → permission + backend entitlement |
| **Entitlements** | `IEntitlementsService` is authoritative; `memberships` added to seed. Portal nav hide is UX only. Do not gate on `PLAN_ENTITLEMENTS` seed in portal flows — use profile `entitlements` |
| **Audit** | `retention.membership_plan_created/updated/deactivated` via existing `IAuditLogWriter` (tenant audit, same shape as 37.1) |
| **PII** | Plans hold no customer PII; nothing new logged |
| **Cache** | None in slice A (optional `tenant:{id}:retention:membership-plans` when Discover storefront lands in D) |
| **Payments / webhooks** | None in slice A (no money moves). Slice C must follow Murphy webhook rules |
| **Scope guardrails** | No wallet/custody tables; `Price` is a list price for the recurring charge, not stored value |

---

## 9. Tests

- [x] Unit (`MembershipPlanServiceTests`, in-memory EF): create happy path; validation (name/price/currency/interval/benefits/grace); patch keeps unspecified fields; deactivate idempotent; not entitled → `entitlements.memberships`; cross-tenant get/patch/deactivate → not found; audit written
- [x] Integration: owner creates/lists/gets/patches/deactivates; unauthenticated → 401; cross-tenant `X-Tenant-Id` → 403; plan from tenant A invisible to B
- [x] Parity: `memberships` seed ⇄ `TenantEntitlements`
- [x] Validation failure path (`retention.validation` → 400)

Run: `dotnet test Adeni.slnx -c Release`

---

## 10. Acceptance criteria (slice A)

- [x] Professional/Business tenant can create, edit, list and deactivate membership plans; Starter gets 403 `entitlements.memberships` on every plan route
- [x] Plans are tenant-isolated; no cross-tenant read or write
- [x] Plan stores typed rollover / cancellation / freeze / grace policies (no DSL)
- [x] Portal shows Memberships nav only when entitled + permitted; list/create/deactivate works against local API
- [x] No `Subscriptions`-module, wallet, payment-provider or Booking code touched

---

## 11. Open questions

| Question | Proposed default | Decision |
|----------|------------------|----------|
| Membership credits: extend `benefit_balances` vs. new table | Extend with `CustomerMembershipId` + cycle key; one ledger / one booking port | TBD at slice B |
| Rollover cap | `carry_over_one_cycle` only (typed, no counts) | TBD |
| Billing provider per market | Paystack (Lagos) / Stripe (Ottawa) via market registry; refs only | TBD slice C |
| Billing intervals | monthly / quarterly / annually | Proposed |
| Pro-rating on mid-cycle enrol | None — first cycle starts at enrol | TBD slice B |
| Plan price change vs. active members | Price snapshot on enrolment; plan edits affect new enrolments only | TBD slice B |

---

## 12. Slice 37.2-B — Customer memberships, in-person (implemented)

**Scope:** enrol a roster customer at the till / by transfer, grant cycle-1 credits through the 37.1 benefit ledger, owner freeze / resume / cancel, portal customer tab. **Not built:** renewals, PastDue/Expired transitions, rollover execution, cycle-end sweep of `CancelPending`, online checkout, billing (slice C); Discover (D); booking redemption of membership credits, MRR, events consumed by Notifications (E).

**Decisions (closes §11 rows for B):**

| Question | Decision |
|----------|----------|
| Credits storage | Extended `retention.benefit_balances`: `CustomerPackageId` now nullable, plus `CustomerMembershipId` + `CycleNumber`; check constraint `CK_benefit_balances_Owner` = exactly one owner; unique indexes are filtered per owner type. One ledger, one `IBenefitRedemptionService`; membership balances are not yet redeemable (slice E). |
| Pro-rating | None — cycle 1 starts at enrolment; `CurrentCycleEnd` = start + 1 / 3 / 12 months. |
| Price/plan snapshot | Name, price, currency, interval, rollover, cancellation, freeze, grace days copied to `customer_memberships`; benefit lines to `customer_membership_benefits` (renewal grants in slice C read these, not the live plan). |
| Duplicates | One live (Pending/Active/PastDue/Frozen/CancelPending) membership per customer + plan: service pre-check + filtered unique index → `409 retention.membership_already_active`. |
| Cancel semantics | Plan `immediate` (or still `Pending`) → `Cancelled`, open reservations released, remaining credits forfeited. `end_of_cycle` → `CancelPending`; credits stay until cycle end (slice C closes it). Repeat cancel / freeze / unfreeze are no-ops. |
| Authority | Enrol: `portal.retention` + `Idempotency-Key`. Freeze / unfreeze / cancel: owner only (`retention.owner_required`). All routes need `memberships` entitlement, including reads. |
| Idempotency | Enrol reuses `retention.benefit_operations` (new `ResultCustomerMembershipId`); same key + body replays, different body → `409 retention.idempotency_conflict`. |

**API** (`/api/v1/tenant/retention`):

| Method | Route | Notes |
|--------|-------|-------|
| GET | `/customers/{tenantCustomerId}/memberships` | `portal.retention` or `portal.bookings` |
| POST | `/customers/{tenantCustomerId}/memberships/enrol` | `201`; body `{ membershipPlanId, businessLocationId, paymentMethod: till\|transfer, note? }`; `Idempotency-Key` required |
| GET | `/customer-memberships/{id}` | detail incl. current-cycle `balances` |
| POST | `/customer-memberships/{id}/freeze` · `/unfreeze` · `/cancel` | owner only; cancel body `{ reason? }` |

New stable codes: `retention.membership_not_found` (404), `retention.membership_plan_inactive`, `retention.membership_already_active`, `retention.membership_invalid_state`, `retention.membership_freeze_not_allowed` (all 409). Audit: `retention.membership_enrolled|frozen|unfrozen|cancelled`. Domain events: `CustomerMembershipActivated`, `CustomerMembershipCancelled` (no consumers yet).

**Schema (DACPAC, no EF migrations):** new `customer_memberships`, `customer_membership_benefits`; altered `benefit_balances`, `benefit_operations`. Existing dev databases need a SqlPackage publish (nullable `CustomerPackageId` + filtered index rebuild).

**Tests:** `CustomerMembershipServiceTests` (enrol/replay/conflict/duplicate/inactive/cross-tenant/validation/entitlement/snapshot, freeze, cancel policies, owner-only), `CustomerMembershipsIntegrationTests` (HTTP contract, auth, Starter denial, cross-tenant), shared Zod + i18n parity tests.

---

## 13. Slices 37.2-C / D / E — implementation notes

**C — billing coordinator.** `IMembershipBillingCoordinator` starts an online subscription as a `Pending` membership plus a Finance payment intent. A cycle is granted only from the idempotent `PaymentCompleted` handler (`MembershipPaymentHandler`). Provider mandate and subscription references are stored on the membership; Retention does not hold funds. `recurringMembershipBilling` is Starter `false`, Professional/Business `true`, and is required together with `memberships` for online subscribe and renewal charges. Outside Development/Testing (or when the stub provider is off), subscribe returns `503 retention.online_subscription_unavailable` until Sprint 34 live settlement. The scheduled sweep closes `CancelPending` at cycle end, starts renewal charges, moves unpaid online memberships to `PastDue`, and expires them after the snapshotted grace days. In-person renewals are `POST …/customer-memberships/{id}/renew` (till/transfer, `Idempotency-Key`) once the cycle has ended.

**D — Discover.** Anonymous `GET /api/v1/businesses/{slug}/retention/memberships/plans`. Customer `POST …/memberships/plans/{id}/subscribe` (`Idempotency-Key`). Signed-in `GET /api/v1/me/retention/memberships` plus self-service freeze / unfreeze / cancel (the caller's own rows only; unpaid pending checkouts are omitted from the list). The public business profile renders the plan list. There is no `/my-memberships` route — the signed-in list lives at `/app/benefits/memberships` with the customer app (C1).

**E — booking + MRR.** `IBenefitRedemptionService` redeems membership cycle credits the same way as package credits (exactly one of `customerPackageId` / `customerMembershipId`). Rollover `carry_over_one_cycle` is applied when a new cycle is granted. `GET /api/v1/tenant/retention/memberships/mrr` returns `TenantMembershipMrr` per currency for Active and PastDue members (quarterly ÷ 3, annually ÷ 12). Domain events emitted for Notifications (38B+), with no email sender here: `CustomerMembershipActivated`, `CustomerMembershipRenewed`, `CustomerMembershipPaymentFailed`, `CustomerMembershipCancelled`, `CustomerMembershipExpired`.

**New codes:** `entitlements.recurringMembershipBilling` (403), `retention.online_subscription_unavailable` (503), `retention.membership_renewal_not_due` (409), `retention.membership_not_redeemable` (409).

**Schema (DACPAC):** `retention.membership_payments`; `customer_memberships` provider-reference and past-due columns; `benefit_balances.CarriedIn` for one-cycle rollover. Publish with SqlPackage before exercising billing on an existing dev database.

**Deferred to Sprint 34:** live Paystack/Stripe mandates, production online subscribe, and webhook edge rate limits beyond the existing payment webhook rules. Stub confirm in Development/Testing is the only online path.

