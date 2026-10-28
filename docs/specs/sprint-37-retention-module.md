# Spec: Sprint 37 — Retention module (memberships, packages, loyalty)

> **Source inputs:** ChatGPT build package `Adeni_Membership_Loyalty_Cursor_Package_v1.zip` (2026-10-21), [product-direction.md](../product-direction.md), [product-strategy.md](../product-strategy.md), [architecture.md](../architecture.md), Sprint 35 tenant customers, Sprint 33 entitlements, Finance module boundaries.  
> **Do not implement until prerequisite module foundations are complete** (see banner below).

## IMPLEMENTATION ORDER

1. **Entitlements closeout** ([sprint-33-entitlements-closeout.md](./sprint-33-entitlements-closeout.md))
2. **Notifications Foundation — 38A** ([sprint-38-notifications-module.md](./sprint-38-notifications-module.md) §3.1)
3. **Reporting Foundation — 39A** ([sprint-39-reporting-module.md](./sprint-39-reporting-module.md) §3.1)
4. **Notifications Completion — 38B** (Retention transactional emails in 38B+)
5. **Reporting Completion — 39B**
6. **Retention staged delivery — 37.1 → 37.5** (this spec §3) — **committed product scope**, staged PRs only

Sprint number **37** is unchanged (repo references); **delivery order** is after 33-closeout and 38A/39A/38B/39B foundations. See [sprints.md](../sprints.md).

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 37 — Retention module |
| **Author** | Engineering (spec from product package) |
| **Status** | Draft |
| **Created** | 2026-10-21 |
| **Depends on** | 33-closeout (retention entitlement keys in seed), 38B (Retention → Notifications events), 39B (Retention → Reporting exports for 37.5) |

---

## 1. Goal (one sentence)

Deliver tenant-scoped **packages, loyalty, memberships, tiers, rewards, referrals, multi-location retention, and retention analytics** for beauty & wellness businesses in **Ottawa + Lagos**—increasing repeat bookings and GMV through Adeni commerce orchestration **without** wallet custody, a platform-wide points currency, or generic CRM depth.

---

## 2. Context

- **Product authority:** [product-direction.md](../product-direction.md) — discover → book → pay → review → rebook; portal includes **customers** and basic analytics; **non-custodial** payments; **consumer mobile: Flutter** (future); **web: Angular** (Discover, Portal, Admin).
- **Strategy:** Retention is a **thin bounded context**, not ERP — phased PRs, **full feature set committed** (§3).
- **Existing customer anchor:** [sprint-35-tenant-customer-records.md](./sprint-35-tenant-customer-records.md) — all retention state hangs off **`TenantCustomer`** (business roster), not a duplicate CRM.
- **Entitlements:** Extend seed per §6.4; enforce on mutating routes (`IEntitlementsService`).
- **Commerce:** [finance-module-boundaries.md](./finance-module-boundaries.md) — `PaymentCompleted` domain events; **Subscriptions** module = **Adeni SaaS** only (`PlatformSubscriptionMrr`).
- **Notifications / Reporting:** Integrate via **events and Application ports only** (§6.5, §6.6).

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

## 3. In scope — committed delivery phases

All phases below are **in scope for Retention V1**, not optional backlog. Ship as **37.1 → 37.5** in separate PRs; avoid one giant rules engine or single mega-PR.

### 37.1 Foundation

> **Packages slice (R1-A):** [sprint-37-1-packages-benefit-ledger.md](./sprint-37-1-packages-benefit-ledger.md) — session credits, benefit ledger, booking hooks. Loyalty items below ship in a follow-on 37.1 PR unless combined at review.

- [ ] **Retention bounded context** — Domain / Application / Infrastructure / Api (`retention.*` schema on SQL Server)
- [ ] **Prepaid packages** — catalog CRUD, one-time purchase via Finance orchestration, `CustomerPackage` + **benefit balances** (granted / used / reserved / remaining / expiresAt)
- [ ] **Basic loyalty** — per-tenant program config, **ledger** (`LoyaltyAccount` + `LoyaltyTransaction`), earn on qualifying **completed + paid** commerce events, redeem as fixed discount at checkout/booking
- [ ] **Benefit redemption coordinator** — reserve on booking create, release on cancel, finalize on appointment completed; **rowversion** on balance rows; idempotent handlers
- [ ] **Portal** — package catalog, loyalty program settings (basic), customer retention tab on tenant customer detail
- [ ] **Discover (Angular)** — view/buy package; view points/balance when logged in
- [ ] **API contracts** mobile-ready for future **Flutter** consumer app (no Expo)
- [ ] **Entitlements v3 (retention flags)** — extend `plan-entitlements.seed.json` (§6.4); backend enforcement on mutations
- [ ] **Tests** — tenant isolation, idempotent payment webhooks, concurrent redemption, entitlement denials

