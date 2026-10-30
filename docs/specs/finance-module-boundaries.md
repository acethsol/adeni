# Spec: Finance module boundaries

| Field | Value |
|-------|-------|
| **Sprint** | Cross-cutting — Finance umbrella (pre–Sprint 34 settlement, pre–tips) |
| **Author** | Team |
| **Status** | Implemented — Phases 1–4 |
| **Created** | 2026-10-19 |

---

## 1. Goal (one sentence)

Establish **Finance** as the extractable bounded context for commerce money movement, provider routing, billing documents, and (later) staff tips/commissions with payroll **exports** — while keeping **Subscriptions** as a separate SaaS module.

---

## 2. Context

- [roadmap-modules.md](../roadmap-modules.md) — product row “Payments & finance”
- [product-strategy.md](../product-strategy.md) §4.3, §8 — orchestration not custody; no full payroll
- [architecture.md](../architecture.md) — modular monolith, module extraction
- [payments.md](../payments.md) → superseded by [finance.md](../finance.md) for HTTP/API reference
- **Subscriptions** remain [subscription-billing.md](../subscription-billing.md) — entitlements + tenant MRR

---

## 3. Decision record

| # | Decision |
|---|----------|
| 1 | **Finance** is the peel-off module name (future service). Code namespaces may still say `Payments` until a dedicated rename sprint. |
| 2 | **Subscriptions** stays its own module; shares **market/country → provider** registry concept only (no merged orchestrator). |
| 3 | **Tips / commissions** = allocation records + portal/API **export** (CSV/JSON) for external payroll tools — **not** pay runs, withholding, or custody. |
| 4 | **V1 dev:** replace `/api/v1/payments/*` with `/api/v1/finance/*` cleanly (no alias routes). Update Paystack/Stripe webhook URLs in provider dashboards when deploying. |
| 5 | Portal route **`/finance`**, permission **`portal.finance`** (replaces `portal.payments`). |
| 6 | SQL schema **`finance.*`** (aligned with Finance module; object scripts under `db/Adeni.Database/finance/`). |
| 7 | Config section **`Payments:`** in `appsettings` unchanged for now (provider keys); optional `Finance:` alias later. |

---

## 4. In scope (by phase)

### Phase 1 — ✅ This spec (boundaries + route migration)

- [x] This document approved
- [x] `AddFinanceModule()` composition root (wraps existing payment stack)
- [x] API routes under `/api/v1/finance/*` (intents, ledger, webhooks, documents, stub)
- [x] `@adeni/api-client` + integration tests updated
- [x] Portal nav `/finance`, `portal.finance`
- [x] [finance.md](../finance.md) as API runbook; [payments.md](../payments.md) pointer

### Phase 2 — Provider registry ✅

- [x] `IPaymentProviderRegistry` — tenant market / country → provider key
- [x] Market catalog: `defaultCommerceProvider`, `defaultBillingProvider` (JSON + `catalog.markets`)
- [x] `MarketDefaultPaymentProvider` + subscription billing use registry

### Phase 3 — Staff compensation ✅

- [x] Tips on `initialize` (intent fields) and `mark-paid` request
- [x] `StaffCompensationAllocation` + commission on paid booking (`StaffCommissionPercent` on profile)
- [x] `GET /api/v1/finance/compensation-export`
- [x] Portal Finance tab “Staff export”

### Phase 4 — Namespace / config ✅

- [x] `Adeni.*.Finance` namespaces (Domain / Application / Infrastructure)
- [x] `Finance:` config section (falls back to legacy `Payments:`)

---

## 5. Out of scope

- Wallet, balance, or custody tables
- Full payroll, tax remittance, direct deposit to staff
- Merging Subscriptions into Finance
- Breaking `/api/v1/public/documents/{token}` (unchanged)
- Production deprecation headers for old payment routes (dev-only clean cut)

---

## 6. Module map

```
Finance (extractable)
├── Commerce          — payment intents, checkout, webhooks, refunds, ledger
├── Documents         — receipts, invoices, PDF, email, public token
├── Providers         — Paystack, Stripe, Stub, MarketDefault (+ registry Phase 2)
└── Compensation      — tips, commissions, exports (Phase 3)

Subscriptions (separate)
├── Entitlements, SKUs, tenant billing status
└── ISubscriptionBillingProvider (may use shared registry)
```

