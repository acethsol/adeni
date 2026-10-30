# Spec: Sprint 37.3 — Rich loyalty (program + points ledger → tiers, rewards, referrals)

> Copied from [`_template.md`](./_template.md). Standing play 1 in [AGENTS.md](../../AGENTS.md).  
> **Parent:** [sprint-37-retention-module.md](./sprint-37-retention-module.md) §3 *37.1 Basic loyalty* + *37.3 Rich loyalty*, §6.4 entitlements, §6.7 concurrency, §9 data model.  
> **Pattern source:** [sprint-37-1-packages-benefit-ledger.md](./sprint-37-1-packages-benefit-ledger.md) and [sprint-37-2-memberships.md](./sprint-37-2-memberships.md) — same module layout, `portal.retention` permission, `RetentionActor`, audit style, `Idempotency-Key` ledger replays, DACPAC-only schema.  
> **Delivery:** staged PRs (§3). **Only slice 37.3-A is authorized/implemented by this spec's first PR.** Dev/local only — no staging/prod rollout implied.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 37.3 — Rich loyalty |
| **Author** | Engineering |
| **Status** | **Approved to start (dev/local).** Slice **37.3-A** (program config + points ledger foundation) implemented; B–E draft |
| **Created** | 2026-10-29 |
| **Depends on** | 37.1 packages (shipped), Sprint 35 `TenantCustomer`, Sprint 33 entitlements. **Not** dependent on 37.2 C–E (see §2 risk) |

---

## 1. Goal (one sentence)

Give beauty & wellness businesses a **tenant-scoped loyalty program** — configurable earn/redeem rules and an immutable **points ledger** per roster customer — as the foundation for earning on paid completed services, redeeming at checkout, **tiers, a reward catalog and referrals**, **without** wallet/custody, a platform-wide points currency, or a rules DSL.

---

## 2. Context

- **Sprint:** [sprints.md](../sprints.md) row 37 — 37.1 packages shipped; 37.2 A/B shipped (dev/local), **37.2 C–E (billing coordinator, Discover, booking benefits + `TenantMembershipMrr`) not done**; 37.3–37.5 draft. The user explicitly chose to proceed with 37.3 now.
- **Dependency / risk note (37.2 C–E not done):**
  - 37.3 does **not** need membership billing, Discover memberships or MRR. Loyalty hangs off `TenantCustomer` and the existing booking / payment events, not memberships. Parent order (§15) lists 37.3 after 37.2 for sequencing, not for a technical dependency.
  - **Real prerequisite gap — basic loyalty never shipped.** Parent §3 puts *basic loyalty* (program config, ledger, earn, simple redeem) in 37.1, but 37.1 was narrowed to packages only ([37.1 spec §4](./sprint-37-1-packages-benefit-ledger.md): "Loyalty … Sprint 37.1 remainder / 37.3"). Grep (2026-10-29, `dev`): **zero** loyalty code — no entities, tables, routes or entitlement key; "loyalty" appears only in docs. **37.3 therefore absorbs the 37.1-remainder** (slices A–C) before adding *rich* features (D–E). Tiers, rewards and referrals cannot be built on nothing.
  - **Risks:** (1) merge conflicts with later 37.2-C/E in shared files (`RetentionEntitlementGuard`, `RetentionServiceCollectionExtensions`, seed JSON, `portal-nav.ts`, i18n) — keep diffs additive and rebase; (2) 37.2-E adds `MembershipActivated/...` events and `PaymentCompleted` handling, and slice B here also listens to payment/booking events — keep handlers idempotent and independent; (3) **Notifications events** (`LoyaltyPointsEarned`, `LoyaltyTierChanged`, …, parent §6.5) are raised later and consumed in 38B+, not here; (4) Online redemption touches Finance totals → stays **stub/sandbox-gated** until Sprint 34 live settlement, like 37.1/37.2.
- **Customer anchor:** all retention state FKs `TenantCustomer` ([sprint-35-tenant-customer-records.md](./sprint-35-tenant-customer-records.md)); no duplicate CRM, no `Customer.LoyaltyPoints` column.
- **Ledger model:** same idea as the 37.1 benefit ledger — mutable account counters (`rowversion`) + append-only transactions; no client-side point mutation.
- **Non-custodial:** points are **tenant-scoped program credits**, not cash, not withdrawable, not transferable between tenants; redemption yields a **discount on that tenant's own service price**. No wallet/balance tables in Finance.
- **Grep results (2026-10-29, clean `dev`):** `loyalty` only in docs; entitlements have `packages` + `memberships` but no `loyaltyBasic` / `loyaltyTiers` / `referrals`; `customerRetentionAnalytics` is an unrelated pre-existing analytics flag (not touched).

