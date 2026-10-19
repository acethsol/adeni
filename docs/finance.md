# Finance — commerce orchestration (Sprint 17+)

Non-custodial payment orchestration via licensed providers. Adeni stores **payment intent state** and provider references only — no wallet or balance tables.

**Module boundaries:** [specs/finance-module-boundaries.md](./specs/finance-module-boundaries.md)  
**Roadmap:** [roadmap-workflows-payments-queue.md](./roadmap-workflows-payments-queue.md) · **Settlement:** Sprint 34 · **Tips/export:** Finance spec Phase 3

### Invoices vs receipts

| | Status |
|--|--------|
| **Billing documents** | Numbered Receipt/Invoice, PDF, email, public `/documents/{token}` |
| **Receipt alias** | `GET /api/v1/finance/{id}/receipt` → issued receipt document |
| **Reporting / BI** | Dashboard consumes ledger; compensation export Phase 3 |
| **Tax e-invoice / CRA–FIRS** | Out of scope |

### Document APIs (Sprint 28)

| Method | Route |
|--------|-------|
| `GET` | `/api/v1/finance/documents` |
| `GET` | `/api/v1/finance/documents/{id}` (+ `/pdf`, `/email`) |
| `POST` | `/api/v1/finance/documents/invoices` |
| `GET` | `/api/v1/public/documents/{token}` (+ `/pdf`) |

### Floor loop (Sprint 27)

Booking statuses: `Confirmed → CheckedIn → Completed → Paid` (or auto-Paid on complete when deposits cover total).

| Action | Route |
|--------|-------|
| Check in | `POST /api/v1/tenant/bookings/{id}/check-in` |
| Complete | `POST /api/v1/tenant/bookings/{id}/complete` |
| Collect balance | `POST /api/v1/tenant/bookings/{id}/collect-balance` |
| Paid at till | `POST /api/v1/tenant/bookings/{id}/mark-paid` |

## Architecture

| Layer | Location |
|-------|----------|
| Domain | `src/Adeni.Domain/Finance/` |
| Application | `IPaymentProvider`, `IPaymentOrchestrator`, `IBillingDocumentService`, `IPaymentProviderRegistry`, `IStaffCompensationService` |
| Infrastructure | `AddFinanceModule()` — orchestrator, providers, registry, compensation |
| API | `FinanceController`, `BillingDocumentsController` |

Booking coordinates through **`IPaymentOrchestrator`**. **Subscriptions** (SaaS billing) remain a separate module.

## Providers

Configure in `appsettings.json` (`Finance:` preferred; `Payments:` still bound for compatibility):

| `Payments:Provider` | Behavior |
|---------------------|----------|
| `Stub` | Local stub checkout |
| `Paystack` | Paystack REST + hosted checkout |
| `Stripe` | Stripe Checkout Session |
| `MarketDefault` | Market → provider (Phase 2: country registry); webhooks demux by signature |

See [payments.md](./payments.md) legacy note for full env var table (unchanged keys).

## Webhook setup

`https://<api-host>/api/v1/finance/webhook`

Murphy rules: verify signature on raw body, idempotent orchestrator, no full payload logs — [AGENTS.md](../AGENTS.md#webhook-security-murphy).

## API endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/v1/finance/initialize` | Initialize deposit/link/invoice payment |
| `POST` | `/api/v1/finance/links` | Create payment link |
| `GET` | `/api/v1/finance/{id}` | Get payment intent |
| `GET` | `/api/v1/finance/ledger?tenantId=` | Business payment history |
| `POST` | `/api/v1/finance/{id}/refund` | Initiate refund |
| `POST` | `/api/v1/finance/webhook` | Paystack / Stripe webhook |
| `POST` | `/api/v1/finance/stub/confirm` | Stub checkout confirmation (dev) |
| `GET` | `/api/v1/finance/compensation-export` | Tips + commissions CSV/JSON export |

## Platform fee, deposits, audit

Unchanged from prior payments doc — see [pricing-and-monetization.md](./pricing-and-monetization.md), booking deposit flow, audit events `payment.*`.

## Business portal

Route: **`/finance`** — ledger, links, documents (permission `portal.finance`).

## Database

Schema: `payments.*` (physical name unchanged).
