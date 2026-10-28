# Spec: Sprint 37.1 — Prepaid packages & benefit ledger (R1-A)

> **Parent:** [sprint-37-retention-module.md](./sprint-37-retention-module.md) §37.1 Foundation (packages slice only).  
> **Product decisions:** [r1a-product-decisions.md](./r1a-product-decisions.md) (approved 2026-10-28).  
> **Do not implement** until this spec is reviewed and prerequisite module order in [sprints.md](../sprints.md) is satisfied. **This document authorizes design only** — no code, DACPAC, or API changes from the spec PR itself.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 37.1 — Packages & benefit ledger (R1-A Phase 1) |
| **Author** | Engineering |
| **Status** | **Implemented** (backend + DACPAC + booking hooks + portal + Discover storefront/purchase/my-packages/`appliedBenefits`). Live online settlement gated on Sprint 34 (stub-only until then); SQL Server concurrency tests authored but pending a run on an isolated SQL Server |
| **Created** | 2026-10-28 |

---

## 1. Goal (one sentence)

Let beauty & wellness businesses sell **prepaid session/service credit packages**, grant auditable **benefit balances** to roster customers, and **redeem credits at booking** (reserve → finalize) with tenant isolation and non-custodial commerce — **without** wallet custody, gift-card monetary ledgers, memberships, or loyalty in this slice.

---

## 2. Context

- **Product authority:** [product-direction.md](../product-direction.md) — discover → book → pay → rebook; **non-custodial** payments.
- **Strategy:** [product-strategy.md](../product-strategy.md) — retention is a thin bounded context; no stored-value platform.
- **Parent module:** [sprint-37-retention-module.md](./sprint-37-retention-module.md) — 37.1 originally included basic loyalty; **this spec narrows 37.1 delivery to packages + benefit ledger only**. Loyalty (37.1 remainder), memberships (37.2), tiers/referrals (37.3), multi-location entitlement (37.4), analytics (37.5) stay in the parent spec.
- **Customer anchor:** [sprint-35-tenant-customer-records.md](./sprint-35-tenant-customer-records.md) — all `CustomerPackage` rows FK **`TenantCustomer`**, never duplicate CRM.
- **Finance:** [finance-module-boundaries.md](./finance-module-boundaries.md) — online package purchase flows through **`PaymentCompleted`** / payment intents; no balance tables in Finance.
- **Gift cards (F3):** [f3-gift-cards-pay-links-plan.md](./f3-gift-cards-pay-links-plan.md) — **monetary** gift value, Pay Links, separate schema under `payments.*`. Packages are **service/session credits**, not gift tender and not redeemable as cash.
- **F1 customer profile:** [f1-customer-profile-activity-foundation.md](./f1-customer-profile-activity-foundation.md) — today Retention sources are `notImplemented`; 37.1 adds an Application port for **package-holder summary** (additive, permission-gated).
- **Settlement:** [sprint-34-payment-settlement.md](./sprint-34-payment-settlement.md) — **online** Discover/consumer package checkout remains gated on tenant merchant settlement readiness (same class of guard as F3 live sales). **In-person till/transfer sales are allowed before live online settlement** per [r1a-product-decisions.md](./r1a-product-decisions.md).

### Prerequisites (implementation order)

1. Entitlements closeout (✅ [sprint-33-entitlements-closeout.md](./sprint-33-entitlements-closeout.md))
2. Notifications 38A / Reporting 39A foundations (for later 38B package-expiry email — **not** blocking 37.1 core ledger)
3. **This spec approved**
4. **Phase 0 (separate PR, optional before or with 37.1 code):** add `packages` entitlement key to `plan-entitlements.seed.json` per §9 — **not part of spec-only PR**
5. **37.1 implementation PR(s):** schema + module + portal/discover MVP

---

## 3. In scope

- [x] **Retention bounded context (packages slice)** — `retention.*` schema on SQL Server; EF in Infrastructure only
- [x] **Package catalog** — CRUD for `PackageDefinition` + nested **benefit lines** (which catalog services / quantities are granted)
- [x] **Sell package to customer** — grant `CustomerPackage` + `BenefitBalance` rows:
  - **In-person:** till / bank transfer / external payment recorded in Portal (no provider checkout) — **allowed before live online settlement**
  - **Online (Discover / consumer):** Finance payment intent → activate grants **only** on idempotent `PaymentCompleted` (Murphy); subject to Sprint **34** settlement + stub/live provider guards
