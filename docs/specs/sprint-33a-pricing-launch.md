# Spec: Sprint 33a — Pricing launch catalog sync

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 33a — Launch pricing & catalog authority |
| **Status** | In progress (catalog synced; phases 1–5 below) |
| **Created** | 2026-10-19 |
| **Depends on** | [sprint-33-saas-subscription-billing-mrr.md](./sprint-33-saas-subscription-billing-mrr.md) |
| **Package authority** | [adeni-subscription-pricing-v1/](./adeni-subscription-pricing-v1/00_READ_ME_FIRST.md) |

---

## 1. Goal (one sentence)

Ship **versioned launch list prices** (CA/NG self-serve, US disabled) and **0% GMV platform fee** at launch in shared catalog + server mirrors, then execute remaining Sprint 33 billing/entitlements work against that commercial baseline.

---

## 2. Context

- Stakeholder package **Adeni_Subscription_Pricing_Cursor_Package_v1** imported under [adeni-subscription-pricing-v1/](./adeni-subscription-pricing-v1/).
- Runtime catalog: [country-pricing.json](../../packages/shared/src/data/country-pricing.json) — `version: 2026-launch-01`, `effectiveFrom: 2026-10-19`.
- C# mirror: `SubscriptionCatalogPrices.cs` (must stay aligned with JSON).
- Parent sprint: live checkout, webhooks, MRR, portal `/plan` — [sprint-33-saas-subscription-billing-mrr.md](./sprint-33-saas-subscription-billing-mrr.md).

### Launch list prices (summary)

| Country | Starter | Professional | Business | GMV @ launch | Self-serve |
|---------|---------|--------------|----------|--------------|------------|
| **CA** | CAD 99 | CAD 149 | CAD 249 | 0% | Yes |
| **NG** | NGN 35,000 | NGN 55,000 | NGN 95,000 | 0% | Yes |
| **US** | — | — | — | 0% (wired) | **No** |

---

## 3. In scope (this spec)

- [x] Import pricing package docs + seed JSON into `docs/specs/adeni-subscription-pricing-v1/`.
- [x] Sync [country-pricing.json](../../packages/shared/src/data/country-pricing.json) to launch figures + metadata.
- [x] Sync `SubscriptionCatalogPrices` + shared/C# tests for catalog amounts.
- [x] Update [pricing-and-monetization.md](../pricing-and-monetization.md) §2 table.
- [ ] **Phase 1–5 implementation** (see §5) — remainder of Sprint 33a/33.

## 4. Out of scope

- Full **entitlements v2** matrix enforcement (limits per SKU) — see package [02_PLAN_ENTITLEMENTS.md](./adeni-subscription-pricing-v1/02_PLAN_ENTITLEMENTS.md); separate PRs.
- Discover **`/plan`** public pricing page — Phase 3.
- **Murphy-compliant subscription webhooks** — parent Sprint 33 spec.
- US launch pricing amounts.
- Annual billing, add-ons, wallet/custody.

---

## 5. Implementation phases

From [11_IMPLEMENTATION_SEQUENCE.md](./adeni-subscription-pricing-v1/11_IMPLEMENTATION_SEQUENCE.md):

### Phase 1 — Foundation

1. Inspect existing code (SKU, billing status, tenant subscription fields).
2. Normalize SKU and `BillingStatus` enums/API.
3. Add/confirm **BillingProfile** (billing country immutability after subscribe).
4. Add **versioned PricingCatalog** persistence (beyond static JSON — bind tenant to catalog version + agreed price).
5. Seed **CA/NG** pricing from `country-pricing.json` / DB seed.
6. Add **entitlement resolver** (read SKU → capabilities; server enforce limits later).
7. Bind `TenantSubscription` to agreed price/version on checkout.

### Phase 2 — Trial lifecycle

1. Start **Professional** trial on activation/publish (30 days default).
2. Trial status/expiry handling.
3. Trial-to-paid conversion.
4. `past_due` / grace / `suspended`.
5. Admin trial controls.

### Phase 3 — Discover + Portal

1. Public **`/plan`** with country/currency pricing.
2. **Most Popular** on Professional.
3. Portal subscription page + checkout / change plan.

### Phase 4 — Admin commercial controls

1. Negotiated pricing, discounts, price locks, comp reason.
2. Effective/expiry dates + audit history.
3. Catalog change must not overwrite bound tenant price.

**Admin configurability (detailed plan):** [sprint-33b-admin-commercial-config.md](./sprint-33b-admin-commercial-config.md). Today: per-tenant fields + wizard on **Businesses → Billing**; catalog remains static JSON/C# until versioned DB + admin catalog UI land in 33b.

### Phase 5 — GMV readiness

1. `platformFeePercent` in catalog (launch **0%** CA/NG).
2. Payment fee fact persistence.
3. Provider abstraction ready for future split/application fee.

**Later (package):** annual billing, AI/SMS add-ons, US pricing, memberships.

---

## 6. Acceptance criteria (summary)

From [10_TESTS_AND_ACCEPTANCE_CRITERIA.md](./adeni-subscription-pricing-v1/10_TESTS_AND_ACCEPTANCE_CRITERIA.md):

| Area | Must hold |
|------|-----------|
| **Pricing** | CA → CAD launch ladder; NG → NGN launch ladder; city ≠ price; disabled country (US) no self-serve; bound price survives new catalog version |
| **Trial** | 30-day Pro trial on activation; no permanent free tier after expiry |
| **Entitlements** | Starter/Pro/Business limits enforced server-side — implementation spec: [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md) |
| **Overrides** | Admin negotiated price + audit; catalog bump does not overwrite agreed price |
| **GMV** | Launch 0% CA/NG; customer total = business-defined total |
| **Billing country** | Primary location change does not silently reprice |
| **Security** | Tenant cannot set agreed price via client; admin-only commercial mutations |

---

## 7. Files (catalog sync — done)

| Layer | Files |
|-------|-------|
| Docs | `docs/specs/adeni-subscription-pricing-v1/*`, this file, `pricing-and-monetization.md` |
| Shared | `packages/shared/src/data/country-pricing.json`, `country-pricing.ts`, tests |
| Application | `SubscriptionCatalogPrices.cs` |
| Tests | `country-pricing.test.ts`, `SubscriptionMetricsServiceTests.cs` |

---

## 8. Tenant / auth / audit

- Catalog is **global** (not tenant-scoped JSON); tenant overrides remain per [pricing-and-monetization.md §5](../pricing-and-monetization.md#5-trials-grace-and-discounts-not-tiers).
- Admin commercial mutations → audit (Phase 4).
- No PII in pricing logs.

---

## Related

- [sprint-33-saas-subscription-billing-mrr.md](./sprint-33-saas-subscription-billing-mrr.md)
- [subscription-billing.md](../subscription-billing.md)
- [04_BILLING_COUNTRY_AND_PRICING_CATALOG.md](./adeni-subscription-pricing-v1/04_BILLING_COUNTRY_AND_PRICING_CATALOG.md)
