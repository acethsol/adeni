# Spec: Dev-completion mobile parity — Phase 2

| Field | Value |
|-------|-------|
| **Sprint** | Dev-completion backlog (pre–Sprint 18) |
| **Author** | Cloud Agent |
| **Status** | Implemented |
| **Created** | 2026-09-07 |

---

## 1. Goal (one sentence)

Complete remaining mobile parity gaps: plan/subscription UI, share kit, business and public reviews, and customer waitlist when no slots.

---

## 2. Context

Phase 1 delivered mobile Payments + booking settings. Web still had Plan, Share kit, Reviews on profile, and waitlist in booking flow.

---

## 3. In scope

- [x] Mobile `/business/plan` — usage meter + plan comparison (stub upgrade)
- [x] Share kit on business profile — link, WhatsApp, Instagram bio templates
- [x] Business reviews panel on profile (tenant view)
- [x] Public reviews on customer business profile (`/business/[slug]`)
- [x] Join waitlist when no slots (mobile `BookingPanel`)
- [x] Plan tab in business portal navigation

## 4. Out of scope

- QR code (web-only via `react-qr-code`; mobile uses share sheet)
- Customer Auth0 sign-up acceptance
- Discovery GET rate limits (backend)
- Paid Paystack subscription checkout

---

## 5. API contract

Existing endpoints only — `getTenantSubscriptionUsage`, `getBusinessReviews`, `joinWaitlist`.

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Frontend | `apps/mobile/components/adeni/*`, `apps/mobile/app/business/plan.tsx`, `profile.tsx`, `[slug].tsx`, `BookingPanel.tsx`, `BusinessTabs.tsx`, `index.tsx` |

---

## 7. Acceptance criteria

- [x] Business owner sees plan usage and tier comparison on mobile
- [x] Share kit copies/shares public booking URL
- [x] Reviews visible in business portal and on public profile
- [x] Signed-in customer can join waitlist when slots empty
