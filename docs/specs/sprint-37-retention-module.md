# Spec: Sprint 37 — Retention module (memberships, packages, loyalty)

> **Source inputs:** ChatGPT build package `Adeni_Membership_Loyalty_Cursor_Package_v1.zip` (2026-10-21), [product-direction.md](../product-direction.md), [product-strategy.md](../product-strategy.md), [architecture.md](../architecture.md), Sprint 35 tenant customers, Sprint 33 entitlements, Finance module boundaries.  
> **Do not implement until this spec is reviewed and Sprint 37 is added to [sprints.md](../sprints.md).**

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 37 — Retention module |
| **Author** | Engineering (spec from product package) |
| **Status** | Draft |
| **Created** | 2026-10-21 |

---

## 1. Goal (one sentence)

Give beauty & wellness businesses in **Ottawa + Lagos** tenant-scoped **packages, loyalty, and (tier-gated) memberships** that increase repeat bookings and GMV through Adeni commerce orchestration—**without** wallet custody, a platform-wide points currency, or generic CRM depth.

---

## 2. Context

- **Product authority:** [product-direction.md](../product-direction.md) — discover → book → pay → review → rebook; portal includes **customers** and basic analytics; **non-custodial** payments.
- **Strategy:** [product-strategy.md](../product-strategy.md) lists loyalty as backlog; [roadmap-workflows-payments-queue.md](../roadmap-workflows-payments-queue.md) defers full memberships vs Zenoti ERP depth. **This spec** implements retention as a **thin bounded context**, not ERP.
- **Existing customer anchor:** [sprint-35-tenant-customer-records.md](./sprint-35-tenant-customer-records.md) — all retention state hangs off **`TenantCustomer`** (business roster), not a duplicate CRM.
- **Entitlements today:** [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md) — `customerRetentionAnalytics` gates history depth and dashboard widgets; **memberships/packages/loyalty flags are not in seed yet**.
- **Commerce:** [finance-module-boundaries.md](./finance-module-boundaries.md) — `PaymentCompleted` domain events; **Subscriptions** module remains **Adeni SaaS MRR only**.
- **Module roadmap:** Add **Retention** as module #13 in [roadmap-modules.md](../roadmap-modules.md) when approved (not in this PR unless requested).

### What exists today (grep / codebase)

| Area | State |
|------|--------|
| Tenant customer roster | ✅ `ITenantCustomerService`, `/api/v1/tenant/customers` |
| Retention products | ❌ No membership/package/loyalty entities or APIs |
| Loyalty ledger | ❌ |
| `customerRetentionAnalytics` | ✅ Entitlement + partial dashboard / customer history limits |
| Customer segmentation list filter | ✅ Gated by `customerRetentionAnalytics` |
| Payment → booking side effects | ✅ `PaymentCompleted`, `BookingConfirmed` handlers (Finance, Notifications) |
| Package naming in seed data | Cosmetic only (`Day Spa Package` service title) |

---

## 3. In scope

### Phase A — MVP (Sprint 37 target)

- [ ] **Retention bounded context** in Domain / Application / Infrastructure / Api (schema `retention.*` on SQL Server).
- [ ] **Prepaid packages** — catalog CRUD, one-time purchase via existing Finance orchestration, `CustomerPackage` + **benefit balances** (granted / used / reserved / remaining / expiresAt).
- [ ] **Basic loyalty** — per-tenant program config, **ledger** (`LoyaltyAccount` + `LoyaltyTransaction`), earn on qualifying **completed + paid** commerce events, redeem as fixed discount at checkout/booking (minimal rules, no giant rules engine).
- [ ] **Benefit redemption coordinator** — reserve on booking create, release on cancel, finalize on appointment completed; idempotent handlers.
- [ ] **Portal** — package catalog, loyalty program settings (basic), customer retention tab on tenant customer detail (balances + points history).
- [ ] **Discover / consumer web** — view/buy package; view points/balance when logged in (membership purchase UI stub → Phase B).
- [ ] **Entitlements v3 (retention flags)** — extend `plan-entitlements.seed.json` + `SubscriptionEntitlements` per package `retention-entitlements.seed.json` (see §7); API enforcement on mutating routes.
- [ ] **Tests** — tenant isolation, idempotent payment webhooks, concurrent redemption, entitlement denials.

### Phase B — Professional tier depth

