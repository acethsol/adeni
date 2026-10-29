# Spec: Sprint 33 — SaaS subscription billing & MRR

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 33 — SaaS subscription billing & MRR |
| **Status** | In progress (33a) |
| **Created** | 2026-10-15 |
| **Depends on** | [sprint-32-portal-dashboard-revisit.md](./sprint-32-portal-dashboard-revisit.md) **phase 32a** only (plan widget / usage UX); full widget dashboard can ship in parallel |

---

## 1. Goal (one sentence)

Businesses **pay Adeni recurring subscription fees** through a licensed billing provider; Adeni records subscription state for **entitlements**, **tenant billing history**, and **ops MRR** — separate from customer payment receipts (Sprint 28).

---

## 2. Context

### Two money flows (do not conflate)

| Flow | Who pays whom | Portal surface today | This sprint |
|------|----------------|----------------------|-------------|
| **SaaS (MRR)** | Business → Adeni | `/plan` (compare + usage); stub `POST /api/v1/subscriptions/checkout` | Live checkout, renewals, tenant invoices |
| **Commerce** | Customer → Business | `/payments` ledger + receipts/invoices | **Out of scope** (Sprint 17/28) |

### Strategy

- [product-strategy.md §4.1](../product-strategy.md#41-saas-subscriptions--foundation-mrrarr) — Free / Pro / Business; MRR = paying tenants × ARPU.
- [subscription-billing.md](../subscription-billing.md) — port `ISubscriptionBillingProvider`, stub provider, webhook shape.
- [roadmap-modules.md](../roadmap-modules.md) — Subscriptions module: entitlements shipped; live billing next.
- Sprint **20e** (planned): GMV / MRR / active-business **admin** dashboards — this sprint supplies **billing truth** MRR can read.

### Commercial model (Oct 2026)

Per [pricing-and-monetization.md](../pricing-and-monetization.md): **no perpetual free tier**; paid SKUs (Starter / Professional / Business) with **trialing / active / past_due / suspended** billing status; **admin-configured** trial length and discounts. Sprint 33 implements billing state + admin promo fields, not only tier enum.

### Current implementation

| Piece | Location | Gap |
|-------|----------|-----|
| Tier storage | `tenants.subscription_tier` | No billing period, provider refs, or invoice rows; `free` tier to migrate |
| Entitlements | `IEntitlementsService`, profile `entitlements[]` | Driven by tier; admin override |
| Checkout | `StubSubscriptionBillingProvider` | Fake URL; no charge |
| Webhook | `POST /api/v1/subscriptions/webhook` | Dev-only; **no signature** (Murphy gap) |
| Portal Plan | `apps/business/src/app/pages/plan/` | No pay button; “billing goes live on Azure” |
| Admin | Paid plan **count** + tier mix | Not currency MRR |
| api-client | No subscription checkout method | Wire when contract stable |

---

## 3. In scope

- [ ] **Provider implementation** (market-aware): Paystack Subscriptions (Lagos) primary; Stripe Billing design hook for Ottawa (may follow 33b if too large).
- [ ] **Murphy-compliant webhooks** — verify signature on raw body; idempotent tier/status updates; audit `subscription.*` events.
- [ ] **Persistence** — subscription billing records (minimal): provider customer id, subscription id, status, current period, tier, last invoice/charge snapshot (exact schema in implementation).
- [ ] **Application orchestration** — on success: update `subscription_tier`, invalidate entitlement caches, emit domain/audit events.
- [ ] **Portal `/plan`** — upgrade CTA, checkout redirect, return handling (`?upgraded=1`), **billing history** (last N provider invoices or synced rows): amount, status, date, download/receipt link if provider offers.
- [ ] **API** — document and implement `/api/v1/subscriptions/checkout`, tenant-facing billing history list, webhook (prod-enabled with secrets).
- [ ] **`packages/shared` + `@adeni/api-client`** — Zod schemas, stable error codes.
- [ ] **Admin commercial controls** — market default trial (days + SKU); per-tenant trial extend, **discount** (% or fixed, N cycles), comp with audit; see [pricing-and-monetization.md §5](../pricing-and-monetization.md#5-trials-grace-and-discounts-not-tiers).
- [ ] **Admin ops (MRR v1)** — extend admin dashboard or new panel: **MRR estimate** from list prices × active paid subs **or** sum of last successful period charges (prefer latter when synced); **paid tenant count**, tier mix, **failed payment** count; not full BI.
- [ ] Integration tests: checkout auth, cross-tenant denial, webhook idempotency, tier transition Free→Pro→Business and downgrade rules (grace TBD).
- [ ] Update [subscription-billing.md](../subscription-billing.md) from stub to live.

## 4. Out of scope

- Customer **Payments → Receipts & invoices** (Sprint 28) — unchanged.
- Wallet / custody / holding business funds.
- Custom invoice PDF for Adeni SaaS (use provider invoice PDF/URL unless trivial).
- Full Sprint 20e GMV dashboards (can share components later).
- Multi-currency FX reporting beyond market default.
- Self-serve **downgrade** with complex proration (document MVP: cancel at period end vs immediate).

---

## 5. API contract (proposed)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/v1/subscriptions/checkout` | JWT + `portal.plan` + tenant header | Start upgrade; body: `targetTier`, `successUrl`, `cancelUrl` |
| `GET` | `/api/v1/tenant/billing` | JWT + `portal.plan` | List Adeni subscription invoices/charges for tenant |
| `GET` | `/api/v1/tenant/subscription` | Existing | Usage + tier; extend with `billingStatus`, `renewalAt` if stored |
| `POST` | `/api/v1/subscriptions/webhook` | Provider HMAC | **Production**; rate-limited |
| `GET` | `/admin/metrics/subscription` | Admin MFA | MRR snapshot, paid counts, failed payments (v1) |

Request/response shapes to be added in `packages/shared` before merge.

---

## 6. Files to touch (expected)

| Layer | Files |
|-------|-------|
| Domain | Subscription billing entities, events |
| Application | `ISubscriptionBillingProvider`, billing orchestrator, DTOs |
| Infrastructure | Paystack (and/or Stripe) provider, EF mappings, webhook handler |
| Api | `SubscriptionBillingController`, admin metrics controller |
| Tests | Webhook signature, idempotency, tenant isolation |
| packages/shared, api-client | Schemas + client methods |
| apps/business | `plan/*`, optional dashboard link from Sprint 32 |
| apps/admin | MRR KPIs |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Secrets** | `Paystack:WebhookSecret`, plan codes — Key Vault staging/prod |
| **Audit** | Tier changes via webhook + admin override logged |
| **Entitlements** | Must refresh on tier change (existing service) |
| **Security** | Close unsigned webhook in prod; no tier change from body alone without provider verification |
| **PII** | Log provider refs + tenant id, not full card data |

---

## 8. MRR definition (v1)

**Authoritative (post-launch):** sum of **recognized monthly subscription charges** in the billing period (from synced provider invoices/charges with status `paid`), optionally split by market currency.

**Proxy (pre-sync / fallback):** `(pro_count × pro_list_price) + (business_count × business_list_price)` using configured catalog prices in `@adeni/shared` — label clearly as **estimated** in admin UI.

**Not MRR:** GMV, payment-link volume, customer invoices (Sprint 28).

---

## 9. Acceptance criteria

1. Business on Free can complete **real** checkout to Pro (Paystack test mode on staging).
2. Webhook replay does not double-apply tier.
3. Tenant sees **at least one** Adeni subscription charge/invoice on `/plan` after payment.
4. Admin sees **paid tenant count** and **MRR (authoritative or estimated)** with documented source.
5. Cross-tenant checkout body rejected (403).
6. `dotnet test Adeni.slnx -c Release` green.

---

## 10. Sequencing

1. **Sprint 32a** — plan widget + honest KPIs + gated shortcuts (unblocks billing).
2. **Sprint 33a** — provider + webhook + tier sync + portal checkout (can overlap 32b–32d).
3. **Sprint 32b–32d** — widget platform, charts, customize, catalog.
4. **Sprint 33b** — tenant billing history UI + admin MRR v1 (+ Stripe Billing if not in 33a).

---

## Launch pricing catalog (Oct 2026)

Country list prices, GMV **0%** at launch for **CA/NG**, and **US self-serve off** are locked in [adeni-subscription-pricing-v1/](./adeni-subscription-pricing-v1/00_READ_ME_FIRST.md) and synced to [country-pricing.json](../../packages/shared/src/data/country-pricing.json). Engineering phases for billing + entitlements + `/plan` are tracked in **[sprint-33a-pricing-launch.md](./sprint-33a-pricing-launch.md)** — this sprint spec remains the source for provider checkout, webhooks, MRR, and portal billing UX.

---

## Related

- [sprint-33a-pricing-launch.md](./sprint-33a-pricing-launch.md)
- [sprint-32-portal-dashboard-revisit.md](./sprint-32-portal-dashboard-revisit.md)
- [payments.md](../payments.md) — customer payment Murphy rules (same pattern for subscription webhooks)
- [AGENTS.md](../../AGENTS.md) — webhook + API contract hardening
