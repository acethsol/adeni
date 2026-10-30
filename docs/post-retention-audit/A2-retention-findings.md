# A2 — Retention correctness (verified + remediated)

## Confirmed and fixed

| Finding | Evidence | Remediation |
|---------|----------|-------------|
| **Referrer cap race** | `ReferralQualificationService.EvaluateFraudAsync` counted `Rewarded` then later `IssueRewardsAsync` credited without re-check under lock | Re-check cap in `IssueRewardsAsync` with SQL `UPDLOCK` on `retention.referral_programs` + transaction commit |
| **Loyalty currency NGN fallback** | `LoyaltyEarnService.ResolveBookingCurrencyAsync` used `?? "NGN"` | Return `null` when unresolved; skip earn; tests for empty currency and CAD/NGN mismatch |
| **Business Retention route gating** | `businessPermissionGuard` used leaf path (`memberships`) → `findBusinessNavItem` miss or no tab entitlement; `entitlementsAny` not enforced | `businessRouteNavPath`, `retentionTabIdFromPath`, enforce `entitlementsAny` + per-tab entitlements |

## Verified / accepted (no code change this branch)

| Finding | Status |
|---------|--------|
| **Benefit ledger concurrency** | Existing `RetentionSqlServerTests` (rowversion) — see tests project |
| **BusinessType.ScheduledAppointment gate** for loyalty/referrals | **Product decision deferred.** Current code requires `BusinessType.ScheduledAppointment` on `BusinessProfile`. Hybrid/walk-in tenants do not earn referral/loyalty on paid bookings until product expands qualification (e.g. completed paid/completed booking event without business-type gate). Documented for Sprint follow-up — do not remove gate without spec. |

## Follow-up (not in this branch)

- SQL Server concurrent referral cap test (`MaxRewardsPerReferrer=10`, 9 rewarded, two parallel qualifies) — add to `RetentionSqlServerTests` when CI SQL job extended.
- Membership billing reconciliation E2E with live Paystack — staging gate per Sprint 33c.
