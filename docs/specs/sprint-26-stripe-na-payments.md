# Spec: Sprint 26 — Stripe NA payments provider

> Standing play 1 — draft for review. Do not implement until **Approved**.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 26 — Stripe NA payments |
| **Author** | Cloud Agent |
| **Status** | Implemented |
| **Created** | 2026-10-11 |

---

## 1. Goal (one sentence)

Ottawa / North America tenants can take booking deposits and pay links through **Stripe** using the same Adeni payment UX and orchestration already used with Paystack in Lagos — still non-custodial.

---

## 2. Context

- Roadmap: [roadmap-workflows-payments-queue.md](../roadmap-workflows-payments-queue.md) §3 (dual market, one port)
- Module index: [roadmap-modules.md](../roadmap-modules.md) §3.2
- Shipped: [payments.md](../payments.md) — `IPaymentProvider`, Paystack + Stub, deposits, links, ledger, refunds
- SaaS billing stub: [subscription-billing.md](../subscription-billing.md) — live Stripe/Paystack **subscriptions** may share webhook patterns but are a follow-on slice unless explicitly pulled in
- Module: **Payments** only (Booking consumes orchestrator events as today)

---

## 3. In scope

- [x] **26a** `StripePaymentProvider` implementing `IPaymentProvider` (initialize, status, refund, webhook parse)
- [x] **26b** Config: `Payments:Provider` = `Stripe` **or** market-aware resolution (Lagos → Paystack, Ottawa → Stripe) with explicit override for staging
- [x] **26c** `Stripe` options section + env vars (`SecretKey`, `WebhookSecret`, `PublishableKey` if needed client-side)
- [x] **26d** Webhook endpoint path reuse or Stripe-specific route with **signature verification first** (Murphy rules — same as Paystack)
- [x] **26e** Idempotent intent updates; never log raw card/PII payloads
- [x] **26f** Platform fee metadata / application fee strategy documented (Connect later vs platform fee on intent — pick one for v1)
- [x] **26g** Tests: signature reject, happy-path complete, double-webhook noop, refund path
- [x] **26h** Docs: update [payments.md](../payments.md) + markets note for Ottawa `platformFeePercent` default

## 4. Out of scope

- Stripe Terminal / in-person collect (payment-loop slice)
- Booking status expansion (`CheckedIn` / `Completed` / `Paid`) — **Sprint 27 payment loop**
- POS bridge (Square)
- Stripe Connect multi-party payouts to each salon bank account (document as open question; v1 may charge platform account only or simple destination charges — decide in open questions)
- Live Paystack Subscriptions / Stripe Billing SaaS checkout (can be 26bis or Sprint 28)
- Wallet / balance tables
- Changing customer-facing deposit UX beyond provider swap

---

## 5. API contract

Existing Payments API stays stable:

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/v1/payments/initialize` | Customer / business (as today) | Creates intent; provider returns checkout URL |
| `POST` | `/api/v1/payments/links` | Business | Pay link |
| `GET` | `/api/v1/payments/{id}` | Scoped | Intent status |
| `GET` | `/api/v1/payments/ledger` | Business | History |
| `POST` | `/api/v1/payments/{id}/refund` | Business | Refund |
| `POST` | `/api/v1/payments/webhook` **or** `/api/v1/payments/webhook/stripe` | Public + signature | Stripe events |

**Provider selection (proposed):**

1. If `Payments:Provider` is `Stub` / `Paystack` / `Stripe` → use that (dev override).
2. Else if `Payments:Provider` is `MarketDefault` → resolve from tenant primary market (`lagos` → Paystack, `ottawa` → Stripe).
3. Missing Stripe keys in Stripe mode → fail closed at startup or first initialize with clear config error.

**Webhook:** verify `Stripe-Signature` on **raw body** before deserialize; map `payment_intent.succeeded` / `charge.refunded` (or Checkout Session completed) into existing normalized orchestrator events.

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | Possibly none (reuse `PaymentIntentRecord` statuses/events) |
| Application | `IPaymentProvider` unchanged unless small extension for provider name / market hint |
| Infrastructure | `StripePaymentProvider.cs`, `StripeOptions.cs`, DI in `PaymentsServiceCollectionExtensions.cs` |
| Api | Webhook routing / content-type raw body for Stripe |
| Tests | `Adeni.Infrastructure.Tests` or Api tests for webhook signature + orchestrator |
| Shared / markets | Optional `paymentProvider` on market config in `markets.json` |
| Docs | `payments.md`, this spec → Approved; roadmap link |

Follow [architecture.md](../architecture.md) — Payments module only; Booking keeps listening to `PaymentCompleted`.

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Intents remain tenant-scoped; webhooks resolve by provider reference |
| **Auth / roles** | Unchanged |
| **Cache** | None |
| **Audit** | Existing `payment.*` audit events |
| **PII** | No card data in Adeni DB; mask emails in logs |
| **Observability** | Correlation ID on initialize; webhook event type + reference only |
| **Secrets** | Stripe keys via env / Key Vault — never commit |

---

## 8. Tests

- [ ] Unit: Stripe signature validation accept/reject
- [ ] Integration: initialize → stubbed Stripe HTTP → pending intent
- [ ] Webhook success → intent Completed + booking deposit handler (reuse existing)
- [ ] Duplicate webhook → no double apply
- [ ] Refund path
- [ ] MarketDefault selects Stripe for Ottawa tenant fixture

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [x] Ottawa-market tenant (or `Payments:Provider=Stripe`) can initialize a deposit/pay link and receive a Stripe-hosted checkout URL
- [x] Signed Stripe webhook marks intent completed; unsigned webhook rejected
- [x] Lagos / Paystack path still works when provider is Paystack
- [x] Stub path unchanged for local CI
- [x] [payments.md](../payments.md) documents both providers and env vars
- [x] No wallet/balance schema changes

*(Initialize/checkout URL against live Stripe requires keys in env; unit tests cover signature + payload parse. E2E with real Stripe keys is ops/staging.)*

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| Checkout Session vs PaymentIntent API? | Prefer **Checkout Session** for hosted UX parity with Paystack authorization_url |
| Stripe Connect for salon payouts in v1? | **Recommend defer** — platform collects, manual/ops payout; Connect in later slice |
| MarketDefault vs explicit Provider only? | Prefer **MarketDefault** + explicit override for staging |
| Include Stripe Billing (SaaS) in same sprint? | **No** — separate slice after card commerce works |
| CAD currency + tax display? | Charge in market currency (`CAD`); tax line items out of scope v1 |

---

## 11. Follow-on (not this sprint)

1. **Sprint 27 — Payment loop** — booking states `CheckedIn` / `Completed` / `Paid` + paid-at-till (see roadmap §4)
2. SaaS Billing (Stripe + Paystack Subscriptions)
3. Terminal / POS bridge
