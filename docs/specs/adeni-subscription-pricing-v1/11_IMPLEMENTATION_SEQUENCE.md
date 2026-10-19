# Recommended Implementation Sequence

## Phase 1 — Foundation
1. Inspect existing code.
2. Normalize SKU and BillingStatus.
3. Add/confirm BillingProfile.
4. Add versioned PricingCatalog.
5. Seed CA/NG pricing.
6. Add entitlement resolver.
7. Bind TenantSubscription to agreed price/version.

## Phase 2 — Trial lifecycle
1. Start Pro trial on activation/publish.
2. Trial status/expiry.
3. Trial-to-paid.
4. past_due/grace/suspended.
5. Admin trial controls.

## Phase 3 — Discover + Portal
1. `/plan`
2. country/currency pricing
3. Most Popular Pro
4. Portal subscription page
5. checkout/change plan

## Phase 4 — Admin commercial controls
1. negotiated pricing
2. discounts
3. price locks
4. comp reason
5. effective/expiry dates
6. audit history

## Phase 5 — GMV readiness
1. platformFeePercent in catalog
2. payment fee fact persistence
3. launch fee remains 0%
4. provider abstraction supports future split/application fee

## Later, not now
- annual billing
- AI add-on
- SMS packs
- Insights add-on
- membership billing
- accounting integrations
- US launch pricing
- financing/insurance referrals
