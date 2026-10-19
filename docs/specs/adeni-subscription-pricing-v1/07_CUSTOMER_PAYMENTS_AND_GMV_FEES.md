# Customer Payments & GMV Platform Fees

Separate from SaaS billing.

## Three economic layers
A. Service amount — set by business, paid by customer
B. Provider processing fee — Stripe/Paystack/etc.
C. Adeni platform fee — business-paid, currently 0% at launch

## Customer total
Adeni platform fee must not increase checkout total.

## Provider abstraction
Support:
- create checkout/payment
- confirm payment
- refund
- status lookup
- provider webhooks
- future application/split fee
- reconciliation references

Provider-specific code belongs in Infrastructure.

## Persist exact fee facts
Payment should preserve:
- GrossAmount
- ServiceAmount
- TaxAmount
- TipAmount
- PlatformFeeBaseAmount
- PlatformFeePercent
- PlatformFeeAmount
- ProviderFeeAmount when known
- RefundedAmount
- ProviderPaymentReference
- PaymentStatus

At launch, platform fee is 0%, so do not overbuild complex fee allocation rules.

## Provider revenue share
Separate from Adeni platform fee.
Do not assume it exists until there is a commercial agreement.
