# A1 — Production budget baseline (post-Retention audit)

**Branch:** `chore/post-retention-audit-remediation`  
**Base `dev`:** `eb86241110d97b88b88e1897457b14e1d3ff9787`

## Policy

Production budgets were **restored** to audit-documented error limits. They must not be raised to hide failures.

| App | `anyComponentStyle` error | `initial` error | Notes |
|-----|---------------------------|-----------------|-------|
| **Business** | **24 kB** (was incorrectly **30 kB**) | 3 MB | Matches [audit-hardening-closeout.md](../audit-hardening-closeout.md) Portal rows |
| **Customer** | **14 kB** (was incorrectly **30 kB**) | 3 MB | Matches C1 / audit Discover `_data-grid` baseline |
| **Admin** | **24 kB** (unchanged) | **2 MB** | Unchanged |

**Evidence of relaxation:** `apps/business/angular.json` and `apps/customer/angular.json` had `maximumError: "30kB"` on `anyComponentStyle` while audit closeout and F-series specs cite **24 kB** (Business) and **14 kB** (Customer/Discover). No spec justified 30 kB.

## Measured blockers (local production build, after restore)

Re-run after each CSS cleanup:

```powershell
npm run build:business
npm run build:customer
npm run build:admin
```

Local production build after restore (2026-10-30):

| App | Result | Fatal (`maximumError`) |
|-----|--------|-------------------------|
| **Business** | **PASS** (error budgets) | Dashboard styles moved to `styles/_dashboard-page.scss`; component sheet minimal. Warnings remain on profile/staff-calendar. |
| **Customer** | **PASS** (error budgets) | Calendar styles moved to `styles/_customer-calendar-page.scss`; component sheet minimal. Warnings on other app-surface sheets. |
| **Admin** | **PASS** (error budgets) | Initial ~**1.69 MB** (under **2 MB** error); several styles **15–20 kB** (warnings only, still under **24 kB** error) |

Inherited audit items (services/staff/plan ~24.5–28 kB, `_data-grid.scss` ~16.9 kB) may appear on other builds; re-run `npm run build:*` after CSS cleanup.

## Safe style ownership (this branch)

No broad SCSS refactor in A1. Follow-up: split shared grid styles per [audit-hardening-closeout.md §F](../audit-hardening-closeout.md).

## CI (A1)

Frontend job now runs unit tests for **shared**, **business**, **customer**, and **admin** workspaces (ChromeHeadless where configured).
