# Subscription billing (Sprint 16 stub → Sprint 33 live)

Provider-agnostic design for SaaS subscription billing. **Implementation spec:** [sprint-33-saas-subscription-billing-mrr.md](./specs/sprint-33-saas-subscription-billing-mrr.md) (after [sprint-32-portal-dashboard-revisit.md](./specs/sprint-32-portal-dashboard-revisit.md)).

**MRR (ops):** recurring revenue from business → Adeni subscriptions only — not GMV or customer invoices ([product-strategy §4.1](./product-strategy.md#41-saas-subscriptions--foundation-mrrarr)). Admin MRR v1 ships with Sprint 33; broader GMV/MRR dashboards remain Sprint 20e.

Default local config uses **Stub**; enable **Paystack** when keys and (optionally) plan codes are configured — no staging deploy required to develop against Paystack test mode.

## Application port

`ISubscriptionBillingProvider` in `Adeni.Application.Subscriptions`:

| Method | Purpose |
|--------|---------|
| `CreateCheckoutAsync` | Start upgrade checkout for a target tier |
| `ParseWebhookAsync` | Normalize provider webhook → `SubscriptionWebhookEvent` |

| Provider | When |
|----------|------|
| `Stub` | Local dev — fake checkout URL + flat JSON webhooks |
| `Paystack` | Paystack test/live keys; `transaction/initialize` with plan or catalog amount |
| `MarketDefault` | Lagos → Paystack; unsigned body → stub parser for local tests |

Implementations: `StubSubscriptionBillingProvider`, `PaystackSubscriptionBillingProvider`, `MarketDefaultSubscriptionBillingProvider`.

## Checkout request

```json
{
  "tenantId": "uuid",
  "targetSku": "professional",
  "customerEmail": "owner@example.com",
  "successUrl": "https://app.adeni.com/business/plan?upgraded=1",
  "cancelUrl": "https://app.adeni.com/business/plan"
}
```

## Stub checkout response

```json
{
  "checkoutUrl": "https://.../success?stub_checkout=1&reference=stub_sub_...&tier=pro",
  "providerReference": "stub_sub_{tenantId}_pro",
  "status": "pending"
}
```

## Webhook endpoint (stub)

`POST /api/v1/subscriptions/webhook`

**Paystack** webhooks require `x-paystack-signature` (HMAC SHA512 on raw body) when `Paystack:WebhookSecret` is set — same Murphy rules as [payments.md](./payments.md#webhook-security-murphy). **Stub** accepts unsigned flat JSON in Development/Testing.

### Configuration (`appsettings` / Key Vault)

```json
"SubscriptionBilling": {
  "Provider": "MarketDefault",
  "Paystack": {
    "ProfessionalPlanCode": "PLN_xxx",
    "BusinessPlanCode": "PLN_yyy"
  }
},
"Paystack": {
  "SecretKey": "sk_test_...",
  "WebhookSecret": "..."
}
```

Plan codes are optional: without them, checkout uses **one-time** `transaction/initialize` with catalog amount (good for Paystack test mode before plans exist). Metadata always includes `tenant_id` and `target_tier` for webhook tier sync.

### Normalized event shape

After parsing, handlers receive:

```json
{
  "eventType": "subscription.create",
  "providerReference": "SUB_abc123",
  "tenantId": "uuid-or-null",
  "tier": "pro",
  "status": "active",
  "occurredAt": "2026-08-22T12:00:00Z"
}
```

### Expected Paystack-aligned events (future)

| eventType | When | Action |
|-----------|------|--------|
| `subscription.create` | Customer completes checkout | Set tenant tier, audit log |
| `subscription.disable` | Cancelled or failed renewal | Downgrade to Free (grace period TBD) |
| `subscription.not_renew` | Expiring soon | Notify business owner |
| `invoice.payment_failed` | Charge failed | Flag tenant, retry policy |

### Example stub webhook body

```json
{
  "event": "subscription.create",
  "reference": "stub_sub_abc_pro",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "tier": "pro",
  "status": "active"
}
```

## Admin MRR (Sprint 33b)

`GET /api/v1/admin/metrics/subscription` (admin MFA) returns paid / trialing / past-due counts, SKU mix, and MRR v1:

- **Authoritative** when charge rows exist this month: sum of `amountMinor` by currency (`mrrSource: synced_charges`).
- **Estimated** otherwise: paying tenants × catalog list price by primary location market (`mrrSource: estimated_list_price`).

List prices on the server mirror `packages/shared/data/country-pricing.json` via `SubscriptionCatalogPrices`.

## Entitlements vs billing

- **Entitlements** (`IEntitlementsService`) — source of truth for gating today; tier stored on `tenants.subscription_tier`.
- **Billing provider** — orchestrates payment; on webhook success, admin or automated job updates `subscription_tier`.
- Sprint 16g admin override allows pilots without billing.

## Related

- [product-strategy.md §4.1](./product-strategy.md#41-saas-subscriptions--foundation-mrrarr)
- Sprint 17 — Paystack payment orchestration (deposits, links; may extend subscription billing)
