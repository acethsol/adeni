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
| **Redemption scope** | **Tenant-configurable**: redeem at a **single location** vs **any location** under the tenant. |

## Open design — customer ↔ location

Today, roster and operational flows often treat customers in a **branch context** (location filters on profile, bookings, queue). Multi-location redemption implies a customer can spend credits booked at Location A at Location B.

**Recommendation for spec draft (not decided):**

- Persist redemption scope on the **package product** (or tenant default), not per customer.
- Ledger entries remain **tenant-scoped**; optional `BusinessLocationId` on grant/redemption rows when scope is single-location or when the sale origin must be recorded for reporting.
- Do **not** fork `TenantCustomer` per location for packages; use existing roster identity and enforce location at redemption time via tenant settings + booking/queue location.
- Portal: when scope is single-location, show **where the package was sold** and **where it may be redeemed** explicitly to avoid staff assuming tenant-wide validity.

## Out of scope for this log

- DACPAC / migrations, retention module implementation, entitlement seed changes, and API contracts — deferred to `sprint-37-1-packages-benefit-ledger.md`.