---

## 3. Delivery slices

Boundary vs the parent spec: **A–C = parent "37.1 Basic loyalty"** (finishing it); **D–E = parent "37.3 Rich loyalty"** (tiers + rewards, referrals).

| Slice | Content | Entitlement | Status |
|-------|---------|-------------|--------|
| **37.3-A — Program config + points ledger (first PR)** | `loyaltyBasic` key; `LoyaltyProgram`, `LoyaltyAccount`, `LoyaltyTransaction` (Domain, DACPAC, EF); `ILoyaltyProgramService`, `ILoyaltyAccountService`; tenant API: program get/upsert, per-customer account + transactions, **audited manual adjustment** (`Idempotency-Key`); shared Zod/i18n/api-client; portal **Loyalty** settings page shell; tests | `loyaltyBasic` | **Implemented in this PR** (§12) |
| 37.3-B — Earn | `ILoyaltyEarnService.StageEarnForPaidBookingAsync`: earn on **Paid** `scheduled_appointment` bookings (qualifying spend after package credits; idempotent per booking), `LoyaltyPointsEarned` event, reversal on refund, portal customer **Loyalty** tab | `loyaltyBasic` | **Implemented (dev/local)** |
| 37.3-C — Redeem + expiry | `ILoyaltyRedemptionService` fixed discount at booking create (multiples of `redeemPoints`, capped to payable after package credits); remaining balance respects discount; release on cancel/refund; FIFO expiry job; `GET /api/v1/me/retention/loyalty` + Discover `…/loyalty` + quote | `loyaltyBasic` | **Implemented (dev/local)** |
| 37.3-D — Tiers + reward catalog | Configurable `LoyaltyTier` (threshold on **lifetime earned**, parent §14 default), tier evaluation on earn, `LoyaltyTierChanged` event; **reward catalog** (non-discount rewards, e.g. free add-on) + redemption beyond the simple discount | `loyaltyTiers` (Pro+) | Draft |
| 37.3-E — Referrals | Referral program config, code/link, qualification on referee's **first paid completed service**, fraud controls (self-referral, same-customer, per-referrer cap), **single** idempotent reward issuance, `ReferralQualified/ReferralRewardIssued` events | `referrals` (Pro+) | Draft |

---

## 4. In scope (slice 37.3-A — this PR)

- [x] **Entitlement `loyaltyBasic`** — Starter `true`, Professional `true`, Business `true` ([parent §6.4](./sprint-37-retention-module.md)); added to both seed copies, `TenantEntitlements`, `JsonPlanEntitlementsMatrix`, shared `TenantEntitlements` Zod + catalog list; enforced on **every** loyalty route (read + write) in the service via `IRetentionEntitlementGuard.EnsureLoyaltyBasicEntitledAsync`. Portal nav hide is UX only.
- [x] **Domain** `Adeni.Domain/Retention/` — `LoyaltyProgram` (one per tenant), `LoyaltyAccount` (one per tenant customer, `rowversion`), `LoyaltyTransaction` (append-only), enum `LoyaltyTransactionKind` (`Earn`, `Redeem`, `Adjust`, `Expire` — only `Adjust` is written in A; the rest are ledger vocabulary reserved for B/C so later slices need no enum/CK change). Typed columns — **no DSL**. Account methods enforce `PointsBalance >= 0`.
- [x] **DACPAC** `retention.loyalty_programs`, `retention.loyalty_accounts`, `retention.loyalty_transactions` (tenant FK, composite `(TenantId, Id)` AK, check constraints, unique program per tenant, unique account per tenant customer, unique `(AccountId, Sequence)`, filtered unique idempotency key). **No EF migrations** (DACPAC is schema authority).
- [x] **Application** `ILoyaltyProgramService` (Get / Upsert) and `ILoyaltyAccountService` (GetForCustomer / ListTransactions / Adjust) + DTOs + `RetentionErrorCodes` + guard method.
- [x] **Infrastructure** `LoyaltyProgramService`, `LoyaltyAccountService`, EF config (tenant query filter), DI in `AddRetentionModule`, audit actions.
- [x] **Api** `/api/v1/tenant/retention/loyalty/...` (§6); `portal.retention` permission; owner-only config, owner/manager-only adjustment; error mapping.
- [x] **`@adeni/shared`** (Zod + types + i18n error keys EN/FR + entitlement catalog) and **`@adeni/api-client`** methods.
- [x] **Portal** — nav item "Loyalty" (permission `portal.retention`, entitlement `loyaltyBasic`), route `/retention/loyalty`: program settings form (enable toggle, earn rate, redeem rate, minimum, expiry) — a **shell**: no points are earned yet, and the page says so.
- [x] **Tests** — service unit tests, HTTP integration, entitlement parity, shared TS tests.

