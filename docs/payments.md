# Payments (redirect)

Commerce orchestration moved under the **Finance** module.

- **API & runbook:** [finance.md](./finance.md)
- **Boundaries (tips, subscriptions split, extraction):** [specs/finance-module-boundaries.md](./specs/finance-module-boundaries.md)

HTTP routes are **`/api/v1/finance/*`** (no `/api/v1/payments/*` aliases in v1 dev).

Configuration keys remain **`Payments:`** in `appsettings` until a optional rename in Finance Phase 4.
