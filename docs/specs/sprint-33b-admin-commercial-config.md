# Spec: Sprint 33b — Admin commercial & pricing configurability

| Field | Value |
|-------|-------|
| **Sprint** | 33b — Admin configurability (Phases 4–5 of pricing package) |
| **Status** | **33b-MVP** committed; **33b-1** (versioned DB catalog + admin read-only + `GET /api/v1/pricing`) implemented locally |
| **Created** | 2026-10-19 |
| **Depends on** | [sprint-33a-pricing-launch.md](./sprint-33a-pricing-launch.md), [adeni-subscription-pricing-v1/](./adeni-subscription-pricing-v1/00_READ_ME_FIRST.md) |
| **Implementation plan** | See parent agent deliverable + § below |

---

## 1. Goal (one sentence)

Make **global pricing catalog**, **per-tenant commercial terms**, and **GMV platform-fee settings** authoritative on the server and **operable from Admin**, while Discover/Portal consume the same resolved values.

---

## 2. Current baseline (already shipped)

| Area | Today |
|------|--------|
| **Admin UX** | `Businesses` → subscription wizard; `businesses/:id/billing` (terms + charges) |
| **Admin API** | `GET/PATCH /api/v1/admin/businesses/{id}/commercial-terms` (MFA) |
| **Tenant storage** | Discount + custom SaaS columns on `tenancy.tenants` |
| **Catalog** | `country-pricing.json` + `SubscriptionCatalogPrices.cs` (not DB-versioned) |
| **Client resolver** | `@adeni/shared` `resolveTenantSaasListPrice` / `applyTenantSaasDiscount` |
| **Audit** | `tenant.commercial_terms_updated` — action only, no before/after |
| **Metrics** | `GET /api/v1/admin/metrics/subscription` |

**Gaps vs package:** `PricingCatalogVersion`, `TenantCommercialOverride`, `BillingProfile`, `BillingAuditLog`, bound `AgreedMonthlyAmount`, `PriceLockEndsAt`, public pricing API, server billing using overrides, admin catalog editor, GMV admin surface.

---

## 3. Config layers

1. **Global catalog** — versioned list prices + `platformFeePercent` + `selfServeEnabled` per country/SKU (source of truth: DB; seed from `2026-launch-01`).
2. **Bound subscription** — on first paid checkout: freeze `pricingCatalogVersionId`, `agreedMonthlyAmount`, `currencyCode`, optional `priceLockEndsAt` (catalog bumps do not reprice).
3. **Per-tenant override** — trials, discounts, negotiated price, comp reason, effective/expiry (migrate from tenant columns → `TenantCommercialOverride` when table lands).
4. **Markets / feature flags** — `CatalogMarkets.IsLive` for discovery; do **not** duplicate SaaS amounts per city.

**Resolution order (server):** valid override → bound agreed price → active catalog (new signup).

---

## 4. Phased delivery

| Phase | Scope | Admin | API / data |
|-------|--------|-------|------------|
| **33b-MVP** | Correct money + ops UX | Billing page shows catalog vs charged; require reason on save; audit before/after | `TenantPricingResolver` in C#; extend GET commercial-terms; optional `GET /api/v1/pricing` |
| **33b-1** | Versioned catalog | Read-only **Pricing → Catalog** (active version + diff preview) | `PricingCatalogVersion` + `PricingCatalogEntry`; seed migration; admin list/get |
| **33b-2** | Full tenant commercial | Price lock, effective dates, audit timeline panel | `BillingAuditLog` or enriched audit; `POST` override lifecycle |
| **33b-3** | GMV admin | **Pricing → GMV** per country (%, mode, self-serve) — no provider secrets | Read/write catalog entries; `PaymentOrchestrator` reads country GMV from resolver |
| **33b-4** | Catalog publish | Draft → publish new version (ops workflow) | `POST /api/v1/admin/pricing-catalog/versions` + activate |

---

## 5. Security

- All routes under `/api/v1/admin/*` — JWT + **Admin MFA** (existing policy).
- Commercial PATCH requires non-empty **reason** (`billingCompReason` or dedicated `changeReason`).
- **BillingAuditLog** / audit metadata: before/after JSON; no card data, no webhook secrets, no Paystack/Stripe keys in UI.
- Tenants cannot set agreed price or catalog via portal APIs.

---

## 6. Consumer wiring

| Surface | Consumption |
|---------|-------------|
| **Discover `/plan`** | `GET /api/v1/pricing?countryCode=` (or SSR fetch); fallback to shared JSON until API live |
| **Portal `/plan`** | Tenant usage + **resolved** list/charge amounts from API (not raw catalog as “your price”) |
| **Checkout / webhooks** | `TenantPricingResolver` + bound agreed price |
| **GMV checkout** | Country `platformFeePercent` from catalog service (deprecate duplicate `markets.json` fee over time) |

---

## Related

- [08_ANGULAR_PLAN_AND_PORTAL_UX.md](./adeni-subscription-pricing-v1/08_ANGULAR_PLAN_AND_PORTAL_UX.md)
- [06_SUBSCRIPTION_SERVICE_AND_APIS.md](./adeni-subscription-pricing-v1/06_SUBSCRIPTION_SERVICE_AND_APIS.md)
- [05_DATA_MODEL_SQL_SERVER.md](./adeni-subscription-pricing-v1/05_DATA_MODEL_SQL_SERVER.md)
