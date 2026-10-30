# Spec: Sprint 37.4 — Multi-location retention

> Copied from [`_template.md`](./_template.md). Standing play 1 in [AGENTS.md](../../AGENTS.md).  
> **Parent:** [sprint-37-retention-module.md](./sprint-37-retention-module.md) §3 *37.4 Multi-location*, §6.4 entitlements.  
> **Builds on:** [sprint-37-1-packages-benefit-ledger.md](./sprint-37-1-packages-benefit-ledger.md) (package sell/redeem scope enums + `PackageScopePolicy`), [sprint-37-2-memberships.md](./sprint-37-2-memberships.md) (membership credits via same benefit ledger).

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 37.4 — Multi-location retention |
| **Author** | Engineering |
| **Status** | **37.4-A/B implemented** (dev/local) |
| **Created** | 2026-10-30 |
| **Depends on** | 37.1 packages, 37.2 memberships (benefit redeem path). Loyalty/referrals are tenant-wide (no 37.4 change). |

---

## 1. Goal (one sentence)

Let **Business** tier tenants redeem prepaid package and membership credits at **any location** when product scope allows, while **Starter and Professional** tenants remain limited to **same-location** redemption even if a package is configured with `all_locations` — enforced in the API, not UI-only.

---

## 2. Context

- **Product scope (37.1)** — `PackageRedemptionScope` / `PackageSellScope` + `PackageScopePolicy` already decide whether a *product definition* allows a booking location.
- **Gap (37.1 deferral)** — `crossLocationRetention` entitlement was never wired; `AllLocations` redemption therefore effectively allows cross-branch redeem for every plan today.
- **Memberships (37.2)** — credits redeem through `IBenefitRedemptionService`; `EnrolledAtLocationId` is stored but not compared to booking location (comment: “37.4”).
- **Plans** — [plan-entitlements.seed.json](./adeni-subscription-pricing-v1/plan-entitlements.seed.json): `crossLocationRetention` **false / false / true** (Starter / Professional / Business). Professional is typically single-location (`maxLocations: 1`); the gate matters when a tenant has **multiple active locations** (Business) or future multi-location Pro.

---

## 3. Delivery slices

| Slice | Content | Status |
|-------|---------|--------|
| **37.4-A — Entitlement + package/membership redeem gate (first PR)** | Seed + `TenantEntitlements` + `IRetentionEntitlementGuard`; enforce on reserve/redeem + list redeemable; stable `entitlements.crossLocationRetention`; tests | **Done** |
| 37.4-B — Portal UX | Redemption scope on package create + plan hints; customer package footnote; membership cross-location copy | **Done** |
| 37.4-C — Membership plan location scope (if product wants plan-level selected locations) | Deferred unless product expands beyond enrolment location | Out of slice A |

---

## 4. In scope (37.4-A)

- [ ] **`crossLocationRetention`** in both seed copies, `JsonPlanEntitlementsMatrix`, `@adeni/shared` Zod + plan catalog, parity test.
- [ ] **`IRetentionEntitlementGuard`** — `IsCrossLocationRetentionEntitledAsync` / `EnsureCrossLocationRetentionEntitledAsync` (tenant resolved via `IEntitlementsService`, same pattern as `loyaltyTiers`).
- [ ] **Cross-location definition** — booking `businessLocationId` ≠ package `SoldAtLocationId` (packages) or ≠ `EnrolledAtLocationId` when both are set (memberships). Either side null → no cross-location gate (preserve existing behaviour for missing location data).
- [ ] **`IBenefitRedemptionService`** — after `PackageScopePolicy.CanRedeemAt` succeeds, if cross-location attempt and not entitled → `403` with `entitlements.crossLocationRetention` (use `RetentionErrorCodes` + shared i18n).
- [ ] **`ListRedeemableAsync` / consumer list** — omit balances that would fail the cross-location gate (same as scope filter today).
- [ ] **Tests** — unit tests on guard + benefit reserve path; entitlement parity.

## 5. Out of scope (37.4-A)

- Loyalty, referrals, tiers (tenant-wide programs — parent §37.4).
- Changing package catalog CRUD rules (owners may still configure `all_locations`; enforcement is at redeem).
- **`advancedRetentionAnalytics` / 37.5** reporting.
- SQL schema changes (no new tables).
- Admin write paths.

---

## 6. API contract

No new routes. Existing mutating/read paths that hit benefit redemption return stable errors:

| Code | HTTP | When |
|------|------|------|
| `entitlements.crossLocationRetention` | 403 | Cross-location redeem attempted without Business entitlement |

All other retention scope failures remain `retention.scope_location_denied` (409/400 per existing mapper).

---

## 7. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `RetentionErrorCodes.cs`; optional helper on `PackageScopePolicy` or small `RetentionLocationPolicy` for cross-location detection |
| Application | `IRetentionServices.cs` (guard methods) |
| Infrastructure | `RetentionEntitlementGuard`, `BenefitRedemptionService`, `JsonPlanEntitlementsMatrix` |
| Catalog | `plan-entitlements.seed.json`, `packages/shared/.../plan-entitlements.json` |
| Shared | `schemas.ts`, `plan-entitlements.ts`, `i18n/errors.ts` |
| Tests | `PlanEntitlementsParityTests`, `RetentionServiceTests` or dedicated cross-location tests, fake guards |
| Docs | `sprints.md` status row |

---

## 8. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Unchanged — location ids are tenant-scoped |
| **Entitlements** | Authoritative in `BenefitRedemptionService`; portal may hide cross-location packages later (37.4-B) |
| **Booking** | No Booking module change — still calls `IBenefitRedemptionService` only |
| **PII / audit** | No new PII; no new audit actions for slice A |

---

## 9. Tests

- [ ] Business entitlement (or fake guard `true`): package `AllLocations`, sold at A, redeem at B → success (with credits).
- [ ] Not entitled: same scenario → `entitlements.crossLocationRetention`.
- [ ] Same location: entitled not required → success.
- [ ] `PurchaseLocationOnly` / selected locations unchanged (existing tests).
- [ ] Parity: seed `crossLocationRetention` ⇄ `TenantEntitlements`.

Run: `dotnet test Adeni.slnx -c Release`

---

## 10. Acceptance criteria (37.4-A)

- [ ] Starter/Professional tenants cannot reserve benefits for a booking at a **different** location than purchase/enrolment when product scope would otherwise allow it.
- [ ] Business tenants with `crossLocationRetention` can.
- [ ] Discover/list redeemable APIs do not surface credits the tenant cannot use at the requested location.

---

## 11. Open questions

| Question | Proposed default | Decision |
|----------|------------------|----------|
| Block `all_locations` in catalog UI for non-Business | Enforce at redeem only; optional 37.4-B copy | Default |
| Membership plan-level selected locations | Use enrolment location only in A | Default |
