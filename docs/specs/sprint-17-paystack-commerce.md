# Spec: Sprint 17 — Commerce orchestration (Paystack)

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 17 — Commerce orchestration (Paystack) |
| **Author** | Cloud Agent (standing play 1) |
| **Status** | Implemented (P1–P4); see [docs/payments.md](../payments.md) |
| **Created** | 2026-08-23 |

---

## 1. Goal (one sentence)

Replace stub payment orchestration with real **Paystack (NG) integration** so businesses can collect booking deposits and standalone payment links via WhatsApp — without Adeni holding funds.

---

## 2. Context

- **Sprint doc:** [docs/sprints.md](../sprints.md) § Sprint 17 (17a–17g)
- **Strategy:** [product-strategy.md §4.3](../product-strategy.md#43-fintech--orchestration-not-custody) — orchestration only, no wallet/custody; 0% platform fee during Lagos pilot
- **Architecture:** [architecture.md](../architecture.md) — Payments module; `IPaymentProvider` port exists
- **Existing stub:** `StubPaymentProvider`, `PaymentIntentRecord`, `POST /api/v1/payments/initialize`, `packages/shared` schemas, booking UI placeholder (“checkout coming soon”)
- **Related (stretch in 17):** [subscription-billing.md](../subscription-billing.md) — Paystack Subscriptions can reuse webhook infra; **not required for 17 MVP**

**North-star for 17 MVP:** A customer can pay a booking deposit OR a business can share a “Pay ₦X” link on WhatsApp; money moves via Paystack; Adeni records intent + status only.

---

## 3. In scope

### 17a — Harden Payments module (build on stub)

- [ ] Extend `IPaymentProvider` if needed (webhook handler port, refund method)
- [ ] Add `IPaymentOrchestrator` (optional) for booking-deposit amount calculation + booking status side effects — keeps Paystack adapter thin
- [ ] Extend `PaymentIntentRecord` for Paystack fields (see §5)
- [ ] Environment-based provider registration: `StubPaymentProvider` (dev/test) vs `PaystackPaymentProvider` (staging/prod)
- [ ] Idempotency key on initialize (client-supplied or server-generated, stored on intent)

### 17b — Paystack integration (NG)

- [ ] `PaystackPaymentProvider` — Initialize Transaction API → persist intent → return `authorization_url` as `checkoutUrl`
- [ ] `POST /api/v1/payments/webhook` — verify `x-paystack-signature` (HMAC SHA512), idempotent event processing
- [ ] Handle events: `charge.success`, `charge.failed` (map to `Completed` / `Failed`)
- [ ] Secrets: `Paystack:SecretKey`, `Paystack:PublicKey`, webhook secret via Key Vault / env — never in repo
- [ ] Amount in kobo (Paystack) vs NGN decimal in domain — convert at adapter boundary only

### 17c — Payment links & invoices

- [ ] `POST /api/v1/tenant/payments/links` — business creates ad-hoc payment link (amount, description, optional expiry)
- [ ] Public resolve: `GET /api/v1/payments/links/{code}` → initialize or redirect to Paystack checkout
- [ ] Portal UI: create link, copy URL, WhatsApp share (extend share-kit pattern from Sprint 16)
- [ ] Receipt: email/logging stub OK; full receipt email → Notifications module (optional 17 stretch)

### 17d — Booking deposit flow

- [ ] Tenant setting: `depositPercent` (0–100) on booking settings — only when `deposits` capability enabled
- [ ] On booking confirm (customer): if deposit required → `initializePayment` with computed amount + `bookingId`
- [ ] Webhook success → mark payment intent completed + update booking payment state (e.g. `depositPaidAt` or booking sub-status)
- [ ] Wire [booking-panel.tsx](../../apps/web/components/booking-panel.tsx) — replace “coming soon” with Paystack redirect / embedded flow
- [ ] Mobile: same flow via `api-client.initializePayment`

### 17e — Transaction ledger (business portal)

- [ ] `GET /api/v1/tenant/payments` — paginated list of payment intents for tenant (amount, status, booking link, createdAt)
- [ ] Portal page: payment history table + filter by status
- [ ] No reconciliation with Paystack dashboard in v1 — export CSV stretch

### 17f — Platform fee config

- [ ] Market-level config: `platformFeePercent` default **0** for Lagos pilot ([markets.md](../markets.md))
- [ ] Pass Paystack `transaction_charge` or split logic only if Paystack subaccount model chosen — **default 17 MVP: 0% fee, no split**
- [ ] Store fee amount on intent for future reporting (nullable columns)

### 17g — Refund orchestration

- [ ] `POST /api/v1/tenant/payments/{id}/refund` — business/admin initiates; calls Paystack Refund API
- [ ] Update intent status → `Cancelled` or new `Refunded` enum value
- [ ] Audit log admin/business refund actions
- [ ] Partial refunds → backlog unless Paystack flow requires in 17

---

## 4. Out of scope

- Wallet / balance / escrow tables — **never**
- Holding or settling funds inside Adeni
- Stripe or multi-provider (Paystack NG only in 17)
- Paystack Subscriptions for SaaS tiers (can share webhook endpoint in 17b; full tier upgrade flow optional stretch)
- In-app messaging / WhatsApp Business API (Sprint 18)
- Full quote-workflow payments (Sprint 19)
- Production deploy + App Insights alerts (Sprint 20) — staging Paystack test keys OK
- Automatic payout to business subaccounts (Paystack Split) — evaluate post-pilot unless required for go-live
- PCI/card storage — Paystack hosted checkout only

---

## 5. API contract

### Existing (extend behavior, keep shapes stable where possible)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/v1/payments/initialize` | Customer JWT (or anonymous for link flow — see open question) | Create intent; return Paystack `checkoutUrl` |
| GET | `/api/v1/payments/{id}` | Customer or business tenant member | Poll status post-checkout |

**Request** (unchanged — [packages/shared](../../packages/shared/src/schemas.ts)):

```json
{
  "tenantId": "uuid",
  "bookingId": "uuid",
  "amount": 5000.00,
  "currency": "NGN"
}
```

**Response** (unchanged shape; `checkoutUrl` becomes real Paystack URL):

```json
{
  "id": "uuid",
  "tenantId": "uuid",
  "bookingId": "uuid",
  "amount": 5000.00,
  "currency": "NGN",
  "status": "pending",
  "checkoutUrl": "https://checkout.paystack.com/...",
  "providerReference": "T_abc123"
}
```

### New endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/v1/payments/webhook` | Paystack signature (no JWT) | Webhook ingress |
| POST | `/api/v1/tenant/payments/links` | Business user + `X-Tenant-Id` | Create shareable payment link |
| GET | `/api/v1/payments/links/{code}` | Anonymous | Resolve link → checkout |
| GET | `/api/v1/tenant/payments` | Business user + `X-Tenant-Id` | Ledger list |
| POST | `/api/v1/tenant/payments/{id}/refund` | Business owner or admin | Initiate refund |

### Schema additions (`PaymentIntentRecord`)

| Column | Type | Notes |
|--------|------|-------|
| `intent_type` | enum | `booking_deposit`, `payment_link`, `manual` |
| `description` | string? | For links |
| `link_code` | string? | Unique slug for `/payments/links/{code}` |
| `platform_fee_amount` | decimal? | Nullable; 0 in pilot |
| `idempotency_key` | string? | Unique index |
| `metadata_json` | jsonb? | Paystack metadata echo |
| `completed_at` | timestamptz? | Set on webhook success |

Add `Refunded = 4` to `PaymentIntentStatus` if needed.

### Shared types

Update [packages/shared/src/schemas.ts](../../packages/shared/src/schemas.ts):

- `paymentLinkCreateRequestSchema`, `paymentLinkResponseSchema`
- `tenantPaymentListItemSchema`
- Extend `paymentIntentResponseSchema` only if new fields required by clients

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| **Domain** | `Adeni.Domain/Payments/*` — status enum, link type, optional domain events (`PaymentCompleted`) |
| **Application** | `IPaymentProvider.cs`, new `IPaymentLinkService`, `ITenantPaymentQuery`, webhook DTOs, `PaystackOptions` |
| **Infrastructure** | `PaystackPaymentProvider.cs`, `PaystackWebhookHandler.cs`, HTTP client, signature validator; extend `PaymentsServiceCollectionExtensions`; migration for new columns |
| **Api** | `PaymentsController.cs` — auth attributes; `PaymentsWebhookController.cs`; `TenantPaymentsController.cs` |
| **Tests** | Unit: amount conversion, signature verification, idempotency; Integration: webhook → status update; tenant ledger isolation |
| **Web** | `booking-panel.tsx`, new `business-payment-links.tsx`, portal nav entry, ledger page |
| **Mobile** | Booking payment step (if parity required in 17) |
| **packages/shared + api-client** | New schemas + client methods |
| **Docs** | `docs/payments-paystack.md` runbook; update `sprints.md` task statuses |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All intents tenant-scoped (`ITenantEntity`); ledger and refund routes enforce JWT tenant + header match; webhook resolves intent by `providerReference` only |
| **Auth / roles** | Initialize for booking: customer JWT; tenant links/refunds: business role; webhook: signature only, rate-limited, no JWT |
| **Cache** | Invalidate tenant profile/overview if payment stats cached later; no cache on webhook path |
| **Audit** | Refunds, manual admin payment adjustments → `admin.audit_logs` |
| **PII** | Do not log full Paystack payloads; mask customer email in logs; webhook processing logs intent id + event type only |
| **Observability** | Log webhook processing with `correlationId`; metric counters for success/fail (App Insights wiring Sprint 20) |
| **Compliance** | Non-custodial posture documented; Paystack as licensed provider; NDPR: payment metadata minimal |

---

## 8. Tests

- [ ] Unit: kobo conversion round-trip; invalid signature rejected; duplicate webhook idempotent
- [ ] Unit: deposit percent calculation from service price
- [ ] Integration: initialize → stub/simulated webhook → GET intent `completed`
- [ ] Integration: business A cannot GET business B payment from ledger
- [ ] Integration: refund endpoint updates status (mock Paystack HTTP)
- [ ] Architecture: Payments Infrastructure does not reference Booking Infrastructure directly — use Application ports / events

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [ ] Staging uses Paystack **test** keys; dev keeps stub unless `Paystack:Enabled=true`
- [ ] Customer completes deposit on test card → booking shows deposit paid
- [ ] Business creates payment link → opens on mobile browser → Paystack checkout → intent completed
- [ ] Webhook rejects tampered signature (401/403)
- [ ] Business portal lists transactions for own tenant only
- [ ] No wallet/balance tables added
- [ ] Platform fee remains 0% for Lagos in config
- [ ] `docs/payments-paystack.md` documents env vars, webhook URL, local tunnel (ngrok) for dev

---

## 10. Implementation phases (recommended order)

| Phase | Tasks | Outcome |
|-------|-------|---------|
| **P1** | 17a + 17b | Paystack initialize + webhook + provider swap |
| **P2** | 17d | Booking deposit end-to-end (web first) |
| **P3** | 17c | Payment links + WhatsApp share |
| **P4** | 17e + 17g | Ledger + refunds |
| **P5** | 17f | Fee config (0% default, schema only) |

Each phase: spec slice → implement (play 2) → security audit (play 4) → tests (play 5) → PR to `dev`.

---

## 11. Open questions

| Question | Recommendation |
|----------|----------------|
| Anonymous initialize for payment links? | Yes for `GET /payments/links/{code}` → server-side initialize with link’s tenant/amount; do not expose open initialize without auth |
| Paystack subaccounts for per-business settlement? | Defer until post-pilot; 17 MVP: business receives via their own Paystack connection later OR manual settlement — document limitation |
| Booking state if deposit fails? | Keep booking `pending` until deposit paid or timeout (24h?) — add `paymentExpiresAt` stretch |
| Mobile parity in 17? | Web + API in P1–P3; mobile deposit UI in P2 if capacity |
| Subscription billing in 17? | Share webhook controller; implement `charge.success` routing by metadata `type: subscription` in stretch |

---

## 12. Configuration (staging)

```json
{
  "Paystack": {
    "Enabled": true,
    "SecretKey": "<from Key Vault>",
    "PublicKey": "<from Key Vault>",
    "WebhookSecret": "<from Key Vault>",
    "BaseUrl": "https://api.paystack.co"
  }
}
```

Webhook URL (staging): `https://api-staging.adeni.io/api/v1/payments/webhook`

Local dev: Paystack disabled → `StubPaymentProvider`; or ngrok + test keys for manual E2E.

---

## 14. Webhook security (Murphy)

**Source:** [@mattmurphyai — Instagram reel DcbzcfyioD9](https://www.instagram.com/reel/DcbzcfyioD9/) — “You accept webhooks from Stripe without verifying the signature…”

Same rules apply to Paystack and any future provider. This is a **non-negotiable** for Adeni trust/commerce.

### The three Murphy fixes

1. **Signature verification on every webhook** — verify before parsing JSON or updating state  
2. **Idempotency** — replayed events must not double-confirm bookings or ledger entries  
3. **Endpoint protection** — rate limiting on public webhook URLs; secrets in Key Vault only  

### Adeni compliance matrix

| Requirement | Payments (`/api/v1/payments/webhook`) | Subscriptions (`/api/v1/subscriptions/webhook`) |
|-------------|--------------------------------------|-----------------------------------------------|
| Signature before parse | ✅ `PaystackPaymentProvider` — `x-paystack-signature`, HMAC SHA512, constant-time compare | ❌ Stub only — **must fix when wiring Paystack Subscriptions** |
| Idempotent success handler | ✅ `PaymentOrchestrator.ApplySuccessAsync` — no-op if `Completed` | ❌ Not implemented |
| Resolve intent by provider ref | ✅ Lookup by `providerReference` | N/A |
| Fulfillment only after verify | ✅ `PaymentCompleted` domain event → booking confirm | N/A |
| Webhook secret required (staging/prod) | ✅ `Paystack:WebhookSecret` enforced when configured | ❌ |
| Rate limiting | ⚠️ Planned (Sprint 20 / infra) | ⚠️ Same |
| No full payload in logs | ✅ Audit + structured logs | ⚠️ Stub logs byte length only |

### Implementation references

- `src/Adeni.Infrastructure/Payments/PaystackPaymentProvider.cs` — `ParseWebhookAsync`
- `src/Adeni.Infrastructure/Payments/PaymentOrchestrator.cs` — `ProcessWebhookAsync`, `ApplySuccessAsync`
- `tests/Adeni.Infrastructure.Tests/Payments/PaystackPaymentProviderTests.cs` — `ParseWebhookAsync_rejects_missing_signature_when_secret_configured`
- Agent playbook: [AGENTS.md](../../AGENTS.md#webhook-security-murphy)
- Runbook: [docs/payments.md](../payments.md#webhook-security-murphy)

### Remaining work

- [ ] Rate-limit `POST /api/v1/payments/webhook` (and subscription webhook when live)
- [ ] Harden `POST /api/v1/subscriptions/webhook` with Paystack signature + idempotency before SaaS billing goes live
- [ ] Integration test: tampered signature → 400/401; duplicate `charge.success` → single booking confirm

### Security audit checklist (play 4)

When reviewing any PR touching webhooks:

- [ ] Signature verified on **raw body** (not re-serialized JSON)
- [ ] Handler idempotent for terminal payment states
- [ ] No code path marks booking paid from initialize/redirect alone (webhook or verified provider API only)
- [ ] Stub confirm route disabled outside Development
- [ ] `WebhookSecret` documented in Key Vault / env, never committed

---

## 15. Standing play next step

Sprint 17 core is shipped. For follow-up hardening:

```
Standing play 4: Security audit payment webhooks against AGENTS.md Webhook security (Murphy).
Close subscription webhook gap and add rate limiting plan.
```
