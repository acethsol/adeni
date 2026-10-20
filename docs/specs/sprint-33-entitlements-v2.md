# Spec: Sprint 33 — Entitlements v2 (seed-driven)

> Align API enforcement and portal UX with the commercial entitlement matrix.  
> **Do not implement until this spec is approved.**

| Field | Value |
|-------|-------|
| **Sprint** | 33 — Entitlements v2 (follow-on to 33a pricing + 33b commercial config) |
| **Author** | Engineering |
| **Status** | Done (MVP + Phase 2 portal/shared; follow-up analytics differentiation deferred) |
| **Created** | 2026-10-20 |

---

## 1. Goal (one sentence)

Make **`plan-entitlements.seed.json`** the single source of truth for Starter / Professional / Business capabilities, enforce limits and flags on the API, and drive portal navigation and ops UI from the tenant profile entitlements payload—without legacy “cripple core behavior” gates (Starter **Adeni inbox stays enabled**).

---

## 2. Context

- **Sprint tracker:** [docs/sprints.md](../sprints.md) — Sprint 33 row **33-entitlements-v2**
- **Commercial authority (matrix):** [02_PLAN_ENTITLEMENTS.md](./adeni-subscription-pricing-v1/02_PLAN_ENTITLEMENTS.md)
- **Machine-readable seed:** [plan-entitlements.seed.json](./adeni-subscription-pricing-v1/plan-entitlements.seed.json)
- **Package docs:** [06_SUBSCRIPTION_SERVICE_AND_APIS.md](./adeni-subscription-pricing-v1/06_SUBSCRIPTION_SERVICE_AND_APIS.md), [08_ANGULAR_PLAN_AND_PORTAL_UX.md](./adeni-subscription-pricing-v1/08_ANGULAR_PLAN_AND_PORTAL_UX.md), [10_TESTS_AND_ACCEPTANCE_CRITERIA.md](./adeni-subscription-pricing-v1/10_TESTS_AND_ACCEPTANCE_CRITERIA.md)
- **Related billing work:** [sprint-33a-pricing-launch.md](./sprint-33a-pricing-launch.md), [sprint-33b-admin-commercial-config.md](./sprint-33b-admin-commercial-config.md) (pricing/catalog; entitlements remain SKU-driven, not per-tenant commercial overrides)
- **Module(s):** Subscriptions / Tenancy / Messaging / Booking / Dashboard / Portal

### Current baseline (gap)

| Area | Today | Problem |
|------|--------|---------|
| C# matrix | `SubscriptionEntitlements.ForSku` hard-coded in Application | Drifts from `02_PLAN_ENTITLEMENTS.md` / seed (e.g. **Starter `Messaging: false`**) |
| TS marketing | `PLAN_COMPARISON` in `@adeni/shared` | Shows Starter messaging **false**; reminders/analytics rows don’t match full matrix |
| Enforcement | Bookings (unused monthly cap), locations, messaging | **No** practitioner cap, waitlist, or WhatsApp gates |
| Portal | Nav hides messaging tab via `entitlement: "messaging"`; locations page shows upgrade copy | Ops pages should **hide** gated features; **no upgrade nags** on locations/messages/dashboard |
| Profile payload | `BusinessProfile.entitlements` from onboarding path | Must stay in sync with expanded `IEntitlementsService` |

**Principle (from matrix):** Do not cripple core Adeni behavior to manufacture upgrades. Gate **scale, automation, analytics sophistication, and multi-location complexity**.

---

## 3. In scope

- [x] **Single source of truth** — `packages/shared/src/data/plan-entitlements.json` + `PlanEntitlementsCatalog.cs` (aligned with seed).
- [x] **Expand `TenantEntitlements` + `IEntitlementsService`** — v2 DTO + Zod; effective SKU via `TenantBillingPolicy.ResolveEffectiveSku`.
- [x] **Starter messaging = true** — C#, tests, `PLAN_COMPARISON`, portal inbox.
- [x] **Numeric limits (server-enforced)** — `maxLocations`, `maxPractitioners`; `entitlements.location_limit` / `entitlements.practitioner_limit`.
- [x] **Feature flags (server-enforced where APIs exist today)** — waitlist, WhatsApp prefs, advanced analytics dashboard gates.
- [x] **Tenant profile entitlements payload** — profile + subscription usage include v2 entitlements + location/practitioner usage counts.
- [x] **Portal capability-driven UI** — nav/limits from profile; removed ops-page upgrade nags (locations/messages/dashboard).
- [x] **`PLAN_COMPARISON`** — synced to matrix (Adeni inbox, caps, waitlist, WhatsApp).
- [x] **Tests** — `EntitlementsServiceTests` + `plan-entitlements.test.ts`.

