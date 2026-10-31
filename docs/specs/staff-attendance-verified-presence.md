# Spec: Staff Attendance, Verified Presence & On-Premises Sensitive Data

| Field | Value |
|-------|-------|
| **Program** | Staff Attendance + Verified Presence + On-Premises Sensitive Customer Data |
| **Status** | **Approved** |
| **Created** | 2026-10-31 |
| **Approved** | 2026-10-31 |
| **Authority** | [product-direction.md](../product-direction.md), [AGENTS.md](../../AGENTS.md) |

---

## 1. Current architecture (audit)

### 1.1 Staff roster vs portal identity (two role systems)

| Concept | Storage | Purpose |
|---------|---------|---------|
| **Floor role** | `StaffMember.RoleKey` (`StaffRoleKeys`) | Discovery, booking eligibility, marketing labels |
| **Portal RBAC** | `BusinessUser.Role` (`BusinessPermissionRoles`) | Business app permissions (`business.*`) |

**Key files:** `src/Adeni.Domain/Booking/StaffMember.cs`, `src/Adeni.Domain/Identity/BusinessUser.cs`, `BusinessPermissions` / `BusinessPermissionRoles` in `src/Adeni.Domain/Identity/BusinessPermissions.cs`, mirror `packages/shared/src/business-permissions.ts`.

**Link:** `BusinessUser.StaffMemberId` (optional). Invite flow: `StaffAccessService`, `POST /api/v1/auth/accept-staff-invite`, roster invite `POST /api/v1/tenant/staff/{id}/invite`.

**Gaps relevant to this program:**

- One `Auth0Sub` → one `BusinessUser` (single tenant membership today).
- Owner may have no `StaffMemberId`; practitioners often do.
- Unknown legacy `BusinessUser.Role` **normalizes to owner** (full permissions) — presence/attendance must not widen that behavior.

### 1.2 Business locations & coordinates

**Entity:** `BusinessLocation` — `Latitude`, `Longitude`, `TimeZoneId`, `IsPrimary`, `IsActive` (`tenancy.business_locations`).

**No geofence fields today.** Coordinates used for discovery/markets, not staff verification.

**Multi-location scoping:** `TenantLocationScope` + `ITenantLocationScopeService` — bookings, queue, quotes, staff lists filter by `BusinessLocationId`; primary default when `multiLocationReporting` entitlement is false.

**Staff branch assignment:** `StaffMember.BusinessLocationId` optional — `null` means “all branches” for **availability**; list APIs with a branch filter use exact match (null staff omitted when filtering).

**Gap:** DACPAC `staff_members` has no FK to `business_locations` (EF configures relationship).

### 1.3 Schedule vs attendance (existing)

| Concern | Exists | Notes |
|---------|--------|-------|
| Business weekly hours | `WeeklyAvailability` | Per `BusinessLocationId` |
| Staff weekly hours | `StaffWeeklyAvailability` | Intersected with business hours in booking internals |
| Leave | `StaffLeave` | Warn-only booking conflicts; no auto-cancel |
| Team / my calendar | `StaffService` calendar APIs | Sum of **booking durations** as “hours worked” — not timeclock |
| External calendar busy | `calendar.*` | Subtracts from slots; unrelated to attendance |

**Explicitly deferred in product:** clock-in / time tracking (`docs/specs/sprint-24-staff-ops.md`, `docs/sprints.md`). **No attendance, presence, or geofence code exists.**

### 1.4 Bookings & staff assignment

`BookingRecord.StaffMemberId` nullable (“any available”). Practitioner scope via `business.bookings.self` + `PortalAccessHttpContext.ResolveBookingsStaffScope`. No post-create staff reassignment API found.

### 1.5 Business RBAC (today)

Permissions (excerpt): `business.overview`, `business.bookings`, `business.bookings.self`, `business.messages`, `business.staff`, `business.staff.self`, `business.finance`, `business.retention`, … — see `BusinessPermissions.All`.

Role templates in `BusinessPermissionRoles.PermissionsFor` — **no attendance permissions yet**.