- [ ] **Membership plans** + **customer memberships** (lifecycle: Pending → Active → PastDue → Frozen → CancelPending → Cancelled → Expired).
- [ ] **Recurring membership billing** via provider mandate/subscription references (Paystack/Stripe); activate/renew **only** on provider-confirmed `PaymentCompleted`; PastDue policy.
- [ ] **Tiered loyalty** — configurable tiers, evaluation metric (explicit: lifetime earned vs rolling spend — decision in §10), tier change events.
- [ ] **Referrals** — program config, qualification on first paid completed service, fraud controls, single reward issuance.
- [ ] **Reward catalog** — definitions + redemption beyond simple point discount.
- [ ] **Retention analytics widgets** — wire to existing `customerRetentionAnalytics` + new `advancedRetentionAnalytics` where distinct.

### Phase C — Business tier

- [ ] **Cross-location** scope (SingleLocation / SelectedLocations / AllTenantLocations) and redemption when `crossLocationRetention` entitled.
- [ ] **Consolidated retention reporting** — aligns with `multiLocationReporting` where overlapping.
- [ ] **Admin** — read-only inspect + audited manual adjustments (loyalty/benefit corrections).

---

## 4. Out of scope

Explicit guardrails (V1 + Adeni patterns):

- **Wallet / custody / stored value platform** — loyalty points are **tenant-scoped program credits**, not withdrawable cash; no Adeni-wide universal points currency.
- **Generic CRM** — no campaigns, pipelines, email blasts, or segmentation builder beyond existing tenant customer list + retention fields.
- **Giant rules engine** — configurable fields per product doc, but implement **typed policies** (enums + columns), not DSL/evaluator.
- **Payroll, inventory, full POS, retail SKU catalog**
- **Sponsored rewards / brand partnerships / AI retention assistant** — later monetization ideas from package doc.
- **Merging Retention into Finance or Subscriptions** — separate module; shares events and payment intents only.
- **Quote-request / non-beauty workflows** — retention hooks **scheduled_appointment** + paid service completion first.
- **Flutter** — consumer primary mobile is **Expo** per current sprint tracker; retention consumer UX targets **Discover web + Expo**, portal **Angular**.

---

## 5. Adopt vs reject (ChatGPT package vs Adeni)

| Package proposal | Adeni decision |
|------------------|----------------|
| Single **Retention** module (memberships, packages, loyalty, referrals, rewards, analytics) | **Adopt** — matches modular monolith; mirror Finance/Reporting boundary docs. |
| Ledger loyalty (not `Customer.LoyaltyPoints`) | **Adopt** |
| Benefit balances with reserve/finalize on booking | **Adopt** — integrate via Application ports to Booking + Finance, not EF cross-module DbSets. |
| Non-custodial recurring membership billing | **Adopt** — provider refs only; Murphy webhook idempotency on renewals. |
| No universal Adeni points | **Adopt** — aligns with product-direction commerce. |
| “Package wins over older ‘later’ notes” | **Partial** — build **phased** MVP; [product-direction.md](../product-direction.md) still wins on **scope** (beauty, Ottawa+Lagos, no CRM). |
| API routes `/api/memberships/...` without `v1` | **Reject** — all routes **`/api/v1/`** + stable error codes. |
| Separate “CRM/customer domain” from retention | **Adopt concept** — **`TenantCustomer`** owns identity link; Retention owns program state keyed by `TenantCustomerId`. |
| Entitlement split in `retention-entitlements.seed.json` | **Adopt with merge** — fold keys into main `plan-entitlements.seed.json` in same PR as enforcement (avoid two sources of truth). |
| Portal-only UX list | **Extend** — Discover + Expo customer surfaces per product-direction journey. |
| SQL table list as-is | **Adapt** — map to `retention.*` schema; reuse `TenantId`, `LocationId`, `TenantCustomerId`, `BookingId`, payment intent ids from existing modules. |
| Domain events list (MembershipPurchased, …) | **Adopt** — handlers in Retention + react to existing `PaymentCompleted` / booking completion events. |
| Immediate “build rich tiers now” | **Defer tiers/referrals/memberships** to Phase B unless product explicitly pulls forward; Phase A proves package + basic loyalty + redemption loop. |

---

## 6. Module boundaries

### 6.1 Layer map

