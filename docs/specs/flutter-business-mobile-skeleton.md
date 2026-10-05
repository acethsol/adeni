# Spec: Flutter business mobile skeleton (owner, employee, front desk)

| Field | Value |
|-------|-------|
| **Sprint** | Post–Angular cleanup / pre–consumer Flutter |
| **Author** | Aceth |
| **Status** | Approved (skeleton); API depth backlog |
| **Created** | 2026-10-05 |

---

## 1. Goal (one sentence)

Give supply-side businesses a native **Flutter** shell with **owner**, **employee**, and **front-desk check-in** modes that call the existing Adeni API—without rebuilding domain logic on the device.

---

## 2. Context

- [ADR-012](../adr/ADR-012-angular-web-deferred-flutter.md) deferred **consumer** Flutter until web GA; this spec covers **business-operational** mobile (aligned with portal GTM).
- Legacy Expo app archived: [legacy-clients-archive.md](../legacy-clients-archive.md).
- Modules: **Tenancy**, **Booking** (today); future **staff roles**, **check-in state** on bookings.
- Product wedge: beauty & wellness (e.g. nail spa front desk checking in a pedicure appointment).

---

## 3. In scope (skeleton — this PR)

- [x] `apps/mobile` Flutter project (`adeni_business`) in monorepo
- [x] Three navigable shells: **Owner**, **Employee**, **Front desk (kiosk-style)**
- [x] Dev launcher to pick mode (no Auth0 wiring yet)
- [x] Thin `AdeniApiClient` — configurable base URL, `GET /health`, placeholder tenant booking list via existing API when auth exists
- [x] README: run instructions, env, relationship to portal/discover
- [ ] Auth0 Native (PKCE) + business JWT + `X-Tenant-Id` (follow-up)
- [ ] OpenAPI → Dart client generation (follow-up)

## 4. Out of scope

- Consumer discover/book app (separate track per ADR-012)
- Wallet / custody, payroll, inventory
- Full check-in state machine on API (`CheckedIn`, `InService`, …) — spec’d below as **phase 2**; skeleton UI only
- Walk-in queue / digital queue product
- App Store / Play release
- Replacing Angular portal for configuration-heavy tasks (services, availability editor)

---

## 5. API contract

### Skeleton (uses today)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/health` | Anonymous | Liveness for dev/demo |
| GET | `/api/v1/business/bookings` | Business JWT + `X-Tenant-Id` | Employee/owner day view (portal parity) |

### Phase 2 — front desk check-in (proposed)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/v1/business/bookings/{id}/check-in` | Business JWT + tenant | Mark arrival; idempotent |
| POST | `/api/v1/business/bookings/{id}/check-out` | Business JWT + tenant | Complete service / checkout |
| GET | `/api/v1/business/bookings/today` | Business JWT + tenant | Front desk list (filter by location optional) |

Stable error codes via existing [api-errors.md](../api-errors.md) (`booking.not_found`, `booking.invalid_state`, …).

**Shared types:** mirror `packages/shared` booking schemas when Dart client lands; until then hand-maintained DTOs in `apps/mobile/lib/core/api/models/`.

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Spec | `docs/specs/flutter-business-mobile-skeleton.md` |
| Mobile | `apps/mobile/**` (Flutter) |
| Docs | `docs/frontend.md`, `docs/target-client-architecture.md` (Flutter business note) |
| Domain / Api | *Phase 2 only* — booking status transitions, audit |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All business routes require JWT tenant claim + `X-Tenant-Id` match (same as portal) |
| **Auth / roles** | Owner vs employee vs kiosk = Auth0 roles/claims TBD; kiosk may use location-scoped device token |
| **Cache** | Mobile: short TTL on “today’s bookings”; invalidate on check-in/out |
| **Audit** | Check-in/out mutations → business audit trail (phase 2) |
| **PII** | Front desk: show customer first name + masked phone only on shared tablet |

---

## 8. UX modes

| Mode | Primary user | Device | MVP screens |
|------|--------------|--------|-------------|
| **Owner** | Business owner | Phone | Dashboard stub, link to plan/payments (web deep link ok) |
| **Employee** | Stylist / tech | Phone | Today’s appointments stub |
| **Front desk** | Reception | Tablet landscape | Search/check-in stub, large touch targets |

Kiosk: consider `android:immersive` / guided access; auto-lock; no owner settings in this mode.

---

## 9. Acceptance criteria (skeleton)

- [ ] `flutter analyze` clean on `apps/mobile`
- [ ] App launches; user can open Owner / Employee / Front desk screens
- [ ] Each screen shows API health status when base URL reachable
- [ ] Documented default API URL for local dev (`http://10.0.2.2:5169` Android emulator, `localhost` iOS sim)

---

## 10. Test plan

- Manual: API running → open Front desk → health shows `healthy`
- Manual: wrong base URL → graceful error message (no crash)
- Phase 2: integration test against `WebApplicationFactory` for check-in routes
