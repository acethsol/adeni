# Backend module — Payments & Billing Documents

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Payment orchestration** (Paystack, Stripe, stub for dev) without customer fund custody. Supports booking deposits, balance collection, refunds, **billing documents** (invoices/receipts), and webhook-driven state transitions.

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Payments/` — intents, billing documents, domain events |
| Application | `Adeni.Application/Payments/` — `IPaymentProvider`, `IBillingDocumentService` |
| Infrastructure | `Adeni.Infrastructure/Payments/` — orchestrator, providers, fee calculator |
| API | `PaymentsController`, `BillingDocumentsController`, `PublicBillingDocumentsController` |

Registration: `AddPaymentsModule(configuration)`.

---

## Providers

| Provider | Use |
|----------|-----|
| `PaystackPaymentProvider` | Lagos / NG markets |
| `StripePaymentProvider` | NA markets (Sprint 26) |
| `StubPaymentProvider` | Local/dev |
| `MarketDefaultPaymentProvider` | Routes by market config |

---

## Security (webhooks)

- Verify **HMAC** on raw body before parsing (Paystack).
- **Idempotent** success handling in `PaymentOrchestrator`.
- Never mark paid on unsigned body — see `AGENTS.md` Murphy webhook table.

---

## HTTP routes (selected)

| Method | Route |
|--------|-------|
| POST | `/api/v1/payments/intents` |
| POST | `/api/v1/payments/webhook` |
| GET/POST | `/api/v1/payments/documents` |
| GET | `/api/v1/public/documents/{token}` |

---

## Domain events

`PaymentCompleted`, `PaymentFailed`, `RefundInitiated` — handlers in `PaymentEventHandlers`.

---

## Clients

Discover (checkout flows), Portal (payments ledger, mark-paid, documents).

---

## Related docs

- `docs/payments.md`, `docs/payment-provider-settlement.md`
- [Subscriptions](./subscriptions.md), [Booking](./booking.md)
