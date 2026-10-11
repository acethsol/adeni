# Spec: Staff accounts & portal permissions

> Standing play 1 — [AGENTS.md](../../AGENTS.md). Do not write code until this spec is reviewed.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 25 — Staff accounts & permissions |
| **Author** | Agent + product |
| **Status** | In progress (25c) |
| **Created** | 2026-10-10 |

---

## 1. Goal (one sentence)

Owners can invite staff to log into the business portal, and each login only sees the screens and data their permission role allows.

---

## 2. Context

- Sprint 24 shipped an **owner-managed roster** (`StaffMember` in Booking): identity, `roleKey` labels, hours, leave, calendar. Spec: [sprint-24-staff-ops.md](./sprint-24-staff-ops.md).
- **Out of scope for 24 (explicit):** staff Auth0 logins / employee self-service.
- Today the portal is a single **business owner** login (`BusinessUser` with `Role = "owner"`). `StaffMember.RoleKey` is a **floor label** for booking UI (stylist, barber, manager, …) — not AuthZ.
- Auth sync for business (`AuthSyncService.SyncBusinessUserAsync`) creates a **new tenant** when no `BusinessUser` exists — that must **never** happen for invited staff.
- Module split (keep clear):
  - **Booking** — floor person (bookable, hours, leave, services).
  - **Identity** — who can log in (`BusinessUser`), invite lifecycle, permission role.
- Strategy: still **no payroll / POS / HR docs** ([product-strategy.md](../product-strategy.md)). Auth0 for portal stays under Identity.

**Recommended slices (implement in order)**

| Slice | Name | Outcome |
|-------|------|---------|
| **25a** | Permission model | Coarse portal permissions; map login roles → permissions; `/auth/me` (or tenant context) returns them; portal nav + API gate |
| **25b** | Invite & link | Owner invites by email → pending invite → Auth0 signup/login → `BusinessUser` linked to tenant (+ optional `StaffMember`) |
| **25c** | Scoped practitioner data | Practitioner role sees own calendar / bookings (and only those), not full roster admin |
| **25d** | Manage access UI | Invite / resend / revoke from Staff; show login status on roster card |

---

## 3. In scope

### Two role concepts (do not conflate)

| Concept | Stored on | Purpose |
|---------|-----------|---------|
| **Floor role** (`roleKey`) | `StaffMember` | What they do on the floor / public booking label (stylist, receptionist, …). Unchanged from Sprint 24. |
| **Portal permission role** | `BusinessUser` | What they can open after login (`owner`, `manager`, `receptionist`, `practitioner`, …). |

A person can be bookable **without** a login. A login may exist without being bookable (e.g. accountant). Preferred link: optional `BusinessUser.StaffMemberId`.

### Slice 25a — Permission model

- [x] Define `PortalPermission` keys in Domain + `@adeni/shared` (same strings API ↔ clients).
- [x] Define `PortalPermissionRole` templates (owner / manager / receptionist / practitioner / accountant / ops) mapping → permission sets.
- [x] Extend `BusinessUser.Role` from free string `"owner"` to the permission-role set (migrate existing rows → `owner`).
- [x] `GET` profile / tenant context returns `permissionRole` + `permissions[]` (+ `staffMemberId` when linked).
- [x] Portal shell: filter nav (and route guards) by permissions **and** existing business-type capabilities.
- [x] API: authorize mutating / sensitive tenant routes by permission (not only `AdeniRoles.Business`).

**Initial permission keys (coarse — expand later)**

| Key | Meaning |
|-----|---------|
| `portal.overview` | Dashboard |
| `portal.bookings` | All bookings inbox |
| `portal.bookings.self` | Own assigned bookings / own staff calendar only |
| `portal.messages` | In-app / WhatsApp notes |
| `portal.quotes` | Quote inbox (still gated by business capability) |
| `portal.services` | Service catalog |
| `portal.staff` | Roster CRUD, hours/leave for others, invites |
| `portal.hours` | Business weekly hours |
| `portal.locations` | Locations |
| `portal.public_page` | Public page editor |
| `portal.profile` | Business profile / verification |
| `portal.payments` | Payment links / ledger |
| `portal.plan` | SaaS plan / billing |
| `portal.staff.self` | Edit own leave / view own calendar (practitioner) |

**Default templates**

