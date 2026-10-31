# Full application audit closeout #2

**Branch:** `chore/full-app-audit-closeout-2`  
**Base:** `a0dd692e640569f2b7bfd0a1cd9168c04b19b97d`

## Summary

Targeted remediation for payment integrity, RBAC gaps, upload validation, customer privacy routes, Auth0 bootstrap, and CI fail-closed behavior. No schema/DACPAC changes.

## P0 — Finance

- Customer `POST /api/v1/finance/initialize` → `InitializeCustomerBookingPaymentAsync` (booking ownership, server-derived deposit, server callback).
- Deposit confirmation requires aggregated completed deposit ≥ required deposit.
- Realtime token only embedded in stub checkout URLs; live provider URLs unchanged.
- Payment read access requires `business.finance` or `business.bookings` (not mere membership).
- Tip staff tenant validation at init + compensation allocation guard.
- Callback URL allowlist for business pay links; customer booking uses server-built receipt callback.
- Idempotency scoped by tenant (+ customer booking scope for customer init).

## P1 — Authz / storage / customer / CI

- Quotes, reviews, verification badges → controller RBAC.
- Business customers nav → `business.bookings` only (already aligned in nav).
- Verification + quote photo post-upload blob validation.
- Shared Zod `verification_document` purpose.
- `/documents/**` client render + private cache headers.
- CI aggregates require `success`; `detect-changes` in `ci-complete`; SQL-sensitive and Application/Domain path rules.

## P2 — Auth / governance

- Single browser Auth0 registration via `main.ts` merge only.
- CSP: follow-up (headers baseline retained).
- Release: default branch `main`; `dev` unprotected; no `qa` branch; ruleset “Ruleset A” active — configure required checks (`build-and-test`, `sqlserver`, `frontend`, `ci-complete`) manually.

## Status

Implemented on branch; pending PR review.