### 37.2 Memberships

- [ ] **Membership plans** + **customer memberships** (lifecycle: Pending → Active → PastDue → Frozen → CancelPending → Cancelled → Expired)
- [ ] **Recurring membership billing** via provider mandate/subscription references (Paystack/Stripe); activate/renew **only** on provider-confirmed `PaymentCompleted`; PastDue policy
- [ ] **Membership benefit balances** + rollover/freeze/cancellation rules (typed policies, not DSL)
- [ ] **`TenantMembershipMrr`** calculation (Retention-owned metric name — not Adeni SaaS MRR)

### 37.3 Rich loyalty

- [ ] **Configurable loyalty tiers** + tier evaluation (metric: lifetime earned vs rolling spend — §14)
- [ ] **Reward catalog** + redemption beyond simple point discount
- [ ] **Referrals** — program config, qualification on first paid completed service, fraud controls, single reward issuance (idempotent)

### 37.4 Multi-location

- [ ] Scope: SingleLocation / SelectedLocations / AllTenantLocations
- [ ] **Cross-location redemption** when `crossLocationRetention` entitled (Business)
- [ ] Centralized cross-location behavior; entitlement enforcement authoritative

### 37.5 Retention analytics

- [ ] **`IRetentionAnalyticsService`** — calculations owned by Retention:
  - **`TenantMembershipMrr`**, membership churn, renewal success
  - Benefit utilization, package redemption
  - Loyalty points issued / redeemed / expired / outstanding
  - Tier distribution, referral conversion, reward redemption
- [ ] **Reporting** consumes port for CSV/XLSX/PDF/download ([sprint-39-reporting-module.md](./sprint-39-reporting-module.md)); Reporting **does not** query `retention.*` tables directly
- [ ] Portal widgets gated by **`advancedRetentionAnalytics`** (§6.4)

---

## 4. Out of scope

- **Wallet / custody / stored value platform** — loyalty points are **tenant-scoped program credits**, not withdrawable cash; no Adeni-wide universal points currency
- **Generic CRM** — no campaigns, pipelines, email blasts, or segmentation builder beyond existing tenant customer list + retention fields
- **Email marketing / drip engine** — transactional Retention events → Notifications only
- **Giant rules engine** — typed policies (enums + columns), not DSL/evaluator
- **Payroll, inventory, full POS, retail SKU catalog**
- **Sponsored rewards / brand partnerships / AI retention assistant**
- **Merging Retention into Finance or Subscriptions**
- **Quote-request / non-beauty workflows** — retention hooks **scheduled_appointment** + paid service completion first
- **Expo / React Native** — not used; consumer UX = **Angular Discover now**, **Flutter later** via stable APIs

---

## 5. Adopt vs reject (ChatGPT package vs Adeni)

| Package proposal | Adeni decision |
|------------------|----------------|
| Single **Retention** module | **Adopt** |
| Ledger loyalty (not `Customer.LoyaltyPoints`) | **Adopt** |
| Benefit balances with reserve/finalize on booking | **Adopt** — Application ports to Booking + Finance |
| Non-custodial recurring membership billing | **Adopt** — Murphy webhook idempotency on renewals |
| No universal Adeni points | **Adopt** |
| Phased delivery | **Adopt** — 37.1–37.5 PRs; **full feature set committed** |
| API routes without `v1` | **Reject** — `/api/v1/` + stable error codes |
| **`TenantCustomer`** anchor | **Adopt** |
| Entitlements in main seed | **Adopt** — fold into `plan-entitlements.seed.json` |
| Portal-only UX | **Extend** — Discover Angular + API-ready for Flutter |
| Defer memberships/referrals to “Phase B maybe” | **Reject** — committed in 37.2–37.3 |

---

## 6. Module boundaries

### 6.1 Layer map

| Layer | Responsibility |
|-------|----------------|
| **Domain** `Adeni.Domain/Retention/` | Entities for packages, loyalty, memberships, tiers, referrals, rewards; domain events (§6.5) |
| **Application** `Adeni.Application/Retention/` | Ports: `IPackageCatalogService`, `ICustomerPackageService`, `ILoyaltyProgramService`, `ILoyaltyAccountService`, `IBenefitRedemptionService`, `IMembershipPlanService`, `IMembershipBillingCoordinator`, `IReferralService`, `IRewardCatalogService`, **`IRetentionAnalyticsService`**, `IRetentionEntitlementGuard` |
| **Infrastructure** `Adeni.Infrastructure/Retention/` | EF, services, `PaymentCompleted` / booking handlers, payment intent metadata |
| **Api** | Retention controllers under `/api/v1/tenant/retention/*`, consumer routes, `/api/v1/me/retention/*` |

