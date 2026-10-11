# Spec: Sprint 27 — In-salon payment loop

> Standing play 1 — draft for review. Do not implement until **Approved**.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 27 — Payment loop |
| **Author** | Cloud Agent |
| **Status** | Implemented |
| **Created** | 2026-10-11 |

---

## 1. Goal (one sentence)

Staff can take a confirmed booking through **check-in → complete → collect balance / paid-at-till**, so Adeni closes the money loop without a POS — and customers/businesses get a simple **payment receipt** (not a full accounting invoice).

---

## 2. Context

- Roadmap: [roadmap-workflows-payments-queue.md](../roadmap-workflows-payments-queue.md) §4
- Module index: [roadmap-modules.md](../roadmap-modules.md) §3.2
- Payments providers shipped: [payments.md](../payments.md), Sprint 26 Stripe
- Today’s booking statuses: `Pending | Confirmed | Rejected | Cancelled` only
- Bookings portal: accept / reject only
- Modules: **Booking** + **Payments** (+ thin portal UI)

### Invoices / receipts — current reality

| Capability | Status |
|------------|--------|
| Payment intent types `deposit` / `link` / `invoice` | ✅ Enum + initialize path |
| Business payment **ledger** | ✅ Portal payments list |
| `ReceiptBaseUrl` callback after checkout | ✅ Config stub (`/checkout/receipt?id=…`) |
| Customer/business **receipt page** (amounts, method, booking) | ❌ Not built |
| Email receipt | ❌ Stub / logging only (Sprint 17 stretch) |
| Tax invoice / PDF / CRA–FIRS numbering | ❌ Not built — out of scope |

**Decision for 27:** Ship a **lightweight receipt** (JSON + simple portal/discover view) tied to a completed payment intent. Defer formal tax invoices / PDF packs. “Invoice” as a payment *type* remains an ad-hoc pay request, not an accounting document.

---

## 3. In scope

### Floor lifecycle

- [x] **27a** Extend `BookingStatus` with `CheckedIn`, `Completed`, `Paid` (keep existing values stable)
- [x] **27b** Tenant APIs: `POST …/check-in`, `…/complete`, `…/mark-paid` (paid-at-till)
- [x] **27c** Transition guards: `Confirmed → CheckedIn → Completed → Paid`; reject illegal jumps; allow `Completed → Paid` only
- [x] **27d** Settlement metadata on booking (or payment link): `PaidOnline` | `PaidExternally` (+ optional external reference / note)
- [x] **27e** `POST …/collect-balance` — initialize payment for remaining amount (`bookingId`, type deposit/link); webhook success → `Paid` + `PaidOnline`

### Money math

- [x] **27f** Remaining balance = booking total − sum of completed payment intents for that booking (deposits + partials)
- [x] **27g** If remaining ≤ 0 after deposit alone, `Complete` may offer “Mark paid” without new charge (or auto-paid on complete — decide in open questions)

### Receipt (lightweight)

- [x] **27h** `GET /api/v1/payments/{id}/receipt` — public-or-scoped receipt DTO (business name, amount, currency, status, paid at, booking ref, method)
- [x] **27i** Minimal receipt UI: discover or portal route using `ReceiptBaseUrl` (no PDF)
- [x] **27j** Portal bookings actions: Check in / Complete / Collect balance / Paid at till; show paid badge

### Docs & tests

- [x] **27k** Update [payments.md](../payments.md) + roadmap §4; shared schemas / api-client
- [x] **27l** Unit + integration: transitions, balance math, mark-paid, collect-balance → webhook → Paid; receipt endpoint

## 4. Out of scope

- Stripe Terminal / hardware readers
- POS bridge (Square, etc.)
- Queue / walk-in kiosk (separate slice)
- Tax invoices, PDF generation, e-invoicing, multi-line retail SKUs
- Tips / split tender / gift cards
- Changing SaaS subscription billing
- Auto no-show after no check-in (later)
- Flutter mobile parity (portal web first; mobile can follow)

---