- [x] **Benefit ledger** — immutable `BenefitTransaction` history; mutable `BenefitBalance` with **granted / used / reserved / remaining** (+ optional duration counters if spec’d on benefit line)
- [x] **`IBenefitRedemptionService`** — Booking hooks: reserve on create, release on cancel, finalize on completed appointment (same SQL transaction as booking create for reserve)
- [x] **Redemption & sell scope** — enums and validation per [r1a-product-decisions.md](./r1a-product-decisions.md) §6 (booking location vs package scope; customer home location unchanged)
- [x] **Expiry & forfeiture** — default **12 months** (tenant-configurable months); unused credits forfeit at expiry; **owner void** with audit
- [x] **Portal RBAC** — new **`portal.retention`** permission; package catalog, sell, customer package list, void (owner)
- [x] **Discover (Angular)** — public catalog; logged-in **my packages / balances**; online purchase when settlement allows
- [x] **Entitlement enforcement** — `IEntitlementsService` **`packages`** on mutating tenant/consumer package routes (see §9)
- [x] **API contracts** under `/api/v1/tenant/retention/...` and consumer routes; Zod in `@adeni/shared`, client in `@adeni/api-client`
- [x] **Tests** — tenant isolation, idempotent grant from duplicate webhooks, concurrent reserve/redemption, scope denial, entitlement denial

---

## 4. Out of scope (37.1 — explicit)

| Excluded | Notes |
|----------|--------|
| **Memberships & recurring billing** | Sprint **37.2** |
| **Loyalty program, points ledger, tiers, referrals, rewards** | Sprint **37.1 remainder / 37.3** |
| **Wallet / stored monetary balance / withdrawable value** | Product strategy; credits are **service counts**, not currency |
| **Gift cards & Pay Links (F3)** | Separate monetary ledger; do not apply gift tender to package purchase in 37.1; do not merge tables |
| **Using package credits to pay arbitrary Pay Links or booking balance in cash terms** | Redemption applies **included services** to booking line items only |
| **Retention analytics exports** | **37.5** / Reporting port |
| **Package-expiry transactional email** | **38B** (`PackageExpiring` event) — optional follow-up |
| **Cross-location entitlement `crossLocationRetention`** | **37.4** — 37.1 implements **product-level** redemption scope enums; Business-tier cross-location *entitlement* flag stays deferred |
| **Admin portal write paths** | Read-only inspect optional; corrections via owner void + audited grant |
| **Flutter consumer app** | API-ready; UI deferred |
| **Quote-request / non–scheduled-appointment** workflows | Hooks **scheduled_appointment** first |
| **DACPAC / migrations / controllers / UI in this spec PR** | Spec only |

---

## 5. Module boundaries

### 5.1 Layers

| Layer | Responsibility (37.1) |
|-------|------------------------|
| **Domain** `Adeni.Domain/Retention/` | `PackageDefinition`, `PackageBenefit`, `CustomerPackage`, `BenefitBalance`, `BenefitTransaction`, `BenefitOperation`; enums; domain events: `CustomerPackageActivated`, `BenefitReserved`, `BenefitRedeemed`, `BenefitReleased`, `CustomerPackageExpired`, `CustomerPackageVoided` |
| **Application** | `IPackageCatalogService`, `ICustomerPackageService`, **`IBenefitRedemptionService`**, `IRetentionEntitlementGuard` (thin wrapper over `IEntitlementsService`) |
| **Infrastructure** | EF mappings, services, `PaymentCompleted` handler for online purchase, booking hook adapter |
| **Api** | `Controllers/Retention/*` under `/api/v1/` |

### 5.2 Dependencies (allowed)

```
Retention → Tenancy (TenantCustomer), Identity, Catalog (service IDs), Finance (payment intents / events), Subscriptions (IEntitlementsService)
Booking → Retention (IBenefitRedemptionService only — Application port)
Finance → Retention (PaymentCompleted event / handler in Retention Infrastructure — no Retention EF in Finance)
Tenancy/F1 → Retention (ICustomerPackageSummaryQuery or equivalent Application port only)
```

**Forbidden:** Retention querying Booking/Finance DbSets; Booking implementing benefit math internally; Retention calling Resend/Twilio; Reporting querying `retention.*` directly.

### 5.3 Booking integration (`IBenefitRedemptionService`)