| Permission role | Permissions (summary) |
|-----------------|----------------------|
| `owner` | All |
| `manager` | All except `portal.plan` (billing stays owner-only in V1) |
| `receptionist` | overview, bookings, messages, quotes?, services (read/write?), hours (read?) — **write bookings + messages**; no staff invite, no plan, no profile verification |
| `practitioner` | `portal.bookings.self`, `portal.staff.self`, optional `portal.messages` (threads for own bookings) |
| `accountant` | overview (usage?), `portal.payments`, `portal.plan` read-only if we add it — else payments only |
| `ops` | services, hours, locations, public_page — no bookings/plan |

Exact matrices live in `@adeni/shared` as a single source of truth; adjust in review before coding.

### Slice 25b — Invite & link

- [x] Owner/manager with `portal.staff` can **Invite to portal** from a staff member (or “access only” invite without staff).
- [x] Persist `StaffPortalInvite`: tenant, email, permission role, optional `staffMemberId`, token hash, expiry, status (`pending` / `accepted` / `revoked` / `expired`).
- [x] Send invite email (reuse notification/email port if present; else queue + log in Development).
- [x] Accept path: user signs up / logs in with Auth0 → sync with invite token (or Auth0 `app_metadata.invite_token` / org invite) → create/update `BusinessUser` for **existing tenant**, set role, link `StaffMemberId`, mark invite accepted.
- [x] **Hard rule:** business Auth sync must **not** create a new `Tenant` unless this is true first-time **owner onboarding** (today’s register flow). Invited staff → fail closed if no matching invite / existing membership.
- [x] Auth0: document Organization **or** invite-token + Management API approach in [auth0-setup.md](../auth0-setup.md). Prefer **invite token in Adeni** for V1 (less Auth0 org complexity); stamp `tenant_id` + `permission_role` into `app_metadata` on accept when Management API is available.
- [x] Dev: seed invite + `X-Dev-Auth0-Sub` path that attaches a second business user to Lekki Cuts without new tenant.

### Slice 25c — Scoped practitioner data

- [x] Bookings list / staff calendar APIs honor `portal.bookings.self`: filter by linked `StaffMemberId`.
- [x] Cross-staff calendar / deactivate / invite → `portal.staff` only; own calendar/leave via `portal.staff.self`.
- [x] Portal: practitioner landing = own calendar (`/my-calendar`); roster admin hidden.

### Slice 25d — Manage access UI

- [x] Staff card / edit: login status (`No access` / `Invite pending` / `Active`), Invite / Resend / Revoke.
- [x] Cannot revoke last `owner`; cannot demote self if sole owner.
- [x] Audit: invite, accept, revoke, role change (tenant audit or existing admin-style log for business mutations — at least structured Warning/Information logs with `PiiMasker` on email).

---

## 4. Out of scope

| Topic | Why |
|-------|-----|
| Payroll, tips, commissions, clock-in | Unchanged — Phase 4+ / finance |
| Fine-grained field-level ACL / custom permission builder UI | Templates only in 25 |
| Moving `StaffMember` out of Booking schema | Still bookable entity; Identity only stores login link |
| Auth0 Organizations multi-tenant SSO polish | Optional later; invite-token first |
| Flutter/mobile staff app login | Portal Angular first |
| Customer (discover) accounts as staff | Separate Auth0 apps / roles |
| MFA step-up for staff | Owner/admin MFA policy unchanged; staff MFA later |

---

## 5. API contract

### Profile / session

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| existing | `GET /api/v1/auth/me` (or tenant context) | Business JWT | Add `permissionRole`, `permissions[]`, `staffMemberId?` |