### 1.6 Customer / sensitive data surfaces (Business portal)

| Area | Routes (representative) | Controller / service |
|------|-------------------------|----------------------|
| Customer list/detail | `GET /api/v1/tenant/customers`, `GET …/{id}` | `TenantCustomersController`, `TenantCustomerService` |
| F1 profile / activity | `GET …/customers/{id}/profile`, `GET …/activity` | `TenantCustomerProfileController`, `CustomerProfileQueryService` |
| Segments | `GET …/customer-segments/*`, `POST …/segments/batch` | `CustomerSegmentsController` |
| Bookings | `GET/POST …/tenant/bookings/*` | `TenantBookingsController`, `BookingService` |
| Intake (staff) | `GET …/intake/bookings/{id}`, `GET …/forms/{assignmentId}/submission` | `IntakeReviewController`, `IntakeSubmissionService` |
| Retention | `/api/v1/tenant/retention/*`, memberships, loyalty | Retention controllers |
| Messaging | `/api/v1/tenant/messages/*` | `TenantMessagesController`, `ConversationService` |
| Finance | `/api/v1/tenant/finance/*`, `/api/v1/finance/documents/*`, reporting previews | Commerce, billing, reporting |

**PII today:** list/detail expose **masked phone + full email** (`PiiMasker.MaskPhone`). Bookings list: masked phone only. Intake submission answers and message bodies are **full content** to authorized staff. Customer statement PDF: masked phone.

**Admin-only:** verification documents, customer delete, intake export (MFA) — not Business portal.

### 1.7 Entitlements (today)

Catalog: `packages/shared/src/data/plan-entitlements.json`. Authority: `IEntitlementsService` / `EntitlementsService`.

Staff-adjacent flags: `maxPractitioners`, `staffPerformanceAnalytics`, `multiLocationReporting`, `advancedPermissions`. **No attendance or on-premises flags.**

### 1.8 Auditing (today)

Single table `admin.audit_logs`. Admin mutations via `AuditMiddleware` on `/api/v1/admin/*`. Portal/service writes: booking transitions, `tenant.reporting.exported`, `staff.*` invites, `retention.*`, `security.cross_tenant_denied`, etc. (`AuditActions.cs`, `docs/audit-runbook.md`).

**No business attendance or presence audit actions yet.**

### 1.9 Reporting (today)

Tenant exports via `GET /api/v1/tenant/reporting/exports/{reportKey}` — bookings register, sales, compensation, staff roster, customers, retention MRR, etc. **No timesheet / attendance reports.**

### 1.10 Geo helpers

**Backend:** `GeoDistance.HaversineKm` in `src/Adeni.Infrastructure/Discovery/GeoDistance.cs` (discovery only).

**Shared:** `packages/shared/src/geo.ts`, `market-resolver.ts` — client/market UX, not tenant enforcement.

**Recommendation:** Move or duplicate Haversine into **Application** (e.g. `IGeoDistanceCalculator`) so attendance module does not depend on Discovery infrastructure.

### 1.11 Business frontend routing

`apps/business/src/app/app.routes.ts` — dashboard, bookings, customers, retention, queue, calendar/my-calendar, staff, locations, availability, messages, finance, settings/plan. Guards: `businessPermissionGuard`, `businessAuthGuard`, practitioner calendar scope.

**No attendance or presence UI.**

### 1.12 Realtime

SignalR used for messaging. **No attendance realtime requirement for V1** — polling/refetch sufficient for manager timesheet views.

---

## 2. Proposed domain boundaries

Three **separate** capabilities with explicit dependencies:

```text
┌─────────────────────┐
│ Staff Attendance    │  Immutable events: ClockIn, BreakStart, BreakEnd, ClockOut
│ (operational time)  │  Optional geofence at event time; manager override; corrections
└──────────┬──────────┘
           │ may emit
           ▼
┌─────────────────────┐
│ Verified Presence   │  Short-lived proof: "was at Location X recently"
│ (security signal)   │  Created by clock-in / explicit verify; expires; not GPS streaming
└──────────┬──────────┘
           │ consulted by
           ▼
┌─────────────────────┐
│ On-Premises Access  │  Optional tenant policy: sensitive customer APIs need valid presence
│ (data gate)         │  Never replaces JWT, tenant, RBAC, entitlements, location scope
└─────────────────────┘
```