## 5. Out of scope (this PR)

- **Earning** points on bookings / payments, **redeeming** points at checkout, **expiry sweep**, reversal on refund → slices B–C. The program stores earn/redeem/expiry **settings only**; nothing consumes them yet.
- Tiers, reward catalog, referrals → D–E. `loyaltyTiers` and `referrals` entitlement keys are **deferred** to those slices (nothing consumes them yet — same rule used for `recurringMembershipBilling` in 37.2).
- Discover / consumer routes (`/api/v1/me/retention/loyalty`) → slice C.
- Per-location program scope / cross-location redemption → **37.4**; the program is tenant-wide.
- Booking, Finance, Notifications, Reporting code — **untouched** (no `Booking → Retention` loyalty hook yet).
- Retention analytics (`IRetentionAnalyticsService`, points issued/redeemed/outstanding) → 37.5.
- Wallet/custody/stored value; platform-wide points; transferring points between customers or tenants; exchanging points for cash.
- Admin portal write paths. Adeni SaaS `Subscriptions` module — untouched.

---

## 6. API contract (slice A)

All under `/api/v1/tenant/retention/loyalty`. Business JWT + `X-Tenant-Id` (`TenantAccessMiddleware`); tenant derived from the resolved portal user, never from the body. Every route needs the `loyaltyBasic` entitlement (service-enforced → `403 entitlements.loyaltyBasic`).

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/program` | `portal.retention` **or** `portal.bookings` | Program config; if none saved yet returns defaults with `configured:false`, `isEnabled:false` |
| PUT | `/program` | `portal.retention` + **owner** | Create-or-replace the program (full body). Audited |
| GET | `/customers/{tenantCustomerId}/account` | `portal.retention` **or** `portal.bookings` | Points balance + lifetime counters (zeros if the customer has no account yet) |
| GET | `/customers/{tenantCustomerId}/transactions?limit=` | same | Newest-first ledger, `limit` 1–200 (default 50) |
| POST | `/customers/{tenantCustomerId}/adjust` | `portal.retention` + **owner or manager** + `Idempotency-Key` | Audited manual ± adjustment with required reason; program must exist. Idempotent replay; `409 retention.idempotency_conflict` if the key is reused with a different body |

```jsonc
// PUT /program body (full replace)
{
  "isEnabled": true,
  "currency": "NGN",
  "earnPoints": 1,            // points earned ...
  "earnPerAmount": 100,       // ... per this much spend (list currency, > 0)  → 1 pt / ₦100
  "redeemPoints": 100,        // this many points ...
  "redeemValue": 500,         // ... are worth this discount (> 0)              → 100 pts = ₦500 off
  "minRedeemPoints": 100,     // smallest redemption (multiple of redeemPoints enforced in slice C)
  "pointsExpiryMonths": 12    // null = never expire; 1–60
}
// response adds: configured, id?, createdAt?, updatedAt?