## 4. Out of scope

Explicitly **not** in this spec (per matrix “Future” and product guardrails):

- Memberships / packages / recurring customer products (not built)
- AI receptionist, SMS packs, advanced Insights add-ons
- Full accounting integrations (QuickBooks, Xero, etc.)
- Admin-editable entitlement overrides per tenant (commercial overrides remain **price/trial** only via 33b)
- DB-persisted entitlement catalog versioning (seed file + codegen is enough for V1; defer to admin catalog pattern only if ops need hot edits)
- Entitlement changes mid-request beyond existing billing/trial resolution
- Discover SEO copy beyond plan comparison table sync

---

## 5. Entitlement model (v2)

### 5.1 Seed → runtime mapping

| Seed key (`plan-entitlements.seed.json`) | Matrix row | MVP enforcement | Portal UX |
|------------------------------------------|------------|-----------------|-------------|
| `maxLocations` | Business locations | `EnsureCanAddLocationForTenantAsync` (count vs max; Business `null` = unlimited) | Locations: hide “add second location” affordance when at cap; no “Business plan required” banner on ops view |
| `maxPractitioners` | Staff/practitioners | **New** `EnsureCanAddPractitionerForTenantAsync` on staff create/activate | Staff page: disable add at cap; optional usage line on `/plan` |
| `unlimitedBookings` | Online bookings | Keep **no** monthly cap (remove dead `MonthlyBookingLimit` usage or set always null from seed) | — |
| `messaging` | Adeni inbox | `EnsureCanUseMessagingForTenantAsync` + SignalR hub | Messages nav visible for Starter; inbox tab not entitlement-gated incorrectly |
| `advancedReminders` | Automated reminders | **Follow-up** if reminder templates differ by tier; MVP: document parity “basic” on Starter | — |
| `whatsappIntegration` | WhatsApp integration | Gate tenant WhatsApp link / notification prefs APIs that expose WhatsApp channel | Hide WhatsApp toggles when false |
| `waitlist` | Waitlist | Gate `POST /api/v1/bookings/waitlist` (and portal queue/waitlist UI if present) | Hide waitlist entry points |
| `advancedAnalytics` | Dashboard / Analytics | Existing dashboard widget filter + `TenantDashboardService` analytics endpoints | Dashboard widgets hidden, not “upgrade to Pro” placeholders |
| `customerRetentionAnalytics` | Customer retention analytics | **Follow-up:** gate specific widgets/reports when built | Hide widgets |
| `staffPerformanceAnalytics` | Staff performance analytics | **Follow-up:** gate staff leaderboard widget (partially exists via `analytics` entitlement today) | Align flags with seed |
| `multiLocationReporting` | Multi-location reporting | **Follow-up:** when cross-location reports ship | Hide nav/widgets |
| `prioritySupport` | Priority support | Informational on `/plan` only | — |
| `publicProfile`, `discoverListing`, `invoicesReceipts`, `onlinePayments`, `deposits` | Core commerce | **No tier gate** in V1 (always true in seed) | — |

### 5.2 API contract

Existing routes stay under `/api/v1/`. Extend payloads; avoid breaking renames without deprecation.

| Method | Route | Auth | Change |
|--------|-------|------|--------|
| `GET` | `/api/v1/tenant/business-profile` (or equivalent profile route) | JWT + tenant header | `entitlements` object **v2** shape |
| `GET` | `/api/v1/tenant/subscription` / usage | JWT + `portal.plan` | Include v2 entitlements + **usage limits** (`locationsUsed`, `practitionersUsed`, optional caps) |
| `POST` | Staff / locations / bookings/waitlist / messaging | JWT + permissions | 403/400 with `entitlements.*` when over cap or flag false |

