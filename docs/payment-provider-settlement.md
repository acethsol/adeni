# Payment settlement — Stripe Connect & Paystack Split

How Adeni routes **customer → business** payments and collects a **configurable platform GMV fee** (including **0%** today for Nigeria). Non-custodial: licensed providers move money; Adeni orchestrates checkout and split math.

**Related:** [payments.md](./payments.md) · [pricing-and-monetization.md §6](./pricing-and-monetization.md#6-customer-payments-pass-through-vs-platform-gmv-fee) · [country-pricing.json](../packages/shared/src/data/country-pricing.json)

### GMV fee bearer: business-paid (policy)

The **customer pays only the business’s stated amount** (booking, deposit, pay link). Adeni’s platform % is **not** added to the checkout total. It is collected from **business settlement**:

- **Stripe Connect:** `application_fee_amount` on a **destination charge** — fee to Adeni platform balance, remainder to connected account.
- **Paystack split:** share to Adeni **main account**, remainder to business **subaccount**.

Card processing fees remain **provider pass-through** (deducted from business payout per Paystack/Stripe rules). See [pricing-and-monetization.md §6.4](./pricing-and-monetization.md#64-gmv-platform-fee-business-paid-policy).

---

## 1. Why two mechanisms

| Region | Provider | Settlement mechanism |
|--------|----------|-------------------|
| **CA / US** | Stripe | **Stripe Connect** (connected account + `application_fee`) |
| **NG** (Africa) | Paystack | **Transaction split** (subaccount + configurable share to Adeni main account) |

Same **orchestrator** (`PaymentOrchestrator` → `PlatformFeeCalculator` → provider initialize). Difference is **how** the provider sends the platform its cut.

**Design rule:** Wire Connect/split **even when `platformFeePercent` is 0**. Fee = 0 should mean “Adeni’s share is zero,” not “we skip settlement infrastructure.” That lets you raise NG from 0% → 0.5% via config without a code fork.

---

## 2. Stripe Connect (Canada & United States)

### 2.1 What it is

Stripe has two account types in your architecture:

| Account | Role |
|---------|------|
| **Platform** | Adeni’s Stripe account (you own the Connect platform) |
| **Connected account** | Each business — a legal merchant identity Stripe KYC’s |

**Connect** lets the platform create charges **on behalf of** connected accounts and take an **`application_fee`** (Adeni GMV %) automatically. Customer pays once; Stripe splits at capture:

```
Customer pays $100
  → Stripe processing fee (pass-through, from connected account settlement)
  → Application fee $0.75 → Adeni platform balance  (if 0.75%)
  → Remainder → Connected account (business)
```

Adeni never holds a wallet balance of customer funds in your DB — Stripe settles per their rules (still non-custodial from Adeni’s product perspective).

### 2.2 Express vs Standard (recommendation)

| | **Express** (recommended) | Standard |
|--|---------------------------|----------|
| Onboarding | Stripe-hosted; fast | More fields; business uses full Stripe Dashboard |
| UX | Embedded / Account Links in portal | Redirect to Stripe |
| Liability / compliance | Platform shares some responsibility | Business owns more |
| Fit | SMB salons/barbers in Ottawa/Houston | Larger businesses later |

**V1:** **Connect Express** + **Account Links** (`type=account_onboarding`) from portal **Payments → Get paid online**.

### 2.3 Data model (to implement)

| Field | Where | Purpose |
|-------|--------|---------|
| `StripeConnectAccountId` | Tenant or `BusinessProfile` | `acct_…` |
| `StripeConnectChargesEnabled` | Cached from Stripe API | Can accept payments |
| `StripeConnectPayoutsEnabled` | Cached | Payouts to business bank |
| `StripeConnectOnboardingStatus` | `pending` / `complete` / `restricted` | Portal UX |

Refresh status on webhook `account.updated` and before each initialize.

### 2.4 Checkout flow (destination charge + application fee)

Today’s `StripePaymentProvider` creates a **platform-only** Checkout Session. Target shape:

1. Resolve tenant’s **`stripeConnectAccountId`**.
2. Create Session with:
   - `payment_intent_data[transfer_data][destination]` = connected account id  
   - `payment_intent_data[application_fee_amount]` = minor units of `PlatformFeeAmount` (**0 allowed**)
3. Webhook `checkout.session.completed` unchanged; ledger stores gross + fee.

**At 0.75% config but Connect not ready:** use **effective fee = 0** OR block payments (`payment.connect_required`) — see [pricing-and-monetization.md §6.7](./pricing-and-monetization.md#67-gmv-fee-caveats--what-they-mean-and-how-to-get-around-them).

**At 0% platform fee:** still set `application_fee_amount = 0` and **still** use `transfer_data.destination` so all GMV lands on the connected account consistently.

### 2.5 Refunds

Refund via Stripe API with **`refund_application_fee: true`** (full refund) or partial fee reversal. Mirror in `PaymentOrchestrator` refund path.

### 2.6 Stripe Dashboard setup (ops)

1. Enable **Connect** on Adeni platform account.  
2. Configure branding + Express settings.  
3. Register webhook: `account.updated`, `checkout.session.completed`, `charge.refunded`.  
4. Store **platform** secret key + Connect webhook secret in Key Vault.

### 2.7 Portal UX (sketch)

1. Owner opens **Payments** → “Connect payout account”.  
2. API creates Express account (if missing) + Account Link.  
3. Return URL → refresh status → show “Ready to accept cards”.  
4. Initialize deposit/link only when `charges_enabled` (or grace period policy).

---

## 3. Paystack Split (Nigeria)

### 3.1 What it is

Paystack lets a **main account** (Adeni) route each transaction to **subaccounts** (businesses) with a **split** — percentage or flat — at **initialize** time or via a **split group** (`split_code`).

```
Customer pays ₦10,000
  → Paystack processing fee (per Paystack pricing)
  → Split: Adeni main share ₦0 (0%) + Business subaccount ₦10,000 (100%)  [today]
  → Later: Adeni main ₦100 (1%) + Business ₦9,900
```

Subaccounts have their own settlement bank details (business KYC via Paystack).

### 3.2 Subaccount lifecycle

1. **Create subaccount** (API) for tenant — business name, bank, percentage_charge (Paystack’s own subaccount fee if any).  
2. Store `PaystackSubaccountCode` (`ACCT_…`) on tenant.  
3. On **transaction/initialize**, attach **split** (inline or `split_code`).  
4. Verify transaction; webhook `charge.success` as today.

**Onboarding:** Portal **Payments → Bank details for payouts** → collect bank info → API creates/updates subaccount (Paystack may require business verification).

### 3.3 Split at 0% platform fee (your requirement)

Configure split from **`country-pricing.json`** → `gmvCollection.platformFeePercent` (0 for NG):

| `platformFeePercent` | Split behavior |
|----------------------|----------------|
| **0** | **100%** to business subaccount, **0%** to main — still uses split payload so plumbing is live |
| **> 0** | Main account share = fee%; subaccount = remainder (validate Paystack min/max split rules) |

Use **percentage** split type; bearer of Paystack fees configurable (`bearer_type`) — product/legal choice (often **account** = subaccount bears processing).

Example inline split on initialize (conceptual):

```json
{
  "split": {
    "type": "percentage",
    "bearer_type": "account",
    "subaccounts": [
      { "subaccount": "ACCT_business", "share": 100 }
    ]
  }
}
```

When fee &gt; 0, Paystack supports splitting between **main** and **subaccounts** — exact JSON depends on Paystack API version; implement against [Paystack Transaction Split](https://paystack.com/docs/payments/split-payments/) at build time.

**Alternative:** Predefine **Split Groups** in Paystack dashboard (`SPL_…`) for 0% / 0.5% / 1% and pass `split_code` from config — easier ops tuning without deploy.

### 3.4 Data model (to implement)

| Field | Purpose |
|-------|---------|
| `PaystackSubaccountCode` | `ACCT_…` |
| `PaystackSubaccountActive` | Verified / active |
| `PaystackSettlementBankLast4` | Display only |

### 3.5 Refunds

Paystack refund API — confirm whether split reversal is automatic or requires manual adjustment; log **platform fee refunded** on intent. Test in Paystack test mode before prod.

### 3.6 Paystack Dashboard setup (ops)

1. Main account for Adeni Ltd (NG entity).  
2. Enable **Transaction Splits** / subaccounts product.  
3. Webhook URL + secret (already used for payments).  
4. Optional: create split templates for 0% and future non-zero shares.

---

## 4. Configurable GMV collection (country catalog)

Extend [country-pricing.json](../packages/shared/src/data/country-pricing.json) **`gmvCollection`** block (see file). Runtime logic:

```
effectivePlatformFee = country.platformFeePercent
if (!tenant.merchantSettlementReady) {
  if (country.gmvCollection.blockPaymentsUntilReady) → error connect_required
  else effectivePlatformFee = 0  // grace: payments allowed, fee waived
}
platformFeeAmount = amount * effectivePlatformFee / 100
applySplitOrConnect(platformFeeAmount)  // always when mode != none
```

| Flag | Meaning |
|------|---------|
| `mode` | `stripe_connect` \| `paystack_split` |
| `platformFeePercent` | Catalog GMV % (0 for NG) |
| `requireMerchantOnboarding` | Must complete Connect/subaccount before card payments |
| `blockPaymentsUntilOnboarding` | Strict gate vs grace period |
| `splitCode` | Optional Paystack `SPL_…` override |

**SaaS billing** (business → Adeni subscriptions) uses Paystack Subscriptions / Stripe Billing — **separate** from GMV split (Zenoti-style dual agreement).

---

## 5. Implementation checklist (future sprint)

| # | Task |
|---|------|
| 1 | Domain + migration: tenant payment settlement fields (Stripe + Paystack) |
| 2 | `IPaymentSettlementService` — onboarding links, status refresh |
| 3 | `PaystackPaymentProvider` — subaccount create + split on initialize |
| 4 | `StripePaymentProvider` — Connect destination + `application_fee_amount` |
| 5 | `PlatformFeeCalculator` — read **country** from tenant, honor `gmvCollection` |
| 6 | Portal Payments onboarding UI + status chips |
| 7 | Tests: NG 0% split 100/0; US 0.75% with mock Connect; refund fee reversal |
| 8 | Admin: view onboarding status; toggle `platformFeePercent` per country |

**Spec placeholder:** `docs/specs/sprint-XX-payment-settlement-connect-split.md` (create when sprint is scheduled).

---

## 6. FAQ

**Q: Why onboard NG businesses to subaccounts if fee is 0%?**  
A: One code path, correct settlement to **their** bank, flip fee via config, ready for Paystack rev-share conversations without re-onboarding everyone.

**Q: Can we use one Paystack main account for SaaS and splits?**  
A: Yes — same main account; subscription charges and split “main share” both settle to Adeni; keep **ledger categories** separate in Adeni DB.

**Q: Stripe Connect + Stripe Billing same platform account?**  
A: Yes — Connect for GMV, Billing for SaaS MRR; different API objects, one Stripe platform.

**Q: Dev / stub provider?**  
A: `Stub` provider skips Connect/split; `Testing` env uses in-memory or fake settlement ids.