### Invites

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/v1/tenant/staff/{id}/invite` | Business + `portal.staff` | Body: `{ "email", "permissionRole"? }` — default role from floor `roleKey` mapping |
| `POST` | `/api/v1/tenant/access/invites` | Business + `portal.staff` | Access-only invite (no staff member) |
| `GET` | `/api/v1/tenant/access/invites` | Business + `portal.staff` | List pending/accepted |
| `POST` | `/api/v1/tenant/access/invites/{id}/resend` | Business + `portal.staff` | New token / email |
| `DELETE` | `/api/v1/tenant/access/invites/{id}` | Business + `portal.staff` | Revoke pending |
| `POST` | `/api/v1/auth/accept-staff-invite` | Authenticated (any) | Body: `{ "token" }` — attaches user to tenant |
| `PATCH` | `/api/v1/tenant/access/users/{businessUserId}` | Business + `portal.staff` | Change permission role / unlink |
| `DELETE` | `/api/v1/tenant/access/users/{businessUserId}` | Business + `portal.staff` | Revoke login (soft: clear Auth0 link or mark disabled) |

**Errors (stable codes):** `staff.invite_invalid`, `staff.invite_expired`, `staff.invite_email_mismatch`, `auth.business_access_denied`, `staff.last_owner`, `permission.denied`.

**Request / response (invite create)**

```json
{
  "email": "fela@lekki.cuts",
  "permissionRole": "practitioner"
}
```

```json
{
  "inviteId": "…",
  "email": "f***@lekki.cuts",
  "permissionRole": "practitioner",
  "staffMemberId": "…",
  "status": "pending",
  "expiresAt": "2026-10-17T00:00:00Z"
}
```

**Floor → default permission role (invite prefill)**

| Floor `roleKey` | Default permission role |
|-----------------|-------------------------|
| stylist, barber, nail_tech, esthetician, therapist, instructor, trainer | `practitioner` |
| receptionist | `receptionist` |
| manager, supervisor | `manager` |
| accountant | `accountant` |
| inventory_manager, marketing, hr, admin_staff, other | `ops` |

Owner remains a register/onboarding path, not an invite default.

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `Identity/BusinessUser` (+ `StaffMemberId?`, disabled flag); `StaffPortalInvite`; permission role constants; `ErrorCodes` |
| Application | DTOs; `IStaffAccessService` (or extend Identity + Staff ports); auth profile DTO permissions |
| Infrastructure | EF config + SqlPackage scripts; AuthSync invite-safe path; invite email; permission checks helper |
| Api | Tenant access/invite controllers; authorize filters / policies per permission; update `auth/me` |
| Tests | Auth sync no-new-tenant for invite; permission denial; invite accept; practitioner booking filter; last-owner guard |
| `packages/shared` | Permission keys, role templates, Zod, i18n error strings |
| `packages/api-client` | Invite + access methods |
| `apps/portal` | Nav + route guards by permission; Staff invite UI; practitioner home |

Follow [architecture.md](../architecture.md): Booking stays roster/hours; Identity owns login membership.

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Invites and `BusinessUser` are `ITenantEntity`; accept binds to invite’s `TenantId` only |
| **Auth / roles** | Platform role stays `business`; **permission role** is additional claim or loaded from DB each request (DB source of truth for V1 — avoid stale JWT permission sets) |
| **Cache** | Invalidate nothing heavy; optional short TTL on permission resolution keyed by `businessUserId` |
| **Audit** | Invite / accept / revoke / role change — structured logs + optional `audit_logs` if tenant audit table exists; mask email |
| **PII** | Invite email masked in API list responses and logs (`PiiMasker`) |
| **Observability** | Log invite accepted / permission denied with correlation id |
| **Auth sync** | **Breaking behavior change** for “unknown business Auth0 user”: must not spawn tenants except owner onboarding |

---

## 8. Tests

- [ ] Unit: permission template matrix; floor `roleKey` → default permission role
- [ ] Unit: AuthSync invited user attaches to tenant; unknown business user without invite → forbidden (no tenant create)
- [ ] Integration: invite → accept → `/auth/me` permissions; nav-relevant keys present
- [x] Integration: practitioner cannot `GET` other staff calendar; can get own (API `CanAccessStaffMember` + portal `staffCalendarGuard`)
- [ ] Integration: receptionist denied `portal.plan` / staff invite
- [x] Integration: cannot revoke last owner
- [x] Self-only bookings accept/reject fail closed when `StaffMemberId` is missing
- [ ] Tenant cross-access denial on invite accept with forged tenant

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [ ] Owner invites Baba Fela → Fela logs in → sees practitioner (or chosen) nav only
- [ ] Floor role “Manager” still shows on public booking; portal permission role controls screens
- [ ] Staff without invite still appear on roster and Discover; no login
- [ ] Invited login never creates a second tenant
- [ ] Revoking access blocks portal API with `auth.business_access_denied` / `permission.denied`
- [ ] Dark mode + light mode invite UI usable (shared field tokens)
- [ ] Spec + [auth0-setup.md](../auth0-setup.md) document the accept flow for staging

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| Auth0 Organizations vs Adeni invite token? | **Recommend invite token for V1**; revisit Orgs when multi-location SSO matters |
| Can one Auth0 user belong to multiple tenants? | **V1: one business membership**; multi-tenant switcher later |
| Should `manager` edit plan/billing? | **No** — owner only |
| Practitioner message access? | **Default on** for threads tied to their bookings if cheap; else messages permission off until 25c+ |
| Move staff table out of `booking` schema? | **No in 25** — optional later Identity/HR schema for employment fields only |

---

## 11. Implementation note (for play 2)

Do **not** start coding until this draft is approved. When approved: implement **25a → 25b → 25c → 25d**, keep diffs minimal, and update [sprints.md](../sprints.md) status per slice.
