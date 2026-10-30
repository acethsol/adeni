# Spec stub: Flutter consumer app (parity with web `/app/*`)

> **Status:** Stub — planning only. Not part of Sprint C1 delivery.  
> **Depends on:** [sprint-c1-consumer-app-experience.md](./sprint-c1-consumer-app-experience.md) (web IA frozen), [target-client-architecture.md](../target-client-architecture.md), ADR-012.

## Goal

Ship a native Flutter consumer app that mirrors the signed-in **customer app** information architecture already live on web under `/app/*`, while public SEO discovery remains web-only (`apps/customer` SSR).

## Frozen nav ids (do not rename without a migration)

Source of truth: `apps/customer/src/app/app-surface/customer-nav.ts`.

| Id | Web path | Role |
|----|----------|------|
| `home` | `/app/home` | Tab + desktop |
| `discover` | `/app/discover` | Tab + desktop (handoff to public business pages for booking) |
| `bookings` | `/app/bookings` | Tab + desktop |
| `messages` | `/app/messages` | Tab + desktop; badge = `needsAttention` |
| `benefits` | `/app/benefits` | Desktop (packages / memberships / gift-cards) |
| `profile` | `/app/profile` | Mobile tab |

Home composition helpers live in `apps/customer/src/app/app-surface/pages/home/customer-home.helpers.ts` — promote to `@adeni/shared` (pure TS) before Dart ports if Flutter starts before a shared package extract.

## Out of scope for this stub

- Implementing Flutter screens
- Push notifications
- Separate booking UI inside the app shell (booking stays on canonical public business profile / deep link)
- Wallet / custody

## Next when Flutter starts

1. Copy this stub → full spec from `_template.md`.
2. OpenAPI → Dart DTOs; mirror Auth0 PKCE + return-path allowlist rules from `auth-return-target.ts`.
3. Map each nav id to a `GoRouter` route; keep ids identical for analytics.