**Dependency rules:**

- Attendance **does not require** on-premises policy to be enabled.
- Presence **can** be created from attendance events but **must expire** independently of “still clocked in.”
- Sensitive-data enforcement **only** adds a check after existing authorization succeeds.
- Clock-out **always** invalidates presence derived from that shift at that location.

**Module placement (recommended):**

| Bounded context | Schema (SQL) | Rationale |
|-----------------|--------------|-----------|
| Attendance events & corrections | `booking` | Tied to `StaffMember`, same lifecycle as roster/leave |
| Verified presence sessions | `identity` | Tied to `BusinessUser` auth identity |
| Location security policy | `tenancy` | Per tenant / per location business settings |
| Geofence config fields | `tenancy.business_locations` | Coordinates already there |

Application ports: `IStaffAttendanceService`, `IVerifiedPresenceService`, `IOnPremisesSensitiveAccessPolicy` (thin orchestrator calling presence + tenant policy).

---

## 3. Attendance data model

### 3.1 Principles

- **Append-only event log** as source of truth; derived “current shift state” computed from latest events (or materialized view updated in same transaction).
- **Corrections** add new events (`CorrectionApplied`, `VoidEvent`) referencing prior event IDs — never delete rows.
- All rows: `TenantId` + `ITenantEntity` query filters.

### 3.2 Proposed tables (DACPAC)

**`booking.staff_attendance_events`**

| Column | Type | Notes |
|--------|------|-------|
| `Id` | uniqueidentifier | PK |
| `TenantId` | uniqueidentifier | FK → tenants |
| `BusinessLocationId` | uniqueidentifier | Required for event (staff picks/checks in at a branch) |
| `StaffMemberId` | uniqueidentifier | FK composite `(TenantId, StaffMemberId)` → staff_members |
| `BusinessUserId` | uniqueidentifier | Actor who recorded (portal user) |
| `EventType` | nvarchar(32) | `clock_in`, `break_start`, `break_end`, `clock_out`, `correction`, `void` |
| `OccurredAt` | datetimeoffset | Business-meaningful time (usually UTC stored, display in location TZ) |
| `RecordedAt` | datetimeoffset | Server receipt time |
| `LocationVerificationStatus` | nvarchar(32) | `not_required`, `verified`, `failed`, `override`, `skipped_unavailable` |
| `DistanceFromLocationMeters` | int null | Computed server-side |
| `ReportedAccuracyMeters` | int null | From client |
| `VerificationMethod` | nvarchar(32) | `geofence`, `override`, `none` |
| `ManagerOverrideByBusinessUserId` | uniqueidentifier null | |
| `ManagerOverrideReason` | nvarchar(500) null | Required when override |
| `RelatedEventId` | uniqueidentifier null | For correction/void |
| `CorrectionReason` | nvarchar(500) null | |
| `ClientLocationCapturedAt` | datetimeoffset null | Device fix time (not retained long-term in audit payload) |

Indexes: `(TenantId, StaffMemberId, OccurredAt DESC)`, `(TenantId, BusinessLocationId, OccurredAt)`.

**Optional V1.1:** `booking.staff_attendance_day_summaries` (materialized) for manager list performance — defer until volume requires.

### 3.3 State machine (derived)

For each `(TenantId, StaffMemberId)` on a calendar day (location TZ):

```text
NotWorking → (ClockIn) → Working → (BreakStart) → OnBreak → (BreakEnd) → Working → (ClockOut) → NotWorking
```

Reject invalid transitions (e.g. double ClockIn without ClockOut) with stable error `attendance.invalid_transition`.

### 3.4 Schedule comparison (future reporting only)

Read `StaffWeeklyAvailability` + `StaffLeave` for **display** (“Scheduled 09:00–17:00”). No scoring, no discipline automation.

