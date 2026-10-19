# Billing Country & Pricing Catalog

## Billing country
At signup, initialize billing country from primary location if needed.
After subscription creation, use a dedicated BillingProfile.

Changing primary operational location must not silently change billing country/currency.

## BillingProfile
Conceptual fields:
- TenantId
- BillingCountryCode
- CurrencyCode
- LegalBusinessName
- BillingAddress
- TaxIdentifier (optional)
- CreatedAt
- UpdatedAt

## Versioned pricing catalog
Conceptual:
- PricingCatalogVersion
- PricingCatalogEntry

Each entry stores:
- CountryCode
- CurrencyCode
- SKU
- MonthlyAmount
- PlatformFeePercent
- IsSelfServeEnabled

Seed:
CA: 99 / 149 / 249 CAD, 0% GMV fee
NG: 35000 / 55000 / 95000 NGN, 0% GMV fee

Do not duplicate pricing by city/market.

## Resolution order
1. Active valid tenant commercial override
2. Tenant's bound subscription/agreed price
3. Active catalog price for new signup