### 6.2 Dependencies (allowed — no circular EF)

```
Retention → Tenancy, Identity, Booking (hooks), Finance (payment intents), Subscriptions (IEntitlementsService)
Booking → Retention (IBenefitRedemptionService only)
Finance → Retention (via PaymentCompleted / metadata — prefer event-driven)
Notifications → (none from Retention Infrastructure) — subscribes to Retention domain events
Reporting → Retention (IRetentionAnalyticsService only)
Retention → Notifications (domain events out only — never Resend/Twilio)
Retention → Reporting (IRetentionAnalyticsService implemented in Retention; Reporting calls in)
```

**Forbidden:** Retention querying Booking/Finance DbSets; Booking applying loyalty math internally; Retention calling Resend/Twilio; Reporting querying `retention.*` EF entities.

### 6.3 Tenant isolation

- All retention entities **`ITenantEntity`** + query filters on `AdeniDbContext`
- Business routes: JWT + **`X-Tenant-Id`** + `TenantAccessMiddleware`
- Customer routes: Auth0 **Customer** + resolved `TenantCustomer` for business context
- Cross-tenant → 403 + audit warning

### 6.4 Entitlements (backend authoritative; portal hide = UX only)

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

**Existing key (do not merge silently):**

| Key | Responsibility |
|-----|----------------|
| **`customerRetentionAnalytics`** | Customer-history depth, segmentation list, **existing** dashboard/customer widgets shipped pre-Retention |
| **`advancedRetentionAnalytics`** | **Retention module** analytics (§37.5): membership MRR/churn, loyalty/tier/referral/reward metrics, utilization |

`IEntitlementsService` on: membership plan CRUD, referrals, tiers, cross-location redeem, advanced analytics exports.

### 6.5 Retention → Notifications (domain events)

Retention raises; Notifications handles delivery (see [sprint-38-notifications-module.md](./sprint-38-notifications-module.md) §7):

`MembershipActivated`, `MembershipRenewed`, `MembershipPaymentFailed`, `MembershipExpiring`, `MembershipCancelled`, `BenefitExpiring`, `PackageExpiring`, `LoyaltyPointsEarned`, `LoyaltyPointsExpiring`, `LoyaltyTierChanged`, `RewardAvailable`, `ReferralQualified`, `ReferralRewardIssued`.

Transactional only — not marketing campaigns.

### 6.6 Retention → Reporting

| Owner | Responsibility |
|-------|----------------|
| **Retention** | `IRetentionAnalyticsService` — metric calculations, domain aggregates |
| **Reporting** | Date range, filters, CSV/XLSX/PDF, download API, formatting — via port only |

### 6.7 Concurrency & integrity (non-negotiable)

- Reserve benefit on booking create; release on cancel; finalize on completed appointment
- **`rowversion`** (or equivalent) on benefit balance rows
- Idempotent **`PaymentCompleted`** / webhook handling (Murphy)
- **Ledger-based loyalty** — immutable history; no client-side point mutation
- Audited manual adjustments (manager + audit log)

### 6.8 Cache (optional)

- `tenant:{id}:retention:package-catalog` — invalidate on package definition write

---

## 7. API contract (sketch)

All routes **`/api/v1/`**. Mutations require auth unless noted. Errors: RFC 7807 + dotted `code`.

### 7.1 Tenant — packages / loyalty / customers

(See prior routes — unchanged paths: `/api/v1/tenant/retention/packages`, `/loyalty/program`, `/customers/{tenantCustomerId}`, etc.)

### 7.2 Consumer

- `GET /api/v1/businesses/{slug}/retention/packages` — anonymous discover
- `POST .../purchase` — Customer JWT → Finance payment intent
- `GET /api/v1/me/retention/balances`, `POST .../loyalty/redeem`

### 7.3 Memberships (37.2)

- `/api/v1/tenant/retention/memberships/plans`, consumer subscribe routes
- Renewals: Finance webhooks + Retention handler (signature + idempotency)

### 7.4 Booking integration (Application)

| Hook | When |
|------|------|
| `TryReserveBenefitsAsync` | Booking created with package/membership benefit |
| `ReleaseReservationsAsync` | Cancelled |
| `FinalizeRedemptionsAsync` | Completed |
| `EvaluateLoyaltyEarnAsync` | PaymentCompleted + completed service |

