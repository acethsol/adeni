# Tenant isolation

Adeni is multi-tenant: each business is a tenant. Customer-facing discovery is intentionally cross-tenant; business portal routes are strictly scoped.

## Request pipeline

1. `TenantScopeMiddleware` — disables EF global filter at the start of each request
2. `TenantAccessMiddleware` — for `/api/v1/tenant/*`, requires `X-Tenant-Id` and validates JWT / `business_users` membership
3. `TenantFilterSyncMiddleware` — syncs `ITenantContext` into `AdeniDbContext.ActiveTenantFilterId`
4. EF global query filters on all `ITenantEntity` tables when filter is active

Cross-tenant access attempts return **403** and write `security.cross_tenant_denied` to the audit log.

## Route classes

| Class | Examples | Tenant filter | Notes |
| --- | --- | --- | --- |
| Public discovery | `GET /discovery`, `GET /businesses/{slug}` | Off | Verified businesses only |
| Customer | `GET /bookings`, `POST /bookings` | Off | Scoped by customer identity |
| Tenant portal | `GET /tenant/profile`, `GET /tenant/services` | On | Requires `X-Tenant-Id` |
| Admin | `GET /admin/businesses` | Off | Role + MFA gated |

## Cache keys

| Key pattern | Scope |
| --- | --- |
| `slot-lock:{tenantId}:...` | Tenant-private |
| `tenant:{tenantId}:profile` | Tenant-private |
| `location:{slug}:profile` | Public (slug is globally unique) |
| `discovery:...` | Public aggregate |

Tenant-private cache entries must include `{tenantId}` in the key.

## Sprint 12e tests

- Integration: tenant A cannot access tenant B profile/services/bookings
- EF filter tests for bookings, services, locations
- Architecture test: every `ITenantEntity` has a global query filter
