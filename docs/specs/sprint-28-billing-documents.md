# Spec: Sprint 28 — Billing documents (receipts & invoices)

> Standing play 1 — draft for review. Do not implement until **Approved**.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 28 — Billing documents |
| **Author** | Cloud Agent |
| **Status** | Implemented |
| **Created** | 2026-10-11 |

---

## 1. Goal (one sentence)

Businesses and customers get **real receipts and invoices** (numbered documents, shareable page, PDF, email) from Adeni’s payment ledger — without turning Adeni into accounting software or a BI suite.

---

## 2. Context

- Prior art: [Sprint 27 payment loop](./sprint-27-payment-loop.md) shipped floor states + lightweight `GET /payments/{id}/receipt` DTO
- Payments: [payments.md](../payments.md) · Stripe + Paystack · non-custodial
- Module index: [roadmap-modules.md](../roadmap-modules.md) §3.2 Payments, §3.5 Dashboards
- Notifications today: `INotificationDispatcher` (logging / preference-aware) — email is stub-grade
- Decision from product review: **robust documents ≠ reporting tool**. Reporting stays in Dashboards/Insights later and *consumes* the same ledger.

### Why not a reporting tool?

| Job | Owner module | Sprint 28 |
|-----|--------------|-----------|
| Proof of payment / bill to pay | **Billing documents** (Payments) | ✅ In scope |
| “How did we do this week?” trends | **Dashboards** | ❌ Out of scope |
| Tax filing / full books | External (QuickBooks etc.) | ❌ Out of scope |

### Current gaps after Sprint 27

| Capability | Status |
|------------|--------|
| Receipt JSON DTO | ✅ Lightweight |
| Document number sequence | ❌ |
| Line items (service + add-ons) | ❌ |
| Shareable public receipt page | ❌ (ReceiptBaseUrl stub only) |
| PDF generation | ❌ |
| Email delivery of receipt/invoice | ❌ (log-only notifications) |
| Invoice as billable document (not just payment *type*) | ❌ |

---

## 3. In scope

### Document model

- [x] **28a** Introduce `BillingDocument` (or equivalent) in Payments domain:
  - `Kind`: `Receipt` | `Invoice`
  - `Status`: `Draft` | `Issued` | `Void` (invoices may stay `Draft` until sent; receipts issue on payment complete)
  - Links: `TenantId`, optional `BookingId`, optional `PaymentIntentId`
  - `DocumentNumber` (tenant-scoped sequence, human-readable e.g. `RCP-2026-00042` / `INV-2026-00017`)
  - Parties: business display name + customer display/email (snapshot at issue time — no live PII join required for PDF)
  - Money: currency, subtotal, taxTotal (nullable v1), total
  - Line items: description, quantity, unitAmount, lineTotal (from booking lines when linked)
  - Timestamps: `IssuedAt`, `CreatedAt`, `UpdatedAt`
- [x] **28b** SQL project table(s) under `payments` schema + EF mapping
- [x] **28c** Numbering service: per-tenant, per-kind monotonic sequence (transaction-safe)

### Issue rules

- [x] **28d** **Receipt**: auto-issue when a payment intent reaches `Completed` (idempotent — one receipt per payment intent)
- [x] **28e** **Receipt for paid-at-till**: issue when booking transitions to `Paid` with `ExternalTill` settlement (no payment intent); link booking only
- [x] **28f** **Invoice**: business can create from completed booking balance **or** ad-hoc amount (extends today’s payment-link/invoice intent); issuing an invoice may initialize/collect payment but the document is first-class
- [x] **28g** Void with audit reason (no hard delete); voided docs keep number

### APIs

- [x] **28h** `GET /api/v1/payments/documents/{id}` — document DTO (auth: tenant staff with payments/bookings, or customer who owns linked booking/payment)
- [x] **28i** `GET /api/v1/payments/documents` — tenant list (filter by kind, bookingId, date range)
- [x] **28j** `POST /api/v1/payments/documents/invoices` — create/issue invoice for booking or ad-hoc
- [x] **28k** `POST /api/v1/payments/documents/{id}/void`
- [x] **28l** `GET /api/v1/payments/documents/{id}/pdf` — `application/pdf`
- [x] **28m** `POST /api/v1/payments/documents/{id}/email` — send to customer email (or override)
- [x] **28n** Public share: `GET /api/v1/public/documents/{token}` (signed/opaque token, no auth) for ReceiptBaseUrl / share links
- [x] **28o** Keep / evolve `GET /api/v1/payments/{id}/receipt` as thin alias → latest Receipt document for that payment (compat)

### PDF & email

- [x] **28p** HTML → PDF renderer (server-side; prefer QuestPDF or similar already-compatible .NET library — decide in open questions)
- [x] **28q** Brand-light template: business name, document kind + number, date, line items, total, method / “Paid at till”, Adeni footer (“Issued via Adeni”)
- [x] **28r** Email via `INotificationDispatcher` with HTML body + optional PDF attachment **or** deep link to public document URL if attachment is hard in v1
- [x] **28s** Prefer real SMTP/provider adapter behind dispatcher if still logging-only — minimum: structured send + link; stretch: SendGrid/SES if credentials exist in env

### Surfaces

- [x] **28t** Discover (or shared) public document page at `ReceiptBaseUrl` /documents/{token}
- [x] **28u** Portal: Payments ledger → View receipt / Download PDF / Email; Bookings paid row → View receipt
- [x] **28v** Shared schemas + api-client

### Docs & tests

- [x] **28w** Update [payments.md](../payments.md), roadmap module briefs; clarify Dashboards ≠ documents
- [x] **28x** Unit + integration: issue on payment complete; till receipt; number uniqueness; void; PDF bytes non-empty; email dispatch invoked; tenant isolation on list/get