## 5. API contract

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/v1/tenant/bookings/{id}/check-in` | Business + booking permission | `Confirmed` → `CheckedIn` |
| `POST` | `/api/v1/tenant/bookings/{id}/complete` | Business + booking permission | `CheckedIn` → `Completed` |
| `POST` | `/api/v1/tenant/bookings/{id}/collect-balance` | Business + booking permission | Create payment intent for remaining; return `checkoutUrl` |
| `POST` | `/api/v1/tenant/bookings/{id}/mark-paid` | Business + booking permission | `Completed` → `Paid`, settlement `PaidExternally` |
| `GET` | `/api/v1/payments/{id}/receipt` | Customer (own) / business (tenant) / optional signed public token later | Receipt DTO |
| existing | `/api/v1/payments/webhook` | Provider signature | On success, if intent linked to booking and balance cleared → set `Paid` |

**mark-paid body:**

```json
{
  "externalReference": "optional till/ref",
  "note": "optional"
}
```

**Receipt response (sketch):**

```json
{
  "paymentIntentId": "uuid",
  "bookingId": "uuid-or-null",
  "businessName": "Demo Studio",
  "description": "Balance — Silk press",
  "amount": 10000,
  "currency": "NGN",
  "status": "Completed",
  "paidAt": "2026-10-11T15:00:00Z",
  "method": "Online",
  "providerReference": "cs_…"
}
```

**Booking status mapping (shared):** extend `formatBookingStatus` for new ints `4/5/6`.

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `BookingStatus.cs`; booking fields for settlement / paidAt / external ref (or small value object) |
| Application | Booking service ports for transitions; payment remaining-balance helper |
| Infrastructure | `BookingService` / tenant booking handlers; `PaymentOrchestrator` success → maybe promote booking to Paid |
| Api | `TenantBookingsController` new actions; Payments receipt endpoint |
| Shared / api-client | Schemas, status labels, client methods |
| Portal | `bookings` (+ optional calendar rail actions) |
| Discover (thin) | Receipt page at `ReceiptBaseUrl` if served from discover |
| Tests | Status transitions, balance, mark-paid, receipt auth |
| Docs | This spec → Approved/Implemented; payments + roadmap |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All tenant booking mutations tenant-scoped; receipt scoped to payer or tenant |
| **Auth / roles** | Practitioners with `portal.bookings.self` may check-in/complete **own** bookings only (mirror accept rules) |
| **Cache** | Invalidate booking lists if cached |
| **Audit** | `booking.checked_in`, `booking.completed`, `booking.marked_paid`, `booking.balance_collect_started` |
| **PII** | Receipt shows minimal customer identifiers; no card data |
| **Observability** | Correlation on mutations |

---

## 8. Tests

- [x] Unit: illegal transitions fail closed
- [x] Unit: remaining balance with deposit + zero remaining
- [x] Integration: check-in → complete → mark-paid
- [x] Integration: complete → collect-balance → webhook → Paid
- [x] Receipt: business can read; other tenant denied
- [x] Practitioner cannot complete another stylist’s booking

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [x] Confirmed booking can be checked in, completed, and marked paid at till with optional note
- [x] Collect balance creates a provider checkout for remaining amount; webhook sets Paid
- [x] Portal bookings UI exposes the new actions with clear status labels
- [x] Receipt endpoint returns a usable summary for a completed payment
- [x] No POS / Terminal / tax-invoice requirements
- [x] Docs updated; CI green

---

## 10. Open questions

| Question | Recommendation |
|----------|----------------|
| Auto-mark Paid when complete if deposit covered 100%? | **Yes** — on `complete`, if remaining ≤ 0 → `Paid` + `PaidOnline` |
| Separate status `PaidExternally` vs same `Paid` + settlement enum? | **Same `Paid`** + `settlementSource` |
| Who can mark-paid — receptionist + owner, or any bookings permission? | Any principal with bookings write (incl. self-scoped for own appointments) |
| Receipt public unauthenticated link? | **v1 authenticated / known id**; signed public token later |
| Calendar rail actions in same sprint? | **Nice-to-have**; bookings list is enough for 27 |

---

## 11. Follow-on

1. Email receipt via Notifications  
2. PDF / tax invoice pack (market-specific)  
3. Queue + kiosk check-in  
4. Terminal + POS bridge  
