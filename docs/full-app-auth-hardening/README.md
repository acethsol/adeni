# Full-application auth & production hardening (closeout)

Branch: `chore/full-app-auth-production-hardening` (from `dev`).

## Shipped (P0/P1)

| Area | Change |
|------|--------|
| Tenant RBAC | `[RequiresBusinessPermission]` on tenant profile/settings/policies/notifications/verification/locations, services, menu groups, customers (`business.bookings` only), kiosk PIN (`business.bookings`), public-page media mutations |
| Media upload-url | Purpose-based permission in `TenantMediaService` (profile/staff/messages/public page) |
| Webhooks | 512 KB body cap via `WebhookBodyReader` (finance + subscription) |
| Sample data | Development-only default; Staging `Enabled: false`; startup guard in Staging/Production |
| Local storage | Path traversal hardening in `LocalFileStorage` |
| Media PUT | 5 MB default / 10 MB verification on `MediaUploadController` |
| Customer SSR | Baseline security headers on Express (`server.ts`) |

## P2 (shipped on same branch)

- Payment checkout SignalR — HMAC `checkoutRealtimeToken` on initialize + `?rt=` on stub checkout URL; hub rejects raw provider reference
- Post-upload blob validation — size, content-type, magic bytes on cover/gallery/avatar/message confirm
- Auth0 — `cacheLocation: memory` + `useRefreshTokens`; documented in `docs/auth0-setup.md`
- CI — pinned `actions/checkout`, `setup-dotnet`, `setup-node` commit SHAs in `.github/workflows/ci.yml`

## Tests

- `BusinessTenantRbacIntegrationTests` — practitioner/accountant mutations forbidden; receptionist customer list OK
- `LocalFileStoragePathTests` — rejects `..` keys
- Updated `CustomerProfileIntegrationTests` / `TenantIsolationIntegrationTests` for new RBAC

Validation: `dotnet test Adeni.slnx -c Release` (all projects green; SQL Server–gated tests skipped when unset).