---

## 4. Verified Presence design

### 4.1 Purpose

Answer: **“Did this BusinessUser recently prove physical presence at Location X?”** — not continuous tracking.

### 4.2 Proposed table **`identity.verified_presence_sessions`**

| Column | Notes |
|--------|-------|
| `Id`, `TenantId` | |
| `BusinessUserId` | Portal actor |
| `StaffMemberId` | Optional mirror for reporting |
| `BusinessLocationId` | **Scope anchor for sensitive data** |
| `VerifiedAt`, `ExpiresAt` | |
| `VerificationMethod` | `geofence`, `attendance_clock_in`, `manual_verify`, `manager_override` |
| `DistanceFromLocationMeters`, `ReportedAccuracyMeters` | Nullable metadata |
| `InvalidatedAt`, `InvalidationReason` | clock_out, logout, location_switch, admin_invalidate, expired |
| `SourceAttendanceEventId` | Optional link |

**Active session:** `InvalidatedAt IS NULL AND ExpiresAt > now`.

### 4.3 Storage strategy (hybrid recommended)

| Layer | Role |
|-------|------|
| **SQL row** | Audit trail, dispute resolution, manager dashboards |
| **Redis cache** (optional) | Key `tenant:{tenantId}:presence:{businessUserId}` → `{ sessionId, locationId, expiresAt }` TTL = remaining validity; **cache miss falls back to SQL** |

Avoid storing raw lat/lng in SQL long-term (see §11).

### 4.4 Creation triggers

1. **Successful geofenced ClockIn** → create session, `VerificationMethod = attendance_clock_in`.
2. **Explicit `POST …/presence/verify`** (same geofence rules) → for staff already working without re-clocking.
3. **Manager override** on attendance or presence → session flagged `manager_override`, still location-scoped.

### 4.5 Expiry (recommended defaults)

Analysis:

- Long appointments (spa) may exceed 90 min without leaving desk — staff may need **re-verify** or **extend** via lightweight verify call, not constant GPS.
- Risk: clock in then leave — presence must **not** last all day.

**Recommended V1 defaults (tenant-configurable within bounds):**

| Setting | Default | Range |
|---------|---------|-------|
| `PresenceValidityMinutes` | **90** | 30–180 (Business tier may widen max) |
| Max client fix age at verify | **90 seconds** | — |
| Max reported accuracy to accept fix | **100 m** | Reject worse as `staff.location_verification_failed` (retry) |

**Sliding extension:** optional `POST …/presence/verify` refreshes `ExpiresAt = min(now + validity, shift end)` — **cap extension** to once per 30 minutes to prevent “verify once at 9am, work remotely until 5pm.”

### 4.6 Invalidation

Mandatory:

- ClockOut at location (or any clock out for that staff day)
- New session at **different** `BusinessLocationId` (previous session invalidated)
- Explicit logout (Auth0 sign-out hook or app `POST …/presence/invalidate` best-effort)
- `ExpiresAt` elapsed

Optional V1: manager “End shift” action.

**Do not rely on JWT expiry alone.**

---

## 5. Geofence design

### 5.1 Configuration (per `BusinessLocation`)

Add columns to `tenancy.business_locations`:

| Column | Default | Notes |
|--------|---------|-------|
| `AttendanceGeofenceEnabled` | `0` | Off until Professional+ entitlement + owner enables |
| `AttendanceGeofenceRadiusMeters` | `100` | Configurable **50–250 m** (UI slider + validation) |

Tenant-level master switch in `tenancy.location_security_policies` (see §7) can require geofence for attendance when policy enabled.

### 5.2 Server-side verification

Client sends **only:**

```json
{
  "latitude": 6.5244,
  "longitude": 3.3792,
  "accuracyMeters": 35,
  "capturedAt": "2026-10-31T14:04:12Z"
}
```

Server:

