# Post-Retention audit remediation — final report

## 1. Branch

`chore/post-retention-audit-remediation` (not merged to `dev`, `qa`, or `main`).

## 2. Base `dev` commit

`eb86241110d97b88b88e1897457b14e1d3ff9787`

## 3. Commits on branch

| Commit | Phase |
|--------|-------|
| `cd17900` | **A1** — production budgets restored, CI customer/admin tests, A1 doc |
| `d2ab4bb` | **A2–A4** — retention fixes, customer lazy `/app`, phase docs |

## 4. Files changed by phase

**A1:** `apps/business/angular.json`, `apps/customer/angular.json`, `.github/workflows/ci.yml`, `docs/ci.md`, `docs/post-retention-audit/A1-production-budgets.md`

**A2:** `ReferralQualificationService.cs`, `LoyaltyEarnService.cs`, `LoyaltyServiceTests.cs`, `business-permission.guard.ts`, `business-route-nav-path.ts`, `docs/post-retention-audit/A2-retention-findings.md`

**A3:** `app.routes.ts`, `customer-app.routes.ts`, `docs/post-retention-audit/A3-customer-boundary.md`

**A4:** `docs/post-retention-audit/A4-admin-authorization.md` (review only)

## 5–6. Findings confirmed vs disproved

**Confirmed & fixed:** referrer cap race (SQL path), NGN currency fallback, Business retention child-route entitlement gap, Customer `/app` eager import, relaxed 30 kB component budgets, missing Customer/Admin CI unit tests.

**Already mitigated / no change:** Benefit ledger rowversion tests (`RetentionSqlServerTests`); backend entitlement guards on retention APIs (unchanged); Admin initial bundle currently under 2 MB error on local build.

**Deferred with documentation:** `BusinessType.ScheduledAppointment` loyalty/referral gate (product); full CSS budget cleanup (non-dashboard/calendar sheets); Auth0 localStorage + CSP deep pass; scoped admin roles.

**Follow-up (same branch):** SQL `ConcurrentReferralCap_AtLimit_ExactlyOneExtraReward`; `business-route-nav-path.spec.ts`; dashboard/calendar SCSS moved to global partials (production budget pass).

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

## 16–18. Validation (local)

| Check | Result |
|-------|--------|
| `dotnet test Adeni.slnx -c Release` | **875 passed**, 30 skipped (SQL Server opt-in) |
| `npm test --workspace @adeni/shared` | **Passed** |
| `npm run build:business` | **PASS** (error budgets) |
| `npm run build:customer` | **PASS** (error budgets) |
| `npm run build:admin` | **PASS** (error budgets; warnings remain) |
| SQL Server category | **Not run locally** (requires `ADENI_SQLSERVER_TESTS=1` + DACPAC) |

## 19. Vulnerabilities

Not re-run in this session; CI continues `npm audit` + NuGet script.

## 20. Production blockers

Historical audit warnings on other Business/Customer component sheets (profile, staff-calendar, app-surface pages); not production **error** blockers after dashboard/calendar extraction.

## 21–23. Risks / debt / product decisions

- Single Admin role accepted for launch.
- In-memory rate limits per instance.
- Walk-in/hybrid businesses excluded from loyalty/referral earn until product spec changes qualification.

## 24. Next priorities

1. Measured shared SCSS / component style split (audit closeout §F).
2. SQL Server referral cap concurrency test in CI.
3. Customer `server.ts` / ingress CSP ownership doc + Auth0 storage review.
4. Optional scoped admin roles post-launch.

## 25. Diff summary

Run: `git log eb86241..HEAD --oneline` and `git diff eb86241...HEAD --stat`
