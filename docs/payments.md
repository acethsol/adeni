# Payments (Sprint 17)

Non-custodial payment orchestration via licensed providers. Adeni stores **payment intent state** and provider references only — no wallet or balance tables.

## Architecture

| Layer | Location |
|-------|----------|
| Domain | `src/Adeni.Domain/Payments/` — `PaymentIntentRecord`, statuses, types, domain events |
| Application | `src/Adeni.Application/Payments/` — `IPaymentProvider`, `IPaymentOrchestrator`, `IPlatformFeeCalculator` |
| Infrastructure | `src/Adeni.Infrastructure/Payments/` — `PaymentOrchestrator`, `PaystackPaymentProvider`, `StubPaymentProvider` |
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
  }
}
```

| `Payments:Provider` | Behavior |
|---------------------|----------|
| `Stub` | Local stub checkout at `/checkout/stub/{reference}` |
| `Paystack` | Paystack REST initialize + hosted `authorization_url` |

## Environment variables

| Variable | Description |
|----------|-------------|
| `Payments__Provider` | `Stub` or `Paystack` |
| `Paystack__SecretKey` | Paystack secret key (server) |
| `Paystack__PublicKey` | Paystack public key (client metadata if needed) |
| `Paystack__WebhookSecret` | HMAC secret for `x-paystack-signature` verification |

## Webhook setup

1. In Paystack dashboard, set webhook URL to: `https://<api-host>/api/v1/payments/webhook`
2. Copy the webhook secret into `Paystack:WebhookSecret`
3. Paystack sends `charge.success`, `charge.failed`, and refund events — parsed into normalized payloads and applied idempotently (terminal states are not double-applied)

### Webhook security (Murphy)

From [@mattmurphyai](https://www.instagram.com/reel/DcbzcfyioD9/): never accept payment webhooks without signature verification — fake payloads can mark orders paid without real money.

| Rule | Implementation |
|------|----------------|
| Verify signature first | `PaystackPaymentProvider` — HMAC SHA512 of raw body vs `x-paystack-signature`; `FixedTimeEquals` |
| Idempotent processing | `PaymentOrchestrator` — skip if intent already `Completed` / refunded |
| Resolve by reference | Lookup `PaymentIntentRecord` by `providerReference`, not client-supplied intent id in webhook body |
| No payload logging | Audit event type + reference only |
| Staging/prod | `Paystack:WebhookSecret` must be set; stub confirm route dev-only |

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
| `POST` | `/api/v1/payments/webhook` | Paystack webhook |
| `POST` | `/api/v1/payments/stub/confirm` | Stub checkout confirmation (dev) |

## Platform fee

- Configured per market in `packages/shared/src/data/markets.json` → `platformFeePercent`
- Lagos pilot default: **0%**
- Fee amount stored on `PaymentIntentRecord.PlatformFeeAmount` and passed to Paystack metadata as `platform_fee`

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

Client-facing errors use dotted `payment.*` codes (see `ErrorCodes.cs` and `packages/shared/src/i18n/errors.ts`). Raw Paystack errors are never exposed.

## Business portal

Web: `/business/payments` — ledger, create link, copy/share WhatsApp, refund completed payments.

## Database

Schema: `payments.payment_intents` (migration `Sprint17PaymentsOrchestration`):

- `Type`, `PlatformFeeAmount`, `Description`, `CustomerEmail`, `CallbackUrl`, `IdempotencyKey`
- `tenancy.business_profiles.DepositPercent` for booking deposit configuration