1. Reject missing/invalid coordinates, stale `capturedAt` (> 90s skew from server), accuracy > max threshold.
2. Load location lat/lng; if missing → `attendance.location_not_configured` (allow override path, block silent pass).
3. `distanceM = HaversineMeters(...)` via shared calculator.
4. **Accept if** `distanceM <= radiusM + accuracyM` (GPS uncertainty buffer — handles 65 m distance, 50 m radius, 40 m accuracy).
5. **Reject if** `distanceM > radiusM + accuracyM` → failed verification (override available).

Record `DistanceFromLocationMeters`, `ReportedAccuracyMeters`, status — **not** raw coordinates in persistent store (transient in request only).

### 5.3 Geofence disabled

When `AttendanceGeofenceEnabled = false`: attendance events use `LocationVerificationStatus = not_required`; presence for sensitive data may still be required if on-premises policy enabled — then **explicit verify** at location coordinates still needed OR policy allows “presence via clock-in without geofence” (product choice: **if geofence off but on-premises policy on, require manual manager attestation or honor clock-in without GPS** — recommend **clock-in at location without GPS satisfies presence when geofence disabled**, documented risk).

### 5.4 Multi-location

Attendance event **always** records which `BusinessLocationId` was selected (default: user’s primary assigned staff location, or prompt if multi-branch).

Presence session is **location-specific** (see §6 matrix).

---

## 6. Sensitive-data classification

Legend: **S** = on-premises protected when policy enabled · **O** = operational (no presence) · **A** = admin-only (out of scope for Business) · **D** = product decision

| Surface | Route / feature | Class | Presence when policy ON |
|---------|-----------------|-------|-------------------------|
| Dashboard summary | `GET /tenant/dashboard/*` | O | No |
| My / team calendar | staff calendar APIs | O | No |
| Bookings list (status, time, masked phone) | `GET /tenant/bookings` | O | No |
| Booking actions (check-in, complete) | booking mutations | O | No* |
| Queue board | queue APIs | O | No |
| Services / hours / locations settings | tenant config | O | No |
| Staff roster (names, roles, no customer PII) | `GET /tenant/staff` | O | No |
| Customer list (name, masked phone, email) | `GET /tenant/customers` | **S** | **Yes** — email + identity |
| Customer detail (email, masked phone) | `GET /tenant/customers/{id}` | **S** | Yes |
| Customer statement PDF | `GET …/statement` | **S** | Yes |
| F1 profile aggregates | `GET …/profile` | O / partial | No for visit counts; **S** if response adds contact fields later |
| F1 activity feed | `GET …/activity` | **D** | Finance/messaging slices **S** when present |
| Segments batch | customer segments | O | No (derived labels, no raw PII export) |
| Intake submission answers | intake review GET | **S** | Yes |
| Intake templates (structure) | template CRUD | O | No |
| Messaging conversation list | metadata | O | No |
| Message bodies / attachments | messages GET/POST | **D** | See §6.1 |
| Loyalty account / adjust | retention loyalty | **S** | Yes (PII-adjacent balances + history) |
| Packages / memberships customer views | retention | **S** | Yes |
| Gift cards purchased | customer gift metadata | **S** | Yes |
| Billing documents / previews | finance documents | **S** | Yes |
| Pay links / commerce | commerce APIs | O | No |
| Compensation export | reporting | **S** | Yes |
| Customer export CSV | `tenant-customers` report | **S** | Yes |
| Verification documents | admin | A | N/A |
| WhatsApp deep link generation | if exposes raw phone | **S** | Yes |

\*Booking **notes** field (if/when exposed) → **S**.

### 6.1 Messaging (V1 recommendation)

| Option | Summary |
|--------|---------|
| A | Messages **unrestricted** by presence — operational comms, already entitlement + RBAC gated |
| B | Message content **requires** presence |
| C | Business-configurable toggle |

**Recommend V1 = Option A** (simplest; avoids blocking urgent remote replies). Document Option C for Business tier later if customers demand it.

### 6.2 Multi-location access rule (recommended)

**Location-specific presence (strong default):**

When policy enabled, sensitive access to customer context is allowed only if:

1. Valid presence at `BusinessLocationId = L`, AND  
2. Customer record access is **scoped to location L** OR tenant has `multiLocationReporting` + user has `business.attendance` (manager) with explicit branch selected.

**Implications:**

| Persona | Behavior |
|---------|----------|
| Practitioner at Branch A | Sensitive data for customers **primarily seen at A** (bookings/queue at A); cross-branch customer profile requires verify at B or manager role |
| Floating staff | Re-verify when switching branch |
| Centralized reception (multi-location entitlement) | Manager/receptionist with team attendance permission + branch picker |
| Owner | Full tenant RBAC unchanged; presence still required for **S** endpoints unless emergency override policy (defer) |

Align with existing `CustomerProfileScope.LocationId` and booking location filters.

---

## 7. On-premises policy (V1)

### 7.1 Settings

**Table `tenancy.location_security_policies`** (one row per tenant; optional per-location override later):

| Column | Default |
|--------|---------|
| `TenantId` | PK |
| `RequirePresenceForSensitiveCustomerData` | `false` |
| `PresenceValidityMinutes` | `90` |
| `AllowManagerPresenceOverride` | `true` |
| `UpdatedAt`, `UpdatedByBusinessUserId` | audit |

**Single business-facing toggle (V1):**

> “Restrict sensitive customer information to staff who are on site.”

Help text links to classified data list (§6). No per-endpoint toggles in V1.

### 7.2 Enforcement strategy

1. **`IOnPremisesSensitiveAccessPolicy.RequiresPresence(routeKey or resourceType)`** — static map maintained alongside controllers (or attributes `[RequiresOnPremisesPresence(SensitiveResourceKind.CustomerContact)]`).
2. Middleware or action filter **after** `RequiresBusinessPermission` succeeds:
   - Load tenant policy; if off → continue.
   - If on → `IVerifiedPresenceService.GetActiveSession(tenantId, businessUserId)` must match required location scope for the request.
3. Failure → **403** with RFC 7807 code `staff.presence_required` or `staff.presence_expired` (distinct from `permission.denied`).

**Frontend:** route guards may **prefetch** presence UX; they are not authoritative.

### 7.3 Entitlement gate

Policy UI and enforcement no-op unless entitlement `onPremisesSensitiveData` (Professional+).

---

## 8. RBAC matrix (proposal)

New permissions (sync Domain + `packages/shared`):

| Permission | Meaning |
|------------|---------|
| `business.attendance.self` | Record own attendance; verify own presence |
| `business.attendance` | Team timesheets, exceptions, overrides, corrections, policy read |
| `business.attendance.manage` | Edit policy (owner/manager) — or fold into `business.profile` / settings |

### 8.1 Role × capability (recommended)

| Role | Self attendance | Team attendance | Corrections | Manager override | Configure policy | Sensitive data (RBAC) | Presence required if policy on |
|------|-----------------|-----------------|-------------|------------------|------------------|----------------------|----------------------------------|
| Owner | Yes | Yes | Yes | Yes | Yes | Full | Yes |
| Manager | Yes | Yes | Yes | Yes | Yes | Full | Yes |
| Receptionist | Yes | Read today (optional V1) | No | No | No | Bookings + retention | Yes |
| Practitioner | Yes | No | No | No | No | Self bookings | Yes |
| Accountant | No | No | No | No | No | Finance only | Yes for finance **S** exports |
| Ops | No | No | No | No | No | No customer APIs | N/A |

Invite defaults: map floor role via existing `DefaultForFloorRole`; add attendance.self to practitioner/receptionist/manager templates.

**Override permission:** require `business.attendance` + entitlement; reason min 10 chars; audited.

---

## 9. Entitlements (commercial, non-final)

Proposed **minimal** new flags in `plan-entitlements.json`:

| Flag | Starter | Professional | Business |
|------|---------|--------------|----------|
| `staffAttendance` | false | true | true |
| `attendanceGeofence` | false | true | true |
| `onPremisesSensitiveData` | false | true | true |
| `multiLocationAttendance` | false | false | true |
| `attendanceReporting` | false | false | true (uses `advancedReports` or dedicated) |