| Layer | Responsibility |
|-------|----------------|
| **Domain** `Adeni.Domain/Retention/` | Entities: `PackageDefinition`, `CustomerPackage`, `PackageBenefitBalance`, `LoyaltyProgram`, `LoyaltyAccount`, `LoyaltyTransaction`, (+ Phase B: membership, tier, referral, reward entities). Value objects: benefit quantities, period windows, status enums. Domain events listed in package doc §11. |
| **Application** `Adeni.Application/Retention/` | Ports: `IPackageCatalogService`, `ICustomerPackageService`, `ILoyaltyProgramService`, `ILoyaltyAccountService`, `IBenefitRedemptionService`, `IRetentionEntitlementGuard`, (+ Phase B: `IMembershipPlanService`, `IMembershipBillingCoordinator`, `IReferralService`, `IRewardCatalogService`, `IRetentionAnalyticsService`). DTOs + commands. |
| **Infrastructure** `Adeni.Infrastructure/Retention/` | EF configurations, services, `IDomainEventHandler<PaymentCompleted>` for purchase activation, `IDomainEventHandler<Booking*>` for reserve/finalize, payment intent metadata for `retention_product_type`. |
| **Api** | `RetentionPackagesController`, `RetentionLoyaltyController`, `TenantRetentionController` (tenant-scoped customer views), (+ Phase B membership/referral controllers). |
| **Reporting** | Retention **metrics queries** either owned by Retention (`IRetentionAnalyticsService`) or thin read delegates consumed by `ITenantDashboardService` — **do not** embed retention SQL in Dashboard without a port. |

### 6.2 Dependencies (allowed)

```
Retention → Tenancy (tenant/location validation), Identity (customer id), Booking (reserve/finalize hooks), Finance (payment intents), Subscriptions (IEntitlementsService)
Booking → Retention (IBenefitRedemptionService only)
Finance → Retention (optional: product line metadata parser — prefer Retention handles via PaymentCompleted)
```

**Forbidden:** Retention querying `Booking` DbSets directly; Booking applying loyalty math internally.

### 6.3 Tenant isolation

- All retention entities implement **`ITenantEntity`** + global query filters on `AdeniDbContext`.
- Routes under `/api/v1/tenant/retention/*` require business JWT + **`X-Tenant-Id`** + `TenantAccessMiddleware`.
- Customer routes `/api/v1/me/retention/*` scoped by Auth0 **`Customer`** + resolved `TenantCustomer` for the business context (slug or tenant id in path).
- Cross-tenant read/write → 403 + audit warning (existing pattern).

### 6.4 Entitlements hooks

Extend seed (names illustrative — finalize in `packages/shared`):

| Seed key | Starter | Professional | Business |
|----------|---------|--------------|----------|
| `packages` | true | true | true |
| `loyaltyBasic` | true | true | true |
| `loyaltyTiers` | false | true | true |
| `memberships` | false | true | true |
| `recurringMembershipBilling` | false | true | true |
| `referrals` | false | true | true |
| `crossLocationRetention` | false | false | true |
| `advancedRetentionAnalytics` | false | true | true |

- Map `advancedRetentionAnalytics` to portal report pages; keep **`customerRetentionAnalytics`** for existing dashboard/customer history gates until consolidated.
- **`IEntitlementsService`** checks on: create membership plan, enable referrals, cross-location redemption, tier CRUD, analytics export.
- Portal: hide nav entries when flag false (Sprint 33 pattern — no ops-page upgrade nags).

### 6.5 Cache (optional MVP)

- `tenant:{id}:retention:package-catalog` — invalidate on package definition write.
- Public discover package list may use same key with short TTL.

---

## 7. API contract (sketch)

All routes prefixed **`/api/v1/`**. Mutations require auth unless noted. Errors: RFC 7807 + dotted `code` (e.g. `retention.package_not_found`, `entitlements.memberships_not_entitled`).

### 7.1 Tenant — package catalog (business)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/tenant/retention/packages` | Business + tenant | List package definitions |
| POST | `/api/v1/tenant/retention/packages` | Business + tenant + entitlement | Create package |
| GET | `/api/v1/tenant/retention/packages/{id}` | Business + tenant | Detail |
| PUT | `/api/v1/tenant/retention/packages/{id}` | Business + tenant | Update |
| POST | `/api/v1/tenant/retention/packages/{id}/archive` | Business + tenant | Soft deactivate |

### 7.2 Tenant — loyalty program (business)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/tenant/retention/loyalty/program` | Business + tenant | Program config |
| PUT | `/api/v1/tenant/retention/loyalty/program` | Business + tenant | Update (basic earn/redeem rules) |
| GET | `/api/v1/tenant/retention/loyalty/tiers` | Business + tenant | Phase B |
| POST | `/api/v1/tenant/retention/loyalty/tiers` | Business + tenant + `loyaltyTiers` | Phase B |
| POST | `/api/v1/tenant/retention/customers/{tenantCustomerId}/loyalty/adjustment` | Business + permission | Manual adjustment (audited) |

