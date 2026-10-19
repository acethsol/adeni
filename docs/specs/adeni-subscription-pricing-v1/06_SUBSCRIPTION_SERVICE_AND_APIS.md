# Subscription Services & APIs

## Suggested services
### PricingCatalogService
- current signup pricing
- historical pricing version lookup
- currency resolution
- platform fee configuration

### TenantPricingResolver
Resolution order:
1. valid commercial override
2. bound agreed subscription price
3. catalog price for new signup

### EntitlementService
Examples:
- CanAddPractitioner
- CanAddLocation
- HasAdvancedAnalytics
- HasWhatsAppIntegration

Backend enforcement is authoritative.

### SubscriptionLifecycleService
- start trial
- convert to active
- past_due/grace/suspended transitions
- cancel/reactivate
- preserve agreed pricing

### CommercialOverrideService
Admin-only:
- negotiated price
- discounts
- comps
- trial changes
- price locks
- audit

## Suggested APIs

### Public
- GET /api/pricing?countryCode=CA
- GET /api/pricing?countryCode=NG

### Portal
- GET /api/subscription
- GET /api/subscription/entitlements
- POST /api/subscription/checkout
- POST /api/subscription/change-plan
- POST /api/subscription/cancel
- POST /api/subscription/reactivate

### Admin
- GET /api/admin/tenants/{tenantId}/subscription
- PATCH /api/admin/tenants/{tenantId}/subscription
- POST /api/admin/tenants/{tenantId}/commercial-override
- POST /api/admin/tenants/{tenantId}/trial
- POST /api/admin/tenants/{tenantId}/suspend
- POST /api/admin/tenants/{tenantId}/reactivate
- GET /api/admin/tenants/{tenantId}/billing-audit

All admin commercial mutations require server authorization and audit reason.
