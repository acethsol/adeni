# Spec: Full-application auth & production hardening

**Status:** Implemented (branch `chore/full-app-auth-production-hardening`)  
**Authority:** Post-retention audit follow-up; user-approved plan Oct 2026.

## Goal

Close P0/P1 authorization and production-readiness gaps on tenant portal APIs, webhooks, sample-data defaults, and customer SSR without expanding v1 product scope.

## In scope

- Server-side RBAC on tenant controllers previously gated only by membership
- Customer directory restricted to `business.bookings` (not `bookings_self`)
- Webhook body size limits; staging sample data off + config validation
- Media upload purpose permissions; local path containment; verification upload size parity
- Customer Express security headers (non-CSP baseline; route-level headers unchanged in `app.routes.server.ts`)
- Integration tests for forbidden practitioner paths

## Out of scope

- New permission keys or role matrix changes
- Auth0 BFF / httpOnly session
- Wallet/custody, microservices

## P2 addendum (same branch)

- `checkoutRealtimeToken` on payment initialize + hub validation
- `IMediaUploadBlobValidator` on confirm paths
- Auth0 memory cache + refresh tokens; CI SHA pins

## Security notes

- Tenant routes: JWT/dev auth + `X-Tenant-Id` + `[RequiresBusinessPermission]` / service-layer purpose checks
- Webhooks: existing HMAC + idempotency unchanged; add size cap before read
- Admin audit unchanged on this pass

## API contract

No new routes. Practitioners receive `403` on previously reachable tenant mutation and customer directory endpoints (breaking for mis-scoped clients; intentional).