| Method | When | Transaction |
|--------|------|-------------|
| `TryReserveBenefitsAsync` | Booking create when customer applies a package benefit to eligible service lines | **Same SQL transaction** as booking persist + slot lock commit path |
| `ReleaseReservationsAsync` | Booking cancelled / no-show policy (characterize with existing booking cancel) | Single transaction |
| `FinalizeRedemptionsAsync` | Appointment **completed** (same lifecycle as existing “complete” side effects) | Single transaction; moves reserved → used |

Validation at reserve time:

- Package `CustomerPackage` **Active**, not expired, not voided
- **Redemption scope** vs **booking `BusinessLocationId`**
- Catalog: service exists and is redeemable at that branch
- Sufficient **remaining** on targeted `BenefitBalance` (after existing reservations)
- Optional: staff permission `portal.retention` or `portal.bookings` for staff-assisted apply (TBD in implementation — default: customer self-serve on Discover; staff apply in Portal booking flow)

---

## 6. Data model (`retention.*`)

All entities **`ITenantEntity`** + global query filters. Composite FKs `(TenantId, Id)` where referencing tenant-scoped parents. **No cascade delete** that destroys ledger history.

### 6.1 Tables

| Table | Purpose |
|-------|---------|
| **`package_definitions`** | Sellable package product: name, description, price display (list price for till/online), currency, active flag, default expiry policy (months), **redemption_scope**, **sell_scope**, optional location checklist JSON/table for `selected_*` scopes |
| **`package_benefits`** (benefits) | Child rows: `PackageDefinitionId`, catalog **service id** (or service template key), **quantity granted** per purchase (sessions), optional **duration minutes** if product uses time-based credits |
| **`customer_packages`** | Instance sold to `TenantCustomerId`: link to definition snapshot version, **sold at** `BusinessLocationId`, sold channel (`in_person_till`, `in_person_transfer`, `online`), optional `PaymentIntentId`, status (`PendingPayment`, `Active`, `Expired`, `Voided`), `ActivatedAt`, `ExpiresAt`, `VoidedAt`, void reason |
| **`benefit_balances`** | One row per (customer package × benefit line): `Granted`, `Used`, `Reserved`, computed **Remaining** (check constraint / app invariant), **`rowversion`**, optional `ExpiresAt` override |
| **`benefit_transactions`** | Append-only ledger: `BenefitBalanceId`, sequence, type (`Grant`, `Reserve`, `Release`, `Redeem`, `Forfeit`, `Void`, `Adjust`), signed delta, resulting reserved/used/remaining snapshot, `BookingId` nullable, `BusinessLocationId` nullable, actor, `OccurredAt` |
| **`benefit_operations`** | Idempotency + mutation receipt: scoped key (tenant + operation kind + client key hash), canonical request fingerprint, result reference (`BenefitTransaction` ids / `CustomerPackageId`), created at — pattern aligned with F3 `CommerceOperation` ([f3-gift-cards-pay-links-plan.md](./f3-gift-cards-pay-links-plan.md) §6) |

### 6.2 Redemption & sell scope (approved #6)

Persist on **`package_definitions`** (not per customer):

**Redemption scope** (`PackageRedemptionScope`):

| Value | Meaning |
|-------|---------|
| `all_locations` | Redeem at any active tenant location (default) |
| `purchase_location_only` | Redeem only at `customer_packages.SoldAtLocationId` |
| `selected_locations` | Redeem only at locations in checklist |

**Sell scope** (`PackageSellScope`):

| Value | Meaning |
|-------|---------|
| `all_locations` | Any active location may sell (default) |
| `selected_locations` | Only checklist locations may sell |

**At redemption:** validate **booking location** against redemption scope. **Do not** mutate customer **home location** on `TenantCustomer`. Optional `BusinessLocationId` on grant/redemption transactions for reporting.

### 6.3 Expiry, forfeiture, owner void

| Rule | Behavior |
|------|----------|
| **Default expiry** | **12 months** from **`ActivatedAt`** (online: activation on payment success; in-person: activation at sell time unless spec’d “starts on first use” — **default: activation at sell**) |
| **Tenant config** | `PackageDefinition.DefaultExpiryMonths` (nullable → tenant default 12) |
| **Forfeiture** | Scheduled job or lazy check on read: when `UtcNow > ExpiresAt`, status → `Expired`, remaining credits **Forfeit** transactions; reservations on open bookings must **Release** or block completion (characterize: expire job releases reservations with audit) |
| **Owner void** | Owner-only API; sets `Voided`, writes **Void** ledger entries, releases reservations; **portal + admin audit** (business audit log pattern / existing owner mutation audit) |
| **No cash refund in 37.1** | Void is operational; monetary refund via Finance is **out of scope** unless linked payment intent refund policy already exists — document as manual/offline |

