# Customer verification (V1)

Platform customer email/phone trust — not business KYC. Spec: [customer-verification-trust-foundation.md](./specs/customer-verification-trust-foundation.md).

## Configuration (`CustomerVerification`)

| Key | Purpose |
|-----|---------|
| `PhoneVerificationHmacKey` | HMAC-SHA256 for phone fingerprints and guest queue proof tokens (Key Vault in staging/prod) |
| `AutoVerifyCustomerInTesting` | When `true`, skips email/phone gates (API `Testing` + local dev default in Development) |
| `GuestQueuePhoneProof` | `RelaxedInDevelopment` or `Required` |

## API

- `GET /api/v1/me/verification`
- `POST /api/v1/me/verification/refresh` (JWT-only sync)

## Gates (when `AutoVerifyCustomerInTesting` is false)

- Booking / deposit: email verified
- Review: email verified (+ existing booking rules)
- Waitlist, package/membership purchase: email + phone verified
- Public queue: phone proof on join (`phoneVerificationProof` on join body)
- Messaging: unchanged (auth + conversation context)

## Auth0 staging go/no-go

Before enabling phone gates in production, validate CA + NG SMS, verification UX, and token claims; record outcome here or in [auth0-setup.md](./auth0-setup.md).