---

## 4. Out of scope

- **Reporting / BI tools** (Metabase, Power BI, custom analytics packs) — Dashboards module later
- Tax authority e-invoicing (FIRS, CRA e-file), VAT/GST registration management
- Multi-currency conversion, credit notes beyond Void, recurring invoices
- Retail inventory SKUs / product catalog invoices
- Tips / split tender / gift cards on documents
- Stripe Terminal / POS bridge
- Full QuickBooks / Xero sync
- Changing SaaS subscription billing (separate)
- Flutter mobile parity (portal + public web first)

---

## 5. API contract (draft)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/payments/documents` | Portal payments (or bookings) | List tenant documents |
| `GET` | `/api/v1/payments/documents/{id}` | Portal / customer (scoped) | Document DTO |
| `GET` | `/api/v1/payments/documents/{id}/pdf` | Portal / customer (scoped) | PDF download |
| `POST` | `/api/v1/payments/documents/{id}/email` | Portal payments | `{ "toEmail": "optional" }` |
| `POST` | `/api/v1/payments/documents/invoices` | Portal payments | Create invoice from booking or amount |
| `POST` | `/api/v1/payments/documents/{id}/void` | Portal payments | `{ "reason": "…" }` |
| `GET` | `/api/v1/public/documents/{token}` | Public | Document DTO for share page |
| `GET` | `/api/v1/payments/{paymentIntentId}/receipt` | Existing | Alias → receipt document |

**Invoice create body (example):**

```json
{
  "bookingId": "uuid-optional",
  "amount": 8000,
  "currency": "NGN",
  "description": "Balance for silk press",
  "customerEmail": "guest@example.com",
  "issue": true,
  "initializePayment": true
}
```

**Document DTO (example fields):**

```json
{
  "id": "…",
  "kind": "receipt",
  "status": "issued",
  "documentNumber": "RCP-2026-00042",
  "businessName": "Glow Studio",
  "customerName": "Ada Okoro",
  "customerEmail": "a***@…",
  "bookingId": "…",
  "paymentIntentId": "…",
  "currency": "NGN",
  "subtotal": 8000,
  "taxTotal": null,
  "total": 8000,
  "method": "Online payment",
  "issuedAt": "2026-10-11T15:00:00Z",
  "lines": [
    { "description": "Silk press", "quantity": 1, "unitAmount": 8000, "lineTotal": 8000 }
  ],
  "publicUrl": "https://…/documents/tok_…"
}
```

---

## 6. Data model (sketch)

```
payments.billing_documents
  Id, TenantId, Kind, Status, DocumentNumber,
  BookingId?, PaymentIntentId?,
  BusinessNameSnapshot, CustomerNameSnapshot, CustomerEmailSnapshot,
  Currency, Subtotal, TaxTotal, Total,
  MethodLabel, PublicToken (unique),
  VoidReason?, IssuedAt?, CreatedAt, UpdatedAt

payments.billing_document_lines
  Id, DocumentId, TenantId, SortOrder,
  Description, Quantity, UnitAmount, LineTotal

payments.billing_document_sequences  (optional)
  TenantId, Kind, NextValue
```

Tenant isolation: query filters + API checks (same pattern as payment intents).

---

## 7. Security & compliance

| Topic | Requirement |
|-------|-------------|
| Tenant isolation | All mutations/list scoped; public token is unguessable (128-bit+) |
| PII | Snapshots on issue; mask email in logs; no card PANs on documents |
| Audit | `document.issued`, `document.voided`, `document.emailed` |
| Idempotency | Receipt issue on `PaymentCompleted` once per payment intent |
| Non-custodial | Document is salon-issued proof; Stripe/Paystack remain rails |

---

## 8. UX notes

- Receipt page: one composition — business name, “Receipt”, number, total, lines, date — not a dashboard
- PDF matches the page closely (print-friendly)
- Portal: no new top-level module — attach to Payments + Bookings actions
- Invoice create: keep short (booking picker or amount + email)

---

## 9. Open questions

1. **PDF library** — QuestPDF vs Playwright HTML print? Prefer QuestPDF (no browser dependency in API).
2. **Tax lines** — store nullable `taxTotal` only in v1, or skip tax fields until market packs?
3. **Email attachment** — attach PDF bytes vs link-only for v1 SMTP stub?
4. **Invoice without payment** — allow unpaid issued invoices, or always `initializePayment: true` when issued?
5. **Who may email** — `portal.payments` only, or also `portal.bookings` for till receipts?

**Proposed defaults if unanswered at approve:** QuestPDF; nullable tax; link-first email with PDF download in portal; invoices may issue unpaid; email allowed for payments **or** bookings permission.

---

## 10. Acceptance criteria

- [x] Completing an online payment produces exactly one Issued receipt with a stable document number
- [x] Paid-at-till produces a receipt linked to the booking (no payment intent required)
- [x] PDF endpoint returns a non-empty PDF for an issued document
- [x] Public token URL renders document without auth; invalid token → 404
- [x] Email action invokes notification dispatcher with document link (and attachment if enabled)
- [x] Cross-tenant document access denied
- [x] Void prevents further email/PDF “active” presentation but preserves number for audit
- [x] Docs state clearly: reporting/BI is **not** part of this sprint

---

## 11. Follow-ups (not this sprint)

1. Market tax packs (GST/HST, VAT) as document adapters  
2. Credit notes / partial refunds on documents  
3. Owner dashboard GMV from issued receipts (Dashboards module)  
4. Accounting export (CSV) / QuickBooks sync  
5. Branded PDF templates per public-page theme  