**Optional (MVP if profile already sufficient):** `GET /api/v1/subscription/entitlements` as thin read — only if clients need entitlements without full profile; prefer single profile payload.

**Response shape (illustrative — finalize in `packages/shared`):**

```json
{
  "subscriptionSku": "starter",
  "entitlements": {
    "maxLocations": 1,
    "maxPractitioners": 3,
    "messaging": true,
    "waitlist": false,
    "whatsappIntegration": false,
    "advancedAnalytics": false,
    "customerRetentionAnalytics": false,
    "staffPerformanceAnalytics": false,
    "advancedReminders": false,
    "multiLocationReporting": false,
    "prioritySupport": false,
    "multiLocation": false
  },
  "usage": {
    "locationsActive": 1,
    "practitionersActive": 2
  }
}
```

**Backward compatibility:** Bump Zod schema with new fields; deprecate removed booleans (`basicCalendar`, `staffManagement`, `monthlyBookingLimit`) only after portal/shared consumers updated in same PR.

---

## 6. Phased delivery

| Phase | Scope | Surfaces |
|-------|--------|----------|
| **MVP (33-entitlements-v2)** | Seed loader/codegen; expanded `TenantEntitlements`; Starter messaging fix; location + practitioner enforcement; waitlist + WhatsApp gates where APIs exist; profile + usage payload; portal nav + remove ops-page upgrade copy; sync `PLAN_COMPARISON`; test updates | API, portal, shared |
| **Follow-up** | `advancedReminders` differentiation; retention/staff/multi-location analytics widgets; practitioner usage on dashboard; architecture test banning scattered `SubscriptionSku.Starter =>` entitlement checks outside loader | API, portal, tests |

---

## 7. Files to touch

| Layer | Files (expected) |
|-------|------------------|
| **Domain** | Optional `PlanEntitlementDefinition` value object; error codes in `ErrorCodes` (`entitlements.practitioner_limit`, `entitlements.waitlist`, `entitlements.whatsapp`, …) |
| **Application** | `SubscriptionEntitlements.cs` → seed-driven; `IEntitlementsService.cs` (+ practitioner/waitlist/whatsapp/analytics helpers); DTO mappers for profile/subscription |
| **Infrastructure** | `EntitlementsService.cs`; `PlanEntitlementsLoader` (read seed JSON); staff create in `StaffMember` / roster service; `BusinessLocationService`; `ConversationService` / `MessagingHub`; `BookingsController` waitlist; `TenantDashboardService`; onboarding profile builder |
| **Api** | Profile/subscription controllers — serialized entitlements v2 |
| **Tests** | `EntitlementsServiceTests.cs`; integration tests for staff/location/waitlist/messaging; optional `country-pricing`-style test asserting seed ↔ C# parity |
| **packages/shared** | `tenantEntitlementsSchema`, `entitlements.ts` flags, `subscription-tiers.ts` / `PLAN_COMPARISON`, export seed or generated TS |
| **apps/portal** | `portal-nav.ts`, `portal-access.ts`, `locations/*`, `portal-shell.component.ts`, `dashboard/*` (no upgrade nag copy), `plan/*` (comparison only) |
| **apps/discover** | `plan/*` if `PLAN_COMPARISON` shared |
| **Docs** | This spec; one-line updates in sprints + 33b cross-ref |

Follow [architecture.md](../architecture.md): no entitlement `if (sku)` in controllers — call `IEntitlementsService`.

---

## 8. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Entitlements derived from **tenant row** (SKU + trial), not client input; enforcement uses tenant id from middleware |
| **Auth / roles** | Portal permissions unchanged; entitlements are **additive** gates on top of RBAC |
| **Cache** | If profile cached (`tenant:{id}:profile`), invalidate on SKU/billing webhook tier change (existing subscription lifecycle) |
| **Audit** | No new admin mutations; API denials log at Info/Warning without PII |
| **PII** | Entitlements payload has no phone/email |
| **Observability** | Optional metric: `entitlements.denied` with `code` + SKU (no tenant name in high-cardinality labels) |

---

## 9. Tests