// POST /customers/{id}/adjust body
{ "points": -50, "reason": "Goodwill correction after double earn" }   // |points| 1–1,000,000; reason 1–500 chars
// response: { account: {...}, transaction: {...} }
```

New stable codes (mirrored in `@adeni/shared` i18n): `entitlements.loyaltyBasic` (403), `retention.loyalty_program_not_configured` (409), `retention.loyalty_insufficient_points` (409), `retention.loyalty_conflict` (409, concurrent balance change), reused: `retention.validation` (400), `retention.owner_required` (403), `retention.idempotency_key_required` (400), `retention.idempotency_conflict` (409). Missing/foreign customer → `404 not_found` (`Error.NotFound("Customer")`, same as 37.2-B). A manager hitting an owner-only route gets `retention.owner_required` (existing code/message, reused for the receptionist-on-adjust case too). Webhooks: none in A.

Later slices (sketch, not built): `…/loyalty/customers/{id}/redeem` (C), `/loyalty/tiers`, `/loyalty/rewards` (D), `/loyalty/referrals/program` (E), `/api/v1/me/retention/loyalty` (C).

---

## 7. Files to touch

| Layer | Files (slice A) |
|-------|-----------------|
| Domain | `Retention/Loyalty.cs` (program, account, transaction), `RetentionEnums.cs` (+`LoyaltyTransactionKind`), `RetentionErrorCodes.cs`, `Auditing/AuditActions.cs` |
| Application | `Retention/ILoyaltyServices.cs`, `LoyaltyModels.cs`, `IRetentionServices.cs` (guard), `Subscriptions/SubscriptionEntitlements.cs` |
| Infrastructure | `Retention/LoyaltyProgramService.cs`, `LoyaltyAccountService.cs`, `RetentionEntitlementGuard.cs`, `RetentionInternals.cs` (mapper), `RetentionServiceCollectionExtensions.cs`, `Persistence/RetentionModelConfiguration.cs`, `Subscriptions/JsonPlanEntitlementsMatrix.cs` |
| DB | `db/Adeni.Database/retention/Tables/loyalty_programs.sql`, `loyalty_accounts.sql`, `loyalty_transactions.sql` |
| Api | `Controllers/Retention/TenantLoyaltyController.cs`, `Errors/ApiErrorResponseMapper.cs` |
| Catalog | `docs/specs/adeni-subscription-pricing-v1/plan-entitlements.seed.json`, `packages/shared/src/data/plan-entitlements.json` |
| Shared / client | `packages/shared/src/{retention.ts,plan-entitlements.ts,schemas.ts,i18n/errors.ts,i18n/messages/overlay-*.ts}`, `packages/api-client/src/retention/retention-api.ts` |
| Portal | `app.routes.ts`, `core/portal-nav.ts`, `pages/retention/retention-loyalty.component.*` |
| Tests | `Retention/LoyaltyServiceTests.cs`, `Integration/LoyaltyIntegrationTests.cs`, existing fake `IRetentionEntitlementGuard`s (+method), `PlanEntitlementsParityTests.cs`, shared TS tests |

---

## 8. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Entities `ITenantEntity` + `HasQueryFilter`; services always filter `TenantId`; cross-tenant customer id → 404; composite `(TenantId, …)` FKs so a ledger row can never reference another tenant's account/customer; tenant routes need `X-Tenant-Id` match → 403 otherwise |
| **Auth / roles** | Reuses `portal.retention`. Program config = **owner only** (it defines money-equivalent value). Manual adjustment = **owner or manager** (parent §6.7 "manager + audit log"); receptionists can read. Mutations always authenticated |
| **Entitlements** | `IEntitlementsService` authoritative via guard; `loyaltyBasic` seeded. Do not gate authenticated portal flows on `PLAN_ENTITLEMENTS` seed — use profile `entitlements` |
| **Audit** | `retention.loyalty_program_saved`, `retention.loyalty_points_adjusted` via `IAuditLogWriter` (tenant audit, same shape as 37.1/37.2); metadata = tenant/customer ids and points delta — **no** reason text or customer PII |
| **PII** | Ledger holds ids + staff-entered reason (internal, not logged). Nothing new logged; responses carry no phone/email |
| **Concurrency** | `LoyaltyAccount.RowVersion`; concurrent adjust → `DbUpdateConcurrencyException` → `retention.loyalty_conflict`; first-account creation race handled by unique `(TenantId, TenantCustomerId)` + reload |
| **Idempotency** | Adjust requires `Idempotency-Key`; key hash + request fingerprint stored on the ledger row (unique per tenant) → replay returns the original result |
| **Cache** | None in A (optional `tenant:{id}:retention:loyalty-program` when Discover reads land in C) |
| **Payments / webhooks** | None in A (no money moves, no provider call). B/C must follow Murphy webhook rules via `PaymentCompleted` idempotency |
| **Scope guardrails** | No wallet/custody tables; points are program credits; no Finance/Booking/Notifications/Reporting changes |

---

## 9. Tests

- [x] Unit (`LoyaltyServiceTests`, in-memory EF): program default read (`configured:false`); upsert create then replace; validation (rates ≤ 0, min redeem, expiry range, currency); owner-only upsert; not-entitled → `entitlements.loyaltyBasic` on every method; adjust happy path (+/−, balance + ledger `BalanceAfter`, sequence increments); negative balance rejected; adjust without program → not configured; missing/blank reason → validation; idempotent replay (same key+body returns same txn, one ledger row); same key different body → conflict; missing key → required; manager allowed / receptionist denied; cross-tenant customer → not found; ledger isolation (tenant B cannot read tenant A's transactions); audit written
- [x] Integration (`LoyaltyIntegrationTests`): owner PUT/GET program; account/transactions/adjust over HTTP; unauthenticated → 401; cross-tenant `X-Tenant-Id` → 403; tenant A data invisible to B; Starter-style denial path via service (entitlement catalog has `loyaltyBasic` true for all tiers, so denial is covered at service level)
- [x] Parity: `loyaltyBasic` seed ⇄ `TenantEntitlements`; shared catalog ⇄ seed
- [x] Shared TS: Zod parse of program / account / transaction / adjust request; i18n parity for new error codes (EN + FR)

Run: `dotnet test Adeni.slnx -c Release`

---

## 10. Acceptance criteria (slice A)

- [x] Every tier (Starter/Professional/Business) sees the Loyalty page; owner can save a program, manager/receptionist can read it
- [x] An owner/manager can post an audited ± adjustment with a reason; ledger is append-only and balance never goes negative
- [x] Duplicate adjust requests with the same `Idempotency-Key` do not double-apply
- [x] No cross-tenant read or write of program, accounts or transactions
- [x] No Booking / Finance / Notifications / Reporting / Subscriptions-module / wallet code touched

---

## 11. Open questions

| Question | Proposed default | Decision |
|----------|------------------|----------|
| Tier evaluation metric (parent §14) | Lifetime **earned** points (manual adjustments excluded) | Default; confirm at slice D |
| Earn base: pre-tax list price vs amount paid | Amount paid for qualifying service lines, excluding tips/tax/package-covered lines | TBD slice B |
| Package/membership-covered services earn points? | No (nothing was paid) | TBD slice B |
| Rounding | Floor points; discount rounded to 2 dp | TBD slice B/C |
| Expiry model | FIFO lots vs "balance expires N months after last activity" | **Default: per-earn-lot expiry via `Expire` ledger rows**; decide at slice C |
| Redeem granularity | Multiples of `redeemPoints` ≥ `minRedeemPoints` | TBD slice C |
| Reward catalog entitlement | Share `loyaltyTiers` (parent §6.4 lists no separate key) | TBD slice D — flag to product |
| Referral reward type | Points to referrer + referee; tenant-configurable amounts | TBD slice E |
| Single currency per program | Yes (tenant currency); mismatched-currency bookings don't earn | Proposed |

---

## 12. Slice 37.3-A — implementation notes

**Decisions:**

| Topic | Decision |
|-------|----------|
| Authority | Program PUT owner-only; adjust owner/manager; all reads `portal.retention` or `portal.bookings`; every route needs `loyaltyBasic` |
| Program uniqueness | One row per tenant (unique `TenantId`); PUT is create-or-replace (full body) so the portal form maps 1:1 |
| Disabled program | `isEnabled:false` keeps config/ledger readable; manual adjust still allowed while a program exists (staff correction) — B/C will ignore disabled programs |
| Ledger | `loyalty_transactions` append-only; `Points` signed; `BalanceAfter` snapshot; `Sequence` per account; `KeyHash`+`Fingerprint` (nullable; set for manual adjusts) with filtered unique `(TenantId, KeyHash)` |
| Account lifecycle | Created lazily on first ledger write; `GET account` for a customer without one returns zeros without creating a row |
| Lifetime counters | `LifetimeEarned` / `LifetimeRedeemed` start at 0 and move only on `Earn` / `Redeem` (slices B/C); adjustments change only `PointsBalance` |
