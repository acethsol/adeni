# Post-Retention audit remediation — final report

## Status

**Closed** — merged to **`dev`** on 2026-10-30 ([PR #43](https://github.com/acethsol/adeni/pull/43), merge commit `1d4f2a4`). CI: **build-and-test**, **frontend**, and **sqlserver** green on merge.

Remediation scope is complete. Items in §24 are **backlog**, not open audit findings.

## 1. Branch

`chore/post-retention-audit-remediation` — **merged** into `dev` (not promoted to `qa` / `main` by this audit).

## 2. Base `dev` commit

`eb86241110d97b88b88e1897457b14e1d3ff9787`

## 3. Commits on branch

| Commit | Phase |
|--------|-------|
| `cd17900` | **A1** — production budgets restored, CI customer/admin tests, A1 doc |
| `d2ab4bb` | **A2–A4** — retention fixes, customer lazy `/app`, phase docs |
| `6794255` | Final report |
| `0c6bc3c` | SQL cap test, guard specs, dashboard/calendar SCSS |
| `e0f8681`–`4e82067` | CI fixes (shared typecheck, customer app.spec, zod audit, SQL test SaveChanges) |
| `1d4f2a4` | **Merge to `dev`** (squash/merge via PR #43) |

## 4. Files changed by phase

**A1:** `apps/business/angular.json`, `apps/customer/angular.json`, `.github/workflows/ci.yml`, `docs/ci.md`, `docs/post-retention-audit/A1-production-budgets.md`

**A2:** `ReferralQualificationService.cs`, `LoyaltyEarnService.cs`, `LoyaltyServiceTests.cs`, `business-permission.guard.ts`, `business-route-nav-path.ts`, `docs/post-retention-audit/A2-retention-findings.md`

**A3:** `app.routes.ts`, `customer-app.routes.ts`, `docs/post-retention-audit/A3-customer-boundary.md`

**A4:** `docs/post-retention-audit/A4-admin-authorization.md` (review only)

## 5–6. Findings confirmed vs disproved

**Confirmed & fixed:** referrer cap race (SQL path), NGN currency fallback, Business retention child-route entitlement gap, Customer `/app` eager import, relaxed 30 kB component budgets, missing Customer/Admin CI unit tests.

**Already mitigated / no change:** Benefit ledger rowversion tests (`RetentionSqlServerTests`); backend entitlement guards on retention APIs (unchanged); Admin initial bundle currently under 2 MB error on local build.

**Deferred with documentation:** `BusinessType.ScheduledAppointment` loyalty/referral gate (product); full CSS budget cleanup (non-dashboard/calendar sheets); Auth0 localStorage + CSP deep pass; scoped admin roles.

**Shipped on merge:** SQL `ConcurrentReferralCap_AtLimit_ExactlyOneExtraReward`; `business-route-nav-path.spec.ts`; dashboard/calendar SCSS → global partials (production budget pass).

## 7–8. Security / correctness fixes

- Referrer reward cap re-check under `UPDLOCK` + transaction (SQL Server).
- Loyalty earn skips unresolved/mismatched currency (no NGN default).
- Business route guard: full path + `entitlementsAny` + retention tab entitlements.

## 9–12. App-specific notes

See `A2-retention-findings.md`, `A3-customer-boundary.md`, `A4-admin-authorization.md`.

## 13–14. CI / budgets

- CI runs shared + business + **customer** + **admin** unit tests.
- Production builds **pass** restored `anyComponentStyle` error limits after dashboard/calendar SCSS extraction; other sheets may still warn.

## 15. Tests added

- `StageEarn_WhenServiceCurrencyMissing_DoesNotFallbackToNgn`
- `StageEarn_WhenProgramCadAndBookingNgn_DoesNotEarn`
- `ConcurrentReferralCap_AtLimit_ExactlyOneExtraReward` (SQL Server opt-in)
- `business-route-nav-path.spec.ts` (retention path + tab id helpers)

## 16–18. Validation

| Check | Result |
|-------|--------|
| `dotnet test Adeni.slnx -c Release` | **875 passed**, 31 skipped (SQL Server opt-in) — CI **build-and-test** |
| Frontend workspaces (typecheck, unit tests, prod builds, audit) | CI **frontend** job on PR #43 |
| SQL Server `Category=SqlServer` | CI **sqlserver** job (includes referral cap concurrency test) |

## 19. Vulnerabilities

Not re-run in this session; CI continues `npm audit` + NuGet script.

## 20. Production blockers

Historical audit warnings on other Business/Customer component sheets (profile, staff-calendar, app-surface pages); not production **error** blockers after dashboard/calendar extraction.

## 21–23. Risks / debt / product decisions

- Single Admin role accepted for launch.
- In-memory rate limits per instance.
- Walk-in/hybrid businesses excluded from loyalty/referral earn until product spec changes qualification.

## 24. Backlog (post-closeout)

Track via normal sprint work — not part of this audit:

1. Measured shared SCSS / component style split ([audit-hardening-closeout.md §F](../audit-hardening-closeout.md)) — e.g. staff-calendar, my-messages warnings.
2. Customer `server.ts` / ingress CSP ownership doc + Auth0 storage review.
3. Product: `BusinessType.ScheduledAppointment` gate for loyalty/referral earn (see A2).
4. Optional scoped admin roles post-launch (see A4).

## 25. Diff summary

Merged range on `dev`: `git log eb86241..1d4f2a4 --oneline` and `git diff eb86241...1d4f2a4 --stat`