### 7.3 Tenant — customer retention profile

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/tenant/retention/customers/{tenantCustomerId}` | Business + tenant | Packages, balances, loyalty account summary |
| GET | `/api/v1/tenant/retention/customers/{tenantCustomerId}/loyalty/transactions` | Business + tenant | Ledger page |

Extend existing **`GET /api/v1/tenant/customers/{id}`** optionally with `?include=retention` to avoid duplicate round-trips (decision §10).

### 7.4 Consumer — purchase & balances

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/businesses/{slug}/retention/packages` | Anonymous | Public catalog for discover profile |
| POST | `/api/v1/businesses/{slug}/retention/packages/{id}/purchase` | Customer JWT | Creates Finance payment intent; on success → `CustomerPackage` |
| GET | `/api/v1/me/retention/balances` | Customer JWT | Cross-business list optional later; MVP per-tenant via query `tenantId` |
| POST | `/api/v1/me/retention/loyalty/redeem` | Customer JWT | Apply redemption to booking/checkout session |

### 7.5 Phase B — memberships

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET/POST | `/api/v1/tenant/retention/memberships/plans` | Business + `memberships` | Plan CRUD |
| POST | `/api/v1/businesses/{slug}/retention/memberships/plans/{id}/subscribe` | Customer JWT | Provider setup + first charge |
| POST | `/api/v1/finance/webhooks/...` | Provider HMAC | Existing; Retention handler for membership renewal metadata |

### 7.6 Booking integration (internal Application)

| Hook | When |
|------|------|
| `TryReserveBenefitsAsync(bookingId, …)` | Booking created with package/membership benefit selected |
| `ReleaseReservationsAsync(bookingId)` | Booking cancelled |
| `FinalizeRedemptionsAsync(bookingId)` | Appointment completed |
| `EvaluateLoyaltyEarnAsync(paymentIntentId \| bookingId)` | PaymentCompleted + completed service |

**Request shape (illustrative — Zod in `@adeni/shared`):**

```json
{
  "packageDefinitionId": "uuid",
  "tenantCustomerId": "uuid",
  "locationId": "uuid"
}
```

---

## 8. UX surfaces

| Surface | Phase A | Phase B+ |
|---------|---------|----------|
| **Portal (Angular)** | Retention nav: Packages, Loyalty; customer detail retention panel; sell package from front desk link | Membership plans, Members, Tiers, Referrals, Reports |
| **Discover (web)** | Business profile: Packages card; checkout for package | Membership subscribe CTA, referral link |
| **Expo mobile** | My balances / packages for logged-in customer | Same + redeem at book time |
| **Admin** | — | Inspect tenant retention config; audited corrections |
| **Kiosk / queue** | Optional: attach walk-in `TenantCustomer` package balance at payment (stretch) | — |

Use **`@adeni/ui`** for shared portal list/toolbar when building catalog pages ([shared-data-grid.md](./shared-data-grid.md)).

---

## 9. Data model (conceptual)

Schema: **`retention.*`** (new migration sprint).

| Group | Tables (from package, adapted) |
|-------|--------------------------------|
| Packages | `package_definitions`, `package_benefits`, `customer_packages`, `package_benefit_balances` |
| Loyalty | `loyalty_programs`, `loyalty_accounts`, `loyalty_transactions`, (+ `loyalty_tiers`, `reward_definitions`, `reward_redemptions`) |
| Memberships (B) | `membership_plans`, `membership_plan_benefits`, `customer_memberships`, `membership_benefit_balances` |
| Referrals (B) | `referral_programs`, `referrals` |

**Keys:** `TenantId`, `TenantCustomerId`, optional `LocationId`, `BookingId`, `PaymentIntentId` / provider reference, `rowversion` on balance rows.

**Do not duplicate** platform `Customer` or `TenantCustomer` — FK only.

---

## 10. Files to touch (implementation checklist)