### 6.4 Snapshots

On sell, copy definition name, price, benefit lines, and scope into **`customer_packages`** snapshot columns or child snapshot table so catalog edits do not rewrite history.

---

## 7. API contract (sketch)

All routes **`/api/v1/`**. Mutations: JWT + stable `code` errors ([api-errors.md](../api-errors.md)). Tenant routes: **`X-Tenant-Id`** + middleware.

**Entitlement:** mutating routes require **`packages`** via `IEntitlementsService` (403 `entitlement.packages` or existing code pattern).

**Portal permission:** mutating catalog/sell/void require **`portal.retention`** (and owner for void). Read-only package list on customer detail may allow `portal.bookings` + retention entitlement — exact matrix in implementation; void **owner only**.

### 7.1 Tenant — catalog

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/tenant/retention/packages` | Business JWT + tenant | List package definitions (filter active) |
| POST | `/api/v1/tenant/retention/packages` | + `portal.retention` | Create definition + benefits |
| GET | `/api/v1/tenant/retention/packages/{id}` | Business JWT + tenant | Detail |
| PATCH | `/api/v1/tenant/retention/packages/{id}` | + `portal.retention` | Update (does not alter sold instances) |
| POST | `/api/v1/tenant/retention/packages/{id}/deactivate` | + `portal.retention` | Soft deactivate |

### 7.2 Tenant — customer packages

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/tenant/retention/customers/{tenantCustomerId}/packages` | Business JWT + tenant | List instances + balances |
| POST | `/api/v1/tenant/retention/customers/{tenantCustomerId}/packages/sell` | + `portal.retention` | In-person sell (till/transfer); body: `packageDefinitionId`, `businessLocationId`, `paymentMethod` (`till`, `transfer`), optional note; **`Idempotency-Key`** |
| POST | `/api/v1/tenant/retention/customer-packages/{id}/void` | Owner + `portal.retention` | Void package; **`Idempotency-Key`** |

### 7.3 Tenant — redemption assist (optional MVP)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/tenant/retention/customers/{tenantCustomerId}/redeemable-benefits` | Business JWT | Query by `businessLocationId` + service ids for booking UI |

