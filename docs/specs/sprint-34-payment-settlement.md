# Spec: Sprint 34 — Payment settlement (Connect + Paystack split)

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 34 — Payment settlement |
| **Status** | Draft |
| **Created** | 2026-10-15 |
| **Depends on** | Sprint 17/26 payments orchestration (done); **parallel** with Sprint 33 after 33a optional |
| **Authority** | [payment-provider-settlement.md](../payment-provider-settlement.md), [country-pricing.json](../../packages/shared/src/data/country-pricing.json) |

---

## 1. Goal (one sentence)

Route customer payments to **business settlement accounts** via **Stripe Connect** (CA/US) and **Paystack split** (NG), collecting Adeni’s **business-paid** GMV fee from config (including **0%** NG).

---

## 2. In scope

- [ ] Tenant fields: `StripeConnectAccountId`, status flags; `PaystackSubaccountCode`, status
- [ ] `IPaymentSettlementService` — create Connect Express account, Account Link; create/update Paystack subaccount
- [ ] Portal **Payments** onboarding UX + status
- [ ] `StripePaymentProvider` — destination charge + `application_fee_amount` from `PlatformFeeAmount`
- [ ] `PaystackPaymentProvider` — split on initialize per `gmvCollection` (100/0 at 0% fee)
- [ ] `PlatformFeeCalculator` — resolve fee from **tenant country** + `country-pricing.json`; `feeBearer: business` enforced (never gross-up customer amount)
- [ ] Refund paths — application fee reversal (Stripe); Paystack per provider rules
- [ ] Gate: `blockPaymentsUntilOnboarding` per country; `payment.connect_required` / `payment.settlement_required` errors
- [ ] Tests + update [payments.md](../payments.md)

## 3. Out of scope

- Customer surcharges (business-paid only — policy)
- Stripe Terminal / in-person
- Changing SaaS subscription billing (Sprint 33)

---

## Related

- [pricing-and-monetization.md §6.4](../pricing-and-monetization.md#64-gmv-platform-fee-business-paid-policy)
