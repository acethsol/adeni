# SQL Server Data Model — Conceptual

Adapt to the existing schema. Do not duplicate concepts that already exist.

## TenantSubscription
Suggested fields:
- Id
- TenantId
- SubscriptionSku
- BillingStatus
- PricingCatalogVersionId
- AgreedMonthlyAmount
- CurrencyCode
- TrialSku
- TrialStartedAt
- TrialEndsAt
- ActivatedAt
- PriceLockEndsAt
- CurrentPeriodStart
- CurrentPeriodEnd
- CancelAtPeriodEnd
- CancelledAt
- CreatedAt
- UpdatedAt
- RowVersion

## BillingProfile
- Id
- TenantId
- BillingCountryCode
- CurrencyCode
- LegalBusinessName
- BillingAddress...
- TaxIdentifier
- CreatedAt
- UpdatedAt

## PricingCatalogVersion
- Id
- Code
- EffectiveFrom
- EffectiveTo
- Status
- CreatedAt

## PricingCatalogEntry
- Id
- PricingCatalogVersionId
- CountryCode
- CurrencyCode
- SubscriptionSku
- MonthlyAmount
- PlatformFeePercent
- IsSelfServeEnabled

## TenantCommercialOverride
Prefer a separate entity rather than temporary columns scattered on Tenant:
- Id
- TenantId
- CustomSaasMonthlyAmount
- CustomSaasCurrency
- CustomSaasSku
- DiscountType
- DiscountValue
- DiscountCycles
- RemainingDiscountCycles
- EffectiveFrom
- ExpiresAt
- PriceLockEndsAt
- CompReason
- ApprovedByUserId
- CreatedByUserId
- CreatedAt
- UpdatedAt
- IsActive

## BillingAuditLog
- Id
- TenantId
- AdminUserId
- Action
- BeforeJson
- AfterJson
- Reason
- CreatedAt

## Entitlements
Centralize plan entitlements.
Do not scatter `if plan == ...` checks across controllers/components.

## Security
Do not store raw card data.
