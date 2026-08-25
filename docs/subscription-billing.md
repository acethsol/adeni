# Subscription billing (Sprint 16 stub)

Provider-agnostic design for SaaS subscription billing. **Real Paystack Subscriptions integration is deferred to Sprint 17+** — this document defines the port, stub behavior, and webhook shape.

## Application port

`ISubscriptionBillingProvider` in `Adeni.Application.Subscriptions`:

| Method | Purpose |
|--------|---------|
| `CreateCheckoutAsync` | Start upgrade checkout for a target tier |
| `ParseWebhookAsync` | Normalize provider webhook → `SubscriptionWebhookEvent` |

Stub implementation: `StubSubscriptionBillingProvider` (returns fake checkout URL, logs webhooks).

## Checkout request

```json
{
  "tenantId": "uuid",
  "targetTier": "pro",
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

**No signature verification in stub** — this is the vulnerability pattern from [@mattmurphyai webhook security guidance](https://www.instagram.com/reel/DcbzcfyioD9/). Future Paystack wiring **must** validate `x-paystack-signature` on the raw body before parsing, with idempotent tier updates — same rules as [payments.md](./payments.md#webhook-security-murphy) and [AGENTS.md](../AGENTS.md#webhook-security-murphy).

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

## Entitlements vs billing

- **Entitlements** (`IEntitlementsService`) — source of truth for gating today; tier stored on `tenants.subscription_tier`.
- **Billing provider** — orchestrates payment; on webhook success, admin or automated job updates `subscription_tier`.
- Sprint 16g admin override allows pilots without billing.

## Related

- [product-strategy.md §4.1](./product-strategy.md#41-saas-subscriptions--foundation-mrrarr)
- Sprint 17 — Paystack payment orchestration (deposits, links; may extend subscription billing)
