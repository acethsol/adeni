# Spec stub: Flutter consumer app (parity with web `/app/*`)

> **Status:** Stub — planning only. Not part of Sprint C1/C2 delivery.  
> **Depends on:** [sprint-c1-consumer-app-experience.md](./sprint-c1-consumer-app-experience.md), [sprint-c2-consumer-portal.md](./sprint-c2-consumer-portal.md), [target-client-architecture.md](../target-client-architecture.md), ADR-012.

## Goal

Ship a native Flutter consumer app that mirrors the signed-in **customer portal** information architecture under `/app/*`, while public SEO discovery remains web-only (`apps/customer` SSR).

## Frozen nav ids (C2 — do not rename without a migration)

Source of truth: `apps/customer/src/app/app-surface/customer-nav.ts`.

| Id | Web path | Desktop | Mobile tab | Role |
|----|----------|---------|------------|------|
| `dashboard` | `/app/dashboard` | Yes | Yes | Default landing / Open app |
| `calendar` | `/app/calendar` | Yes | Yes | Appointment month/week |
| `discover` | `/app/discover` | Yes | Yes | In-app find → public business pages |
| `bookings` | `/app/bookings` | Yes | No | List + detail + intake |
| `messages` | `/app/messages` | Yes | Yes | Badge = `needsAttention` |
| `benefits` | `/app/benefits` | Yes | No | Packages / memberships / gift-cards |
| `profile` | `/app/profile` | Account menu | Yes | Identity + shortcuts |

`/app/home` redirects to `/app/dashboard` on web; Flutter should not register a `home` destination.

Dashboard helpers: `app-surface/pages/dashboard/customer-dashboard.helpers.ts` — promote to `@adeni/shared` before Dart ports if needed.

## Out of scope for this stub

- Implementing Flutter screens
- Push notifications
- Separate booking UI inside the app shell (booking stays on canonical public business profile / deep link)
- Wallet / custody

## Next when Flutter starts

1. Copy this stub → full spec from `_template.md`.
2. OpenAPI → Dart DTOs; mirror Auth0 PKCE + return-path allowlist rules from `auth-return-target.ts`.
3. Map each nav id to a `GoRouter` route; keep ids identical for analytics.