### 7.4 Consumer / Discover

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/businesses/{slug}/retention/packages` | Anonymous | Public catalog (verified businesses only) |
| POST | `/api/v1/businesses/{slug}/retention/packages/{id}/purchase` | Customer JWT | Initialize Finance payment intent; metadata links to pending `CustomerPackage`; **`Idempotency-Key`** |
| GET | `/api/v1/me/retention/packages` | Customer JWT | Purchased packages + balances for linked tenant customers |

Online purchase endpoint returns **503/403** with explicit code when tenant not settlement-ready (Sprint 34 / stub-only guard) — mirror F3 pattern.

### 7.5 Booking (internal Application — not new public routes)

Booking create/update payloads gain optional **`appliedBenefits`** (customer package id + benefit balance id + quantity). Server validates; hooks call `IBenefitRedemptionService`.

**Request shape (illustrative — finalize in `@adeni/shared`):**

```json
{
  "packageDefinitionId": "uuid",
  "benefits": [
    { "catalogServiceId": "uuid", "quantityGranted": 5 }
  ],
  "redemptionScope": "all_locations",
  "sellScope": "all_locations",
  "selectedLocationIds": []
}
```

---

## 8. UX surfaces

| Surface | 37.1 scope |
|---------|------------|
| **Portal** | Nav entry gated by **`portal.retention`** + `packages` entitlement; package catalog CRUD (data grid); sell flow at POS/till; customer detail **Packages** tab showing sold location, scopes, balances, expiry; owner void |
| **Discover** | Business profile section **Packages**; purchase CTA when online payments enabled; logged-in **My packages** |
| **F1 customer profile** | Add **`retentionPackages`** source via Retention-owned read port: counts/active package names, `availability: available \| notEntitled \| notImplemented` → **available** when entitled; **no fake zeros** when not entitled; branch scope respects location rules like other F1 sources |
| **F5 segments** | **Package Holder** segment remains blocked until 37.1 data exists ([f5-smart-segments-document-preview-plan.md](./f5-smart-segments-document-preview-plan.md)) |

Use **`@adeni/ui`** list/toolbar patterns ([shared-data-grid.md](./shared-data-grid.md)).

---

## 9. Entitlements & portal permission

### 9.1 Plan entitlement: `packages`

Documented in parent [sprint-37-retention-module.md](./sprint-37-retention-module.md) §6.4:

| Key | Starter | Professional | Business |
|-----|---------|--------------|----------|
| **`packages`** | true | true | true |

**Spec-only PR does not edit** `plan-entitlements.seed.json`. Implementation PR must:

- Add key to seed if missing (Phase 0)
- Enforce on **all mutating** package catalog, sell, purchase, void routes
- Portal hides nav when not entitled (UX only; API is authoritative)

### 9.2 Portal permission: `portal.retention`

Add to `@adeni/shared` `PORTAL_PERMISSIONS` and role templates (proposal):

| Role | `portal.retention` |
|------|---------------------|
| owner | yes |
| manager | yes |
| receptionist | yes |
| accountant | no (unless product says otherwise) |
| practitioner | optional read-only via `portal.bookings` customer context — **no catalog edit** |
| ops | no |

Void package: **owner** role only (stricter than permission).

---

## 10. Security, concurrency, idempotency

| Area | Requirement |
|------|-------------|
| **Tenant isolation** | All tables tenant-scoped; cross-tenant → 403 + warning log |
| **PII** | No package ledger dumps in logs; mask customer identifiers in debug |
| **Online grant** | **`PaymentCompleted`** handler: verify intent metadata, signature path unchanged in Finance; **skip if already Active** (Murphy idempotency) |
| **In-person sell** | **`Idempotency-Key`** required; `benefit_operations` stores fingerprint (definition id, customer id, location, channel) |
| **Reserve** | Conditional update on `benefit_balances` with **`rowversion`**; `Remaining - Reserved >= qty`; failure → `retention.benefit_insufficient` |
| **Concurrency** | Real SQL transactions (not Redis as authority); lock order: balance → booking; retry transient deadlocks with same operation id; re-read idempotent result after conflict (F3 §6 pattern) |
| **Booking txn** | `TryReserveBenefitsAsync` **must** participate in booking create transaction — rollback booking if reserve fails |
| **Admin audit** | Owner void → audit entry |
| **Rate limits** | Public catalog + purchase: existing discovery/public limits (Sprint 20 edge) |

---

## 11. Files to touch (implementation checklist)

| Layer | Files |
|-------|-------|
| Domain | `Adeni.Domain/Retention/**` |
| Application | `IPackageCatalogService`, `ICustomerPackageService`, `IBenefitRedemptionService`, DTOs, `RetentionErrorCodes` |
| Infrastructure | EF configs, `RetentionModule`, `PaymentCompleted` handler, booking hook registration |
| Api | `Controllers/Retention/**` |
| Booking | Inject port only; hook in create/cancel/complete services |
| Finance | Payment intent metadata contract for package purchase |
| db | `db/Adeni.Database` — `retention` schema tables + indexes + RLS deferred |
| Tests | Unit (balance math, scope), integration (HTTP, tenant denial, idempotency, concurrent reserve) |
| packages/shared | Zod, i18n errors, `portal.retention`, entitlement type |
| packages/api-client | retention module |
| apps/portal, apps/discover | UX per §8 |

---

## 12. Tests

- [x] Unit: scope validation (`purchase_location_only`, `selected_locations`); expiry forfeit math; void releases reserve
- [x] Integration: sell in-person → grant balances; duplicate sell idempotency key → same package
- [x] Integration: online purchase → pending → webhook twice → single grant
- [x] Integration: booking create reserves; cancel releases; complete redeems
- [x] Integration: concurrent two bookings reserve last credit → one succeeds, one `benefit_insufficient` (sequential path: `Reserve_SecondBookingBeyondRemaining_IsInsufficient`; true race: `RetentionSqlServerTests` — authored, runs with `ADENI_SQLSERVER_TESTS=1`, **not yet executed against an isolated SQL Server**)
- [x] Integration: cross-tenant customer package id → 403/404
- [x] Integration: Starter without `packages` manipulation (if key false in test seed) → 403
- [x] Integration: redeem at wrong location → `retention.scope_location_denied`
- [x] Architecture: Booking does not reference Retention Infrastructure

Run: `dotnet test Adeni.slnx -c Release`

---

## 13. Rollout & dependencies

| Dependency | Impact |
|------------|--------|
| **Sprint 34** | **Online** package purchase on live Stripe/Paystack requires tenant settlement mapping; till/transfer **not** blocked |
| **Sprint 33 entitlements** | `packages` key seed + enforcement |
| **38B** | Optional `PackageExpiring` notification |
| **37.5 / 39B** | Package utilization reporting via `IRetentionAnalyticsService` |

**Rollout:**

1. Ship schema + API behind entitlement; Portal catalog + in-person sell first
2. Enable Discover catalog (read-only) 
3. Enable online purchase when settlement stub/E2E passes per tenant market
4. Monitor benefit_operations conflict rate and balance rowversion retries

---

## 14. Acceptance criteria

- [x] Business can define packages with benefit lines and redemption/sell scopes
- [x] Staff can sell package in-person before live online settlement; balances appear on customer
- [x] Customer with Active package can redeem at booking when location + service rules pass
- [x] Cancel releases reservation; complete consumes credit
- [x] Expired package forfeits remaining credits; owner can void with audit
- [x] Gift card ledger and package ledger remain separate; no gift tender for packages in 37.1
- [x] Online purchase double webhook does not double-grant
- [x] Entitlement and tenant isolation enforced on all mutations

---

## 14a. Implementation notes (closeout)

**Online purchase (Discover):**

- `GET /api/v1/businesses/{slug}/retention/packages` (anonymous, `PublicReadPolicy`) → `{ items, onlinePurchaseEnabled }`.
- `POST /api/v1/businesses/{slug}/retention/packages/{id}/purchase` (customer JWT, `PublicMutationPolicy`, optional `Idempotency-Key`) creates a `pending_payment` `CustomerPackage` + a Finance payment intent and returns `{ customerPackageId, paymentIntentId, status, checkoutUrl }`. **No credits exist until `PaymentCompleted`.**
- `GET /api/v1/businesses/{slug}/retention/redeemable-benefits` (customer JWT) feeds the Discover booking panel's `appliedBenefits` selection.
- **Config gate (Sprint 34):** `onlinePurchaseEnabled` / purchase succeeds only for the F3 sandbox condition — `(Development || Testing)` **and** payment provider `Stub`. Otherwise the purchase returns **503 `retention.online_purchase_unavailable`** and Discover shows "ask them to sell in person". Enabling live Paystack settlement means replacing that gate with tenant settlement readiness; no wallet/custody tables are involved either way.
- **Grant:** `PackagePurchasePaymentHandler` (`PaymentCompleted`, own DI scope) → `PackagePurchaseActivation`. Idempotent: non-pending packages are a no-op; amount/currency must equal the snapshot or the package stays pending (logged); package rowversion makes a concurrent duplicate lose. `PackageExpiryHangfireJob` also reconciles still-pending packages whose intent is `completed` (safety net if the handler failed).
- Known limits: benefits and expiry months are read from the live definition **at activation** (no benefit snapshot table — editing a definition between purchase and payment changes what is granted); refunds of online package payments require a manual owner **void**; stale unpaid `pending_payment` packages are not auto-expired.
- Bug fixed on the way: `PaymentOrchestrator` reused the payment-intent id as the audit-log primary key, so the second audit write per intent (`payment.confirmed`) collided and confirmation returned HTTP 500 after the payment had already committed. Audit rows now get their own ids.

**Surfaces:** portal `/customers/:id?tab=packages` (sold location/channel, redemption scope, balances, expiry), F1 profile tile (active count / remaining credits), Discover `/businesses/:slug#packages`, `/my-packages`, booking panel "Use package credits" on the confirm step.

---

## 15. Open questions

| Question | Proposed default | Decision |
|----------|------------------|----------|
| Expiry anchor | `ActivatedAt` at sell | TBD |
| First-use activation | Defer; sell-time activation | TBD |
| Practitioner sell package | Receptionist + manager + owner only | TBD |
| Partial service redemption (1 of 5 on multi-qty line) | One session = one benefit unit | TBD |
| Refund on void when online purchase | Manual Finance refund out of scope | TBD |

---

## 16. References

- [AGENTS.md](../../AGENTS.md) — Murphy webhooks, `/api/v1/`, no custody
- [r1a-product-decisions.md](./r1a-product-decisions.md)
- [sprint-37-retention-module.md](./sprint-37-retention-module.md)
- [f3-gift-cards-pay-links-plan.md](./f3-gift-cards-pay-links-plan.md) — idempotency/concurrency patterns
- [sprint-34-payment-settlement.md](./sprint-34-payment-settlement.md)