Avoid explosion: geofence bundled with Professional attendance; advanced attendance exports only on Business.

---

## 10. UX (Business app)

### 10.1 Practitioner — “My attendance”

- Nav entry under **Staff** or home card (mobile-first layout later).
- Shows today’s **scheduled** window (read-only from weekly availability).
- Primary actions: Clock in → Start break → End break → Clock out.
- Location picker when tenant has 2+ branches.
- Browser geolocation prompt **only** on clock-in/verify tap (not on app load).

### 10.2 Manager — “Attendance”

- Subviews: Today, Timesheets (week), Exceptions (failed geofence, missing clock-out), Overrides pending (if async approval deferred, V1 = immediate override by manager on behalf).
- Export via future `attendance-register` report key (Phase E).

### 10.3 Sensitive data blocked

- Inline panel when API returns `staff.presence_required`:

  > This information is restricted by your business’s security policy.  
  > Verify that you’re at **[Location name]** to continue.

- Actions: **Verify location** (one-shot GPS), **Request manager override** (optional message).

### 10.4 Error / override states

| State | UX |
|-------|-----|
| GPS denied | Explain; non-sensitive app usable; retry button |
| Verification failed | Show distance band (optional “about Xm away” without map tracking) |
| Override submitted | Toast + audit confirmation |
| Presence expired mid-page | Soft banner + re-verify CTA |

---

## 11. Privacy & retention

### 11.1 Collected at event time (transient)

- Lat/lng fix — **process in memory only** for distance calculation.

### 11.2 Persisted (recommended)

- `DistanceFromLocationMeters`, `ReportedAccuracyMeters`, `LocationVerificationStatus`, `VerificationMethod`, timestamps, location/tenant/user IDs.

### 11.3 Not stored

- Continuous tracks, background location, “current employee map,” movement history.

### 11.4 Retention

- Attendance events: **24 months** operational default (align with reporting needs; legal review for employment records).
- Presence sessions: **90 days** after invalidation (metadata only).
- Raw coordinates: **do not persist** in V1; if dispute requires, optional encrypted 7-day hold — **defer** unless legal asks.

### 11.5 Audit logs

Use action codes (§12); **no GPS coordinates** in `admin.audit_logs` payload.

---

## 12. API contract (proposed)

All routes: `/api/v1/tenant/...`, JWT + `X-Tenant-Id`, `[RequiresBusinessPermission]`, `Result` + stable `code` in shared `ErrorCodes` / i18n.

### 12.1 Attendance

| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| GET | `/attendance/me/today` | `attendance.self` | Current state + today’s events |
| GET | `/attendance/me/schedule-today` | `attendance.self` | Read-only scheduled window |
| POST | `/attendance/me/events` | `attendance.self` | Body: `eventType`, `businessLocationId`, optional `locationFix` |
| GET | `/attendance/team` | `attendance` | Query: `date`, `locationId` |
| GET | `/attendance/staff/{staffMemberId}` | `attendance` or self | History range |
| POST | `/attendance/staff/{staffMemberId}/override` | `attendance` | Manager override failed clock-in |
| POST | `/attendance/staff/{staffMemberId}/corrections` | `attendance` | Append correction event |

**Request `locationFix` (optional):**

```json
{
  "latitude": 0,
  "longitude": 0,
  "accuracyMeters": 25,
  "capturedAt": "2026-10-31T12:00:00Z"
}
```

### 12.2 Presence

| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| GET | `/presence/me` | any business user with policy | Active session or null |
| POST | `/presence/verify` | `attendance.self` | Explicit verify + optional fix |
| POST | `/presence/invalidate` | self | Logout / end presence |

### 12.3 Policy (settings)

| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| GET | `/settings/location-security` | `attendance.manage` or owner | |
| PUT | `/settings/location-security` | same | Toggle + validity minutes |

Location geofence radii: extend existing `PUT /api/v1/tenant/locations/{id}` (or PATCH) with geofence fields — entitlement `attendanceGeofence`.