**Ports (Application):** `IPaymentProvider`, `IPaymentOrchestrator`, `IBillingDocumentService`; future `IStaffCompensationService`, `IPaymentProviderRegistry`.

**Cross-module:** Booking → `IPaymentOrchestrator`; Staff → compensation export reads staff IDs only via Staff port.

---

## 7. API contract (Phase 1)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/v1/finance/initialize` | Customer JWT | Create intent + checkout URL |
| `POST` | `/api/v1/finance/links` | Business + `portal.finance` | Payment link |
| `GET` | `/api/v1/finance/{id}` | Customer or business (scoped) | Intent |
| `GET` | `/api/v1/finance/{id}/receipt` | Scoped | Receipt document or fallback DTO |
| `GET` | `/api/v1/finance/ledger?tenantId=` | Business + `portal.finance` | Ledger page |
| `POST` | `/api/v1/finance/{id}/refund` | Business + `portal.finance` | Refund |
| `POST` | `/api/v1/finance/webhook` | Provider HMAC | Paystack / Stripe (MarketDefault demux) |
| `POST` | `/api/v1/finance/stub/confirm` | Dev/Testing only | Stub confirm |
| `GET` | `/api/v1/finance/documents` | Business | List billing documents |
| `GET/POST/...` | `/api/v1/finance/documents/...` | As Sprint 28 | Invoices, PDF, email, void |
| `GET` | `/api/v1/public/documents/{token}` | Anonymous | Unchanged |

**Phase 3 (planned):**

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/finance/compensation-export` | Business + `portal.finance` | Tips + commissions CSV/JSON |

Request/response shapes: existing `packages/shared` payment/billing schemas (names unchanged).

---

## 8. Tips / commissions product rules (Phase 3)

| Topic | Rule |
|-------|------|
| **Tip** | Optional amount at payment time; split across one or more `staffMemberId` |
| **Commission** | Accrued on `Paid` (or configurable trigger) from tenant rules (% of service line) |
| **Export columns** | Period, staff id/name, type (tip/commission), gross amount, currency, booking id, payment intent id |
| **Payroll systems** | Owner downloads CSV and imports into Gusto/ADP/etc.; Adeni does not file taxes |

---

## 9. Files to touch (Phase 1)

| Layer | Files |
|-------|-------|
| Api | `FinanceController.cs`, `BillingDocumentsController.cs` routes |
| Infrastructure | `FinanceServiceCollectionExtensions.cs`, `ServiceCollectionExtensions.cs` |
| Tests | `PaymentIntegrationTests.cs`, permission tests if any |
| Frontend | `packages/api-client`, portal nav/routes/shell, `portal-permissions.ts` |
| Domain | `PortalPermissions.cs` |
| Docs | `finance.md`, `payments.md`, `architecture.md`, `roadmap-modules.md`, `sprints.md` |

---

## 10. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Unchanged — intents/documents tenant-scoped |
| **Auth** | `portal.finance` replaces `portal.payments` on ledger/links/refund/documents |
| **Webhooks** | Murphy rules unchanged; **update provider webhook URL** to `/api/v1/finance/webhook` |
| **PII** | Unchanged — no full payloads in logs |
| **Realtime** | `PaymentStatusChanged` events unchanged |

---

## 11. Tests

- [x] Integration: finance initialize, stub confirm, ledger tenant isolation, idempotency
- [ ] Phase 3: allocation + export tests

Run: `dotnet test Adeni.slnx -c Release`

---

## 12. Acceptance criteria (Phase 1)

- [x] No `/api/v1/payments/*` controllers registered
- [x] api-client uses `/api/v1/finance/*`
- [x] Portal `/finance` guarded by `portal.finance`
- [x] Spec + finance.md document boundaries and Phases 2–3

---

## 13. Open questions

| Question | Decision |
|----------|----------|
| Rename `Payments:` config to `Finance:`? | Defer Phase 4 |
| Rename C# namespaces `Payments` → `Finance`? | Defer Phase 4 |
| Subscription webhook URL | Stays `/api/v1/subscriptions/webhook` |
