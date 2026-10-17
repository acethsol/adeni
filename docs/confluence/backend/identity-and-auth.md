# Backend module — Identity & Auth

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

Links **Auth0 identities** to Adeni **Customer** and **BusinessUser** records, enforces **roles** and **portal permissions**, and supports **staff invites** and calendar-scoped access.

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Identity/` — `Customer`, `BusinessUser`, `StaffPortalInvite`, permission constants |
| Application | `Adeni.Application/Auth/` — `IAuthSyncService`, `IStaffAccessService`, `IPortalPermissionService`, `AdeniRoles`, `AdeniClaimTypes` |
| Infrastructure | `Adeni.Infrastructure/Identity/` — `AuthSyncService`, `StaffAccessService`, `PortalPermissionService` |
| API | `AuthController`, `TenantAccessController`, `TenantStaffController` (invites) |

Registration: `AddIdentityModule()`.

---

## Key behaviors

- **JWT validation:** RS256, audience/issuer from configuration (`AddAdeniAuth`).
- **Claims namespace:** `https://adeni.io/` for roles, tenant id, portal permissions.
- **Auth sync:** `POST /api/v1/auth/sync` creates/links user after Auth0 login.
- **Session introspection:** `GET /api/v1/auth/me` — roles, tenant, permissions for client routing.
- **Staff access:** Portal permissions gate routes (see portal `portalPermissionGuard`).
- **Dev-only:** `DevBusinessAuthMiddleware`, `DevCustomerAuthMiddleware`, `DevAdminAuthMiddleware` — Development/Testing only.

---

## HTTP routes (primary)

| Method | Route | Auth |
|--------|-------|------|
| POST | `/api/v1/auth/sync` | Bearer |
| GET | `/api/v1/auth/me` | Bearer |
| GET/POST/PATCH | `/api/v1/tenant/access/*` | Business + tenant |
| POST | `/api/v1/tenant/staff/invites` | Business + permissions |

---

## Security notes

- Fail closed: invalid token → 401.
- Admin MFA: `Auth0:RequireMfaForAdmin` — see `docs/mfa-enforcement.md`.
- Never log raw tokens or PII — use `PiiMasker`.

---

## Clients

| App | Usage |
|-----|--------|
| Discover | Customer Auth0 SPA, `customerAuthGuard` for my-bookings |
| Portal | Business Auth0, tenant header on API service |
| Admin | Admin Auth0 + MFA |

---

## Tests

- `tests/Adeni.Api.Tests/Controllers/AuthControllerMeTests.cs`
- `tests/Adeni.Infrastructure.Tests/Identity/`

---

## Related docs

- `docs/auth0-setup.md`
- [Tenancy](./tenancy.md) — business user ↔ tenant