### 12.4 Error codes (add to `ErrorCodes` + `@adeni/shared`)

| Code | When |
|------|------|
| `staff.presence_required` | Policy on, no active session |
| `staff.presence_expired` | Session expired |
| `staff.presence_location_mismatch` | Wrong branch |
| `staff.location_verification_failed` | Out of range / bad fix |
| `staff.location_not_configured` | Branch missing coordinates |
| `attendance.invalid_transition` | State machine violation |
| `attendance.not_entitled` | Plan lacks feature |

---

## 13. Database / DACPAC impact

| Object | Action |
|--------|--------|
| `booking.staff_attendance_events` | **Create** |
| `identity.verified_presence_sessions` | **Create** |
| `tenancy.location_security_policies` | **Create** |
| `tenancy.business_locations` | **Alter** — geofence columns |
| `booking.staff_members` | Optional: add FK to `business_locations` in DACPAC (hygiene) |

EF configurations in `AdeniDbContext`; **no EF migrations** — SqlPackage/DACPAC deploy authority per AGENTS.md.

---

## 14. Tests (mapping to requirements)

Integration tests (representative):

| # | Test focus |
|---|------------|
| 1–3 | Tenant isolation, staff identity binding |
| 4–8 | Geofence on/off, distance, accuracy buffer, stale fix |
| 9–11 | RBAC self vs team vs correction |
| 12–14 | Override + reason + correction append-only |
| 15–20 | Presence lifecycle, expiry, location mismatch, tenant isolation |
| 21 | Customer JWT rejected on attendance routes |
| 22–26 | PII in responses, ordinary routes without presence, stable errors |
| 27 | Multi-location filters |
| 28–30 | DACPAC build, Angular test/build, booking regression suite |

Unit tests: distance policy function (pure), state machine transitions.

---

## 15. Risks

| Risk | Mitigation |
|------|------------|
| GPS inaccuracy (indoor, Lagos urban canyon) | Accuracy buffer formula; override path; coordinate QA in onboarding |
| Browser permission denial | Non-sensitive work continues; manager override |
| GPS spoofing | Server-side distance only; no client `insideGeofence`; rate limits; audit anomalies — **not foolproof** (document limitation) |
| Manager override abuse | Reason required; reporting on override counts; owner review |
| Multi-location ambiguity | Location-scoped presence + explicit branch picker |
| Remote / home visit staff | V1: override only; no dynamic geofence engine |
| Authorization regression | Central policy filter; integration tests on all **S** endpoints |
| Support burden | Clear UX copy; admin doc for fixing business coordinates |
| Privacy perception | Event-based GPS only; no tracking language in UI |
| Owner role normalization bug | Do not expand unknown roles; explicit tests |

---

## 16. Phased implementation recommendation

```text
Phase A — Attendance foundation (no geofence, no sensitive gate)
  DACPAC events table, domain service, self clock API, basic My Attendance UX, RBAC, audit actions

Phase B — Geofenced attendance
  Location columns, server verification, manager override, failed-state UX

Phase C — Verified presence
  Sessions table/cache, verify API, expiry/invalidation tied to clock-out

Phase D — On-premises sensitive-data policy
  Policy table, entitlement, enforcement filter on S endpoints, customer UX gate

Phase E — Reporting & polish
  attendance-register export, manager dashboards, optional SignalR, Flutter-ready hardening
```

**Do not ship Phase D before Phase C.** Phases A+B can ship without enabling sensitive restriction.

---

## Post-approval implementation notes

- Branch from latest `dev`: `feature/staff-attendance-verified-presence`
- Small commits following sequence in program brief
- Implementation branch: `feature/staff-attendance-verified-presence` from latest `dev`
- Phase A begins only after sprint slot is scheduled in `docs/sprints.md`

---

## Out of scope (V1 reaffirmation)

Continuous tracking, beacons, biometrics, payroll/tax, disciplinary scoring, policy DSL, location-only auth, customer access to attendance APIs, automatic punishment for lateness.
