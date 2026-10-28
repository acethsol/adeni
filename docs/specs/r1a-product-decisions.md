# R1-A — Product decisions (packages / benefit ledger)

Status: **Decision log** — inputs for future spec [`sprint-37-1-packages-benefit-ledger.md`](./sprint-37-1-packages-benefit-ledger.md). No API, entitlement, or schema work authorized by this document alone.

Authority: [product-direction.md](../product-direction.md), [product-strategy.md](../product-strategy.md) (non-custodial commerce; no wallet).

## Decisions (2026-10-28)

| Topic | Decision |
|-------|----------|
| **Sales before live settlement** | Till / transfer (in-person) package sales are **allowed** before live online settlement is enabled for the tenant. Online package purchase remains subject to existing Paystack orchestration and settlement gates. |
| **Package value type** | Packages are **session / service credits only** (count or duration tied to catalog services). They are **not** a monetary wallet, stored balance, or withdrawable cash value. |
| **Expiry** | Default **12 months** from purchase or activation (exact anchor TBD in spec). Tenant may configure expiry in **months**. Unused credits **forfeit** at expiry unless the **owner voids** the package (audit-logged). |
| **Portal entitlement** | **`portal.retention`** — yes; package management and redemption UI in Portal is in scope for R1-A when implemented. |
| **Redemption scope (#6)** | **Approved** — package-level redemption + sell scope; validate **booking location** at redemption; customer home location unchanged. See section 6 below. |

## 6. Redemption scope — **Approved**

**Package definition**

- **Redemption scope** (enum): `all_locations` | `purchase_location_only` | `selected_locations` (location checklist).
- **Sell scope:** which locations may sell the package (Fresha-style default: all active locations).

**At redemption**

- Validate **booking location** against the package redemption scope.
- Do **not** move or overwrite the customer **home location** for redemption.
- Customer home location remains for CRM/segments; redeem at another branch only when the package allows.
- Service must exist at the branch to redeem (catalog constraint at booking/redemption time).

**Defaults & implementation notes**

- Default redemption scope: **`all_locations`**; default sell scope: **all locations** may sell unless restricted.
- Persist scope on the **package product** (or tenant default), not per customer.
- Ledger entries remain **tenant-scoped**; optional `BusinessLocationId` on grant/redemption rows for sale origin and reporting.
- Do **not** fork `TenantCustomer` per location for packages; enforce location at redemption via package scope + booking/queue location.
- Portal: show **where the package was sold**, **redemption scope**, and **where it may be redeemed** so staff do not assume tenant-wide validity when scope is narrower.

## Out of scope for this log

- DACPAC / migrations, retention module implementation, entitlement seed changes, and API contracts — deferred to `sprint-37-1-packages-benefit-ledger.md`.