| Layer | Files (expected) |
|-------|------------------|
| Domain | `Adeni.Domain/Retention/**`, events under `Domain/Retention/Events/` |
| Application | `Adeni.Application/Retention/I*.cs`, DTOs, `RetentionErrorCodes` |
| Infrastructure | `Adeni.Infrastructure/Retention/**`, EF configs, event handlers, `AddRetentionModule()` |
| Api | `Controllers/Retention/**`, register in `ServiceCollectionExtensions` |
| Finance | Payment intent **metadata** convention; optional `retention` line item type |
| Booking | Call `IBenefitRedemptionService` at create/cancel/complete |
| Tests | Unit (loyalty math, rollover policy), integration (HTTP + tenant denial + webhook idempotency) |
| packages/shared | Zod schemas, entitlements keys, i18n error keys |
| packages/api-client | Typed methods per route group |
| apps/portal | Retention routes + customer detail |
| apps/discover or web | Public package purchase UX |

---

## 11. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All retention tables tenant-scoped; customer APIs verify `TenantCustomer` belongs to tenant. |
| **Auth / roles** | Business: existing portal permissions + new `portal.retention` (optional). Manual loyalty adjustment: manager role + audit. |
| **Audit** | Admin corrections; manual loyalty adjustments → `admin.audit_logs` or tenant audit table. |
| **PII** | No referral codes tied to raw phone in logs; mask in analytics exports. |
| **Webhooks** | Membership renewals: signature + idempotency (Murphy); same as Finance payment webhooks. |
| **Observability** | Correlate retention activation with `X-Correlation-Id`; App Insights events (Sprint 20). |
| **Notifications** | Phase B: PastDue membership, referral qualified — via Notifications module events. |

---

## 12. Tests

- [ ] Package purchase activates balances only after `PaymentCompleted`
- [ ] Duplicate webhook does not double-grant benefits or points
- [ ] Booking cancel releases reservation; complete finalizes redemption
- [ ] Loyalty earn only on qualifying paid+completed path
- [ ] Redeem fails when balance insufficient; concurrency safe
- [ ] Cross-tenant API → 403
- [ ] Entitlement: Starter cannot create membership plan or referral program
- [ ] Cross-location redeem blocked when `crossLocationRetention` false

Run: `dotnet test Adeni.slnx -c Release`

---

## 13. Acceptance criteria

- [ ] Business on **Starter** can sell **packages** and run **basic loyalty**; cannot enable memberships/referrals/tiers.
- [ ] **Professional** tenant can configure memberships (B), tiers, referrals, and see retention analytics pages.
- [ ] Customer can buy a package on Discover and see remaining credits on logged-in profile.
- [ ] Package credit applies to a booking without consuming on create-only; cancel restores reservation.
- [ ] Loyalty ledger is auditable; no client-side point mutation.
- [ ] No raw card data; membership billing uses provider references only.
- [ ] Module registered with clean ports; Booking/Finance do not import Retention Infrastructure.

---

## 14. Open questions

| Question | Proposed default | Decision |
|----------|------------------|----------|
| Phase A vs B split for sprint commitment | Phase A in Sprint 37; B as 37b or 38 | TBD |
| Fold retention entitlements into main seed vs sidecar JSON | Single `plan-entitlements.seed.json` | TBD |
| Extend `GET tenant/customers/{id}` vs separate retention aggregate | `include=retention` on existing detail | TBD |
| Default tier evaluation metric | Lifetime **earned** points (package doc option) | TBD |
| Freeze/rollover/cancellation defaults | Per package doc open decisions — business-configurable with sane defaults | TBD |
| Membership renewal provider | Paystack subscription vs Stripe billing per market registry | TBD |
| Priority booking benefit | Flag on tier/membership → booking module priority field (if any) | TBD |
| Earn on queue walk-in payments without booking | Phase B — require linked completed appointment | TBD |
| Platform fee on membership GMV | Out of scope until Finance settlement Sprint 34+ | TBD |

---

## 15. Implementation sequence (recommended)

1. **Spec approval + entitlements seed extension**
2. **Domain + EF** — packages + loyalty ledger + balances
3. **Finance purchase flow** — intent metadata → `PaymentCompleted` handler
4. **BenefitRedemptionService** + Booking hooks
5. **Portal package/loyalty admin + customer retention panel**
6. **Discover package purchase**
7. **Phase B** — memberships billing coordinator, tiers, referrals, analytics
8. **Phase C** — cross-location + admin tools

---

## 16. References

- External package files: `00_READ_ME_FIRST.md` … `16_OPEN_DECISIONS.md`, `retention-entitlements.seed.json`
- [AGENTS.md](../../AGENTS.md) — Murphy webhooks, API `/api/v1/`, no custody
- [finance.md](../finance.md) — payment intents
- [reporting-module-boundaries.md](./reporting-module-boundaries.md) — dashboard widget ownership
