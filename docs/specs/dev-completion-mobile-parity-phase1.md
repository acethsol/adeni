# Spec: Dev-completion mobile parity — Phase 1

| Field | Value |
|-------|-------|
| **Sprint** | Dev-completion backlog (pre–Sprint 18) |
| **Author** | Cloud Agent |
| **Status** | Implemented |
| **Created** | 2026-09-07 |

---

## 1. Goal (one sentence)

Give mobile business users the same **Payments** portal and **booking settings** (auto-confirm, deposit %) that web already has, using existing API contracts.

---

## 2. Context

- Sprint 17 + hardening PRs delivered payments on web (`/business/payments`, booking settings on profile).
- Mobile `BusinessTabs` stopped at Profile — no Payments tab or deposit/auto-confirm controls.
- [product-strategy.md](../product-strategy.md) calls for mobile portal parity with web.

---

## 3. In scope

- [x] Mobile `/business/payments` — create payment link, ledger, refund, WhatsApp share
- [x] Mobile booking settings on profile — auto-confirm + deposit % (when `deposits` capability)
- [x] Payments tab in `BusinessTabs` (gated by `deposits` capability when profile loaded)
- [x] Overview quick link to Payments

## 4. Out of scope

- Plan page, share kit, waitlist, public reviews (Phase 2)
- Backend API changes
- Staging / live Paystack E2E
- Lawyer-approved legal copy

---

## 5. API contract

Existing — no changes.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/v1/payments/links` | Business JWT + tenant | Create payment link |
| GET | `/api/v1/payments/ledger?tenantId=` | Business JWT + tenant | List ledger |
| POST | `/api/v1/payments/{id}/refund` | Business JWT + tenant | Refund completed payment |
| PATCH | `/api/v1/tenant/settings` | Business JWT + tenant | `autoConfirmBookings`, `depositPercent` |

Client: `@adeni/api-client` — `createPaymentLink`, `listPaymentLedger`, `refundPayment`, `updateTenantSettings`.

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Api | — (none) |
| Frontend | `apps/mobile/components/adeni/BusinessPaymentsPanel.tsx`, `BusinessBookingSettings.tsx`, `BusinessTabs.tsx`, `app/business/payments.tsx`, `app/business/profile.tsx`, `app/business/index.tsx` |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | All calls via `createBusinessApiClient()` with tenant header |
| **Auth / roles** | Business portal only; same as existing mobile business screens |
| **PII** | No new logging; provider references shown in UI only |

---

## 8. Tests

- [x] Mobile TypeScript (`npm run typecheck` in `apps/mobile`)
- Backend integration tests unchanged (already cover payment + settings APIs)

---

## 9. Acceptance criteria

- [x] Business with `deposits` capability can create payment links on mobile
- [x] Ledger loads and completed payments can be refunded (with confirmation)
- [x] WhatsApp share opens with pre-filled message
- [x] Auto-confirm and deposit % save via API and persist
- [x] Payments tab hidden when tenant lacks `deposits` capability

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| BFF vs direct API on mobile? | Direct `@adeni/api-client` (existing mobile pattern) |
| Copy link on mobile? | Use `Share.share()` (no new clipboard dependency) |
