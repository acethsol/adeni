# Payments (Sprint 17 + Sprint 26)

Non-custodial payment orchestration via licensed providers. Adeni stores **payment intent state** and provider references only — no wallet or balance tables.

**Roadmap:** closing the in-salon payment loop and optional POS bridge — see [roadmap-workflows-payments-queue.md](./roadmap-workflows-payments-queue.md).  
**Stripe NA:** [Sprint 26](./specs/sprint-26-stripe-na-payments.md) ✅ · **Payment loop + receipt:** [Sprint 27](./specs/sprint-27-payment-loop.md) (draft).

### Invoices vs receipts

| | Today | Sprint 27 |
|--|-------|-----------|
| **Receipt** | Callback URL stub + ledger only | Lightweight receipt DTO + simple UI for completed payments |
| **Invoice** | Payment *type* for ad-hoc pay requests | Unchanged — not a tax/accounting document |
| **Tax PDF / e-invoice** | Not built | Still out of scope |

## Architecture

| Layer | Location |
|-------|----------|
| Domain | `src/Adeni.Domain/Payments/` — `PaymentIntentRecord`, statuses, types, domain events |
| Application | `src/Adeni.Application/Payments/` — `IPaymentProvider`, `IPaymentOrchestrator`, `IPlatformFeeCalculator` |
| Infrastructure | `PaymentOrchestrator`, `PaystackPaymentProvider`, `StripePaymentProvider`, `MarketDefaultPaymentProvider`, `StubPaymentProvider` |
| API | `src/Adeni.Api/Controllers/PaymentsController.cs` |

Booking and other modules coordinate through **`IPaymentOrchestrator`** (Application port). Infrastructure modules do not reference each other directly (NetArchTest enforced).

## Providers

Configure in `appsettings.json`:

```json
{
  "Payments": {
    "Provider": "Stub",
    "StubCheckoutBaseUrl": "/checkout/stub",
    "ReceiptBaseUrl": "/checkout/receipt"
  },
  "Paystack": {
    "SecretKey": "",
    "PublicKey": "",
    "WebhookSecret": "",
    "BaseUrl": "https://api.paystack.co"
  },
  "Stripe": {
    "SecretKey": "",
    "PublishableKey": "",
    "WebhookSecret": "",
    "BaseUrl": "https://api.stripe.com",
    "WebhookToleranceSeconds": 300
  }
}
```

| `Payments:Provider` | Behavior |
|---------------------|----------|
| `Stub` | Local stub checkout at `/checkout/stub/{reference}` |
| `Paystack` | Paystack REST initialize + hosted `authorization_url` |
| `Stripe` | Stripe Checkout Session + hosted `url` |
| `MarketDefault` | Lagos / default → Paystack; Ottawa → Stripe; webhooks routed by signature header |

`markets.json` documents intended provider per market (`paymentProvider`: `paystack` \| `stripe`). Runtime `MarketDefault` maps `ottawa` → Stripe and everything else → Paystack.

## Environment variables

| Variable | Description |
|----------|-------------|
| `Payments__Provider` | `Stub`, `Paystack`, `Stripe`, or `MarketDefault` |
| `Paystack__SecretKey` | Paystack secret key (server) |
| `Paystack__PublicKey` | Paystack public key (client metadata if needed) |
| `Paystack__WebhookSecret` | HMAC secret for `x-paystack-signature` verification |
| `Stripe__SecretKey` | Stripe secret key (server) |
| `Stripe__PublishableKey` | Stripe publishable key (client if needed) |
| `Stripe__WebhookSecret` | Secret for `Stripe-Signature` verification |
| `Stripe__WebhookToleranceSeconds` | Max signature timestamp skew (default 300; `0` disables) |

## Webhook setup

Same URL for both providers: `https://<api-host>/api/v1/payments/webhook`

1. **Paystack** — set webhook URL; copy secret into `Paystack:WebhookSecret`
2. **Stripe** — add endpoint for `checkout.session.completed` (and refund events as needed); copy signing secret into `Stripe:WebhookSecret`
3. With `MarketDefault`, both secrets must be set in Staging/Production

Events are parsed into normalized payloads and applied idempotently (terminal states are not double-applied).

### Webhook security (Murphy)

From [@mattmurphyai](https://www.instagram.com/reel/DcbzcfyioD9/): never accept payment webhooks without signature verification — fake payloads can mark orders paid without real money.

| Rule | Implementation |
|------|----------------|
| Verify signature first | Paystack: HMAC SHA512 vs `x-paystack-signature`; Stripe: HMAC SHA256 of `{t}.{body}` vs `Stripe-Signature` v1; `FixedTimeEquals` |
| Idempotent processing | `PaymentOrchestrator` — skip if intent already `Completed` / refunded |
| Resolve by reference | Lookup `PaymentIntentRecord` by `providerReference` (Stripe Checkout Session id `cs_…`) |
| No payload logging | Audit event type + reference only |
| Staging/prod | Provider webhook secret(s) required; stub confirm route dev-only |

Full agent rules: [AGENTS.md](../AGENTS.md#webhook-security-murphy).  
**Gap:** `POST /api/v1/subscriptions/webhook` still lacks verification — apply same rules when wiring subscription billing.

## API endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/v1/payments/initialize` | Initialize deposit/link/invoice payment |
| `POST` | `/api/v1/payments/links` | Create payment link |
| `GET` | `/api/v1/payments/{id}` | Get payment intent |
| `GET` | `/api/v1/payments/ledger?tenantId=` | Business payment history |
| `POST` | `/api/v1/payments/{id}/refund` | Initiate refund |
| `POST` | `/api/v1/payments/webhook` | Paystack / Stripe webhook |
| `POST` | `/api/v1/payments/stub/confirm` | Stub checkout confirmation (dev) |

## Platform fee

- Configured per market in `packages/shared/src/data/markets.json` → `platformFeePercent`
- Lagos / Ottawa pilot default: **0%**
- Fee amount stored on `PaymentIntentRecord.PlatformFeeAmount` and passed as provider metadata (`platform_fee`)
- **v1:** platform collects; Stripe Connect destination charges deferred

## Booking deposits

1. Business enables **deposits** capability (category workflow) and sets **deposit %** in portal settings
2. Customer confirms booking → API creates pending booking → initializes deposit payment
3. On `PaymentCompleted`, domain handler confirms the linked booking

## Audit trail

All state transitions are written via `IAuditLogWriter`:

- `payment.initialized`
- `payment.confirmed`
- `payment.failed`
- `payment.refunded`
- `payment.webhook_received`

## Error codes

Client-facing errors use dotted `payment.*` codes (see `ErrorCodes.cs` and `packages/shared/src/i18n/errors.ts`). Raw provider errors are never exposed.

## Business portal

Web: `/business/payments` — ledger, create link, copy/share WhatsApp, refund completed payments.

## Database

Schema: `payments.payment_intents` (migration `Sprint17PaymentsOrchestration`):

- `Type`, `PlatformFeeAmount`, `Description`, `CustomerEmail`, `CallbackUrl`, `IdempotencyKey`
- `tenancy.business_profiles.DepositPercent` for booking deposit configuration