---

## 8. UX surfaces

| Surface | Delivery |
|---------|----------|
| **Portal (Angular)** | Packages, loyalty, memberships, tiers, referrals, reports (gated) |
| **Discover (Angular)** | Profile packages, purchase, balances |
| **Admin** | Read-only inspect + audited corrections (37.4+) |
| **Flutter (future)** | Consume same `/api/v1/me/retention/*` — no Expo tasks in this sprint |

Use **`@adeni/ui`** for portal list/toolbar ([shared-data-grid.md](./shared-data-grid.md)).

---

## 9. Data model (conceptual)

Schema **`retention.*`**: package definitions/balances, loyalty program/accounts/transactions, membership plans/memberships, tiers, referrals, rewards.

**Keys:** `TenantId`, `TenantCustomerId`, optional `LocationId`, `BookingId`, `PaymentIntentId`, **`rowversion`** on balances.

**Do not duplicate** platform `Customer` or `TenantCustomer` — FK only.

---

## 10. Files to touch (implementation checklist)

| Layer | Files |
|-------|-------|
| Domain | `Adeni.Domain/Retention/**`, events |
| Application | `I*.cs`, DTOs, `RetentionErrorCodes`, **`IRetentionAnalyticsService`** |
| Infrastructure | EF, handlers, `AddRetentionModule()` |
| Api | `Controllers/Retention/**` |
| Finance / Booking | Metadata + `IBenefitRedemptionService` hooks |
| Tests | Unit + integration + architecture (no cross-module EF) |
| packages/shared | Zod, entitlements, i18n |
| apps/portal, apps/discover | Retention UX |

---

## 11. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All tables tenant-scoped |
| **Auth / roles** | Optional `portal.retention`; manual adjustments audited |
| **Webhooks** | Membership renewals — Murphy |
| **Notifications** | §6.5 events (38B+) |
| **Reporting** | §6.6 port (37.5 / 39B) |

---

## 12. Tests

- [ ] Package purchase activates after `PaymentCompleted` only
- [ ] Duplicate webhook does not double-grant
- [ ] Cancel releases reservation; complete finalizes
- [ ] Loyalty earn on qualifying path only
- [ ] Redeem fails when insufficient; **concurrency** on balances
- [ ] Cross-tenant → 403
- [ ] Starter cannot create membership/referral/tier features
- [ ] Cross-location redeem blocked without `crossLocationRetention`

Run: `dotnet test Adeni.slnx -c Release`

---

## 13. Acceptance criteria

- [ ] **Starter:** packages + basic loyalty only
- [ ] **Professional:** memberships, tiers, referrals, **`advancedRetentionAnalytics`**
- [ ] **Business:** + **`crossLocationRetention`**
- [ ] Customer buys package on Discover; credits apply with reserve/finalize semantics
- [ ] Loyalty ledger auditable; membership billing provider-ref only
- [ ] Clean module ports; no Retention Infrastructure in Booking/Finance/Notifications/Reporting

---

## 14. Open questions

| Question | Proposed default | Decision |
|----------|------------------|----------|
| Fold retention entitlements into main seed | Single `plan-entitlements.seed.json` | TBD |
| `GET tenant/customers/{id}?include=retention` | Prefer `include=retention` | TBD |
| Tier evaluation metric | Lifetime **earned** points | TBD |
| Membership renewal provider | Paystack vs Stripe per market registry | TBD |
| Consolidate `customerRetentionAnalytics` + `advancedRetentionAnalytics` | **Flag for product** — specs keep separate | TBD |
| Platform fee on membership GMV | Out of scope until Finance settlement Sprint 34+ | TBD |

---

## 15. Internal implementation sequence (37.1 → 37.5)

1. Spec approval + entitlements seed extension
2. **37.1** — domain/EF, packages, loyalty ledger, redemption, portal/discover MVP
3. **37.2** — memberships + billing coordinator
4. **37.3** — tiers, rewards, referrals
5. **37.4** — multi-location
6. **37.5** — `IRetentionAnalyticsService` + Reporting export wiring

---

## 16. References

- [AGENTS.md](../../AGENTS.md) — Murphy, `/api/v1/`, no custody
- [finance.md](../finance.md), [reporting-module-boundaries.md](./reporting-module-boundaries.md)
- [sprint-38-notifications-module.md](./sprint-38-notifications-module.md), [sprint-39-reporting-module.md](./sprint-39-reporting-module.md)