- [ ] **Unit:** Seed loads for all three SKUs; Starter `messaging === true`; Professional/Business limits match [10_TESTS_AND_ACCEPTANCE_CRITERIA.md](./adeni-subscription-pricing-v1/10_TESTS_AND_ACCEPTANCE_CRITERIA.md)
- [ ] **Unit:** `EnsureCanAddPractitionerForTenantAsync` at 3/10/unlimited boundaries
- [ ] **Unit:** Trialing Professional inherits Pro entitlements (existing trial policy)
- [ ] **Integration:** Active Starter can open messaging API; second location blocked; Starter waitlist POST returns `entitlements.waitlist` (or equivalent)
- [ ] **Integration:** Cross-tenant staff/location mutations still 403 via tenant middleware
- [ ] **Shared:** Test or snapshot that `PLAN_COMPARISON` inbox row matches seed for all SKUs

Run: `dotnet test Adeni.slnx -c Release`

---

## 10. Acceptance criteria (matrix → enforceable in V1)

Mapped to [02_PLAN_ENTITLEMENTS.md](./adeni-subscription-pricing-v1/02_PLAN_ENTITLEMENTS.md) rows that the codebase can enforce now:

- [ ] **Starter — 1 location, ≤3 practitioners** — API rejects 2nd location and 4th practitioner; usage reflected on `/plan` or profile
- [ ] **Professional — 1 location, ≤10 practitioners** — Same enforcement at Pro caps
- [ ] **Business — multiple locations, high/unlimited practitioners** — Second location allowed; practitioner cap not hit at 10
- [ ] **Unlimited bookings / services** — No artificial monthly booking cap in entitlements layer
- [ ] **Adeni inbox (all tiers)** — Starter messaging **enabled** in seed, C#, tests, portal nav, and `PLAN_COMPARISON`
- [ ] **Waitlist** — Disabled for Starter; enabled Pro/Business on waitlist API
- [ ] **WhatsApp integration** — Disabled Starter; enabled Pro/Business on WhatsApp-capable endpoints/settings
- [ ] **Analytics (basic vs advanced)** — Starter does not receive advanced analytics widgets/API aggregates; Pro/Business do (per `advancedAnalytics` seed)
- [ ] **Multi-location** — Only Business gets `multiLocation` / second location create
- [ ] **Portal ops UX** — Locations, Messages, Dashboard do **not** show “upgrade to Pro/Business” nag copy; gated features are hidden or disabled with neutral copy; **`/plan`** remains the upgrade surface ([08_ANGULAR_PLAN_AND_PORTAL_UX.md](./adeni-subscription-pricing-v1/08_ANGULAR_PLAN_AND_PORTAL_UX.md))
- [ ] **Single source** — Changing seed (with codegen/sync step) updates C# and TS without editing scattered switch statements
- [ ] **Security** — Tenant cannot widen entitlements via request body; only SKU/trial resolution on server ([10_TESTS_AND_ACCEPTANCE_CRITERIA.md](./adeni-subscription-pricing-v1/10_TESTS_AND_ACCEPTANCE_CRITERIA.md) security row)

---

## 11. Open questions

| Question | Decision |
|----------|----------|
| Codegen vs runtime JSON load in API? | **Prefer build-time codegen to C# + copy/sync to `@adeni/shared`** (matches pricing catalog discipline); runtime load acceptable for MVP if codegen deferred one sprint |
| Keep legacy entitlement booleans (`basicCalendar`, `staffManagement`) in API? | **Remove from API response in v2** once portal uses new flags; internal mapping only if needed short-term |
| Gate walk-in queue vs waitlist separately? | **Waitlist seed only** unless product splits queue in matrix |
| Practitioner count includes inactive staff? | **Active/roster members only** — document in service (match UI staff list) |

---

## Related

- [sprint-33-saas-subscription-billing-mrr.md](./sprint-33-saas-subscription-billing-mrr.md) — billing + tier sync refreshes entitlements
- [sprint-33a-pricing-launch.md](./sprint-33a-pricing-launch.md) — Phase 1 pricing; entitlements enforcement called out in acceptance summary
- [sprint-33b-admin-commercial-config.md](./sprint-33b-admin-commercial-config.md) — catalog/commercial; **entitlements v2:** [sprint-33-entitlements-v2.md](./sprint-33-entitlements-v2.md)
