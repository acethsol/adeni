# Spec: Sprint 19 — Trust depth & quote workflows

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 19 — Trust depth & quote workflows |
| **Author** | Cloud Agent |
| **Status** | Complete |
| **Created** | 2026-09-07 |

---

## 1. Goal

Differentiate on trust (tiered verification badges) and service-type flexibility (full quote request → accept → book flow).

---

## 2. Tasks

| Task | Scope |
|------|--------|
| **19a** | Tiered badges: phone, CAC, address, license; admin grant; discovery/profile display |
| **19b** | Business requests badge upgrades; admin queue shows documents + pending badges |
| **19c** | Service `pricingType`: `fixed` \| `quote_request` \| `hourly`; validated vs `businessType` |
| **19d** | Quote lifecycle: submit → business quotes → customer accepts → booking |
| **19e** | Business owner public reply on reviews |
| **19f** | Discovery trust signals: badge stack, verified since, completion rate |
| **19g** | CAC verification research doc (stretch) |
| **19h** | Category-specific badge requirements (plumbers/electricians → license) |

---

## 3. API contract (summary)

| Audience | Routes |
|----------|--------|
| Public | Trust fields on discovery + business profile |
| Customer | Quote create/list/accept; review list includes owner reply |
| Business | Quote inbox + submit quote; review reply; badge upgrade request |
| Admin | Pending verifications + documents; grant/revoke badges |

---

## 4. Out of scope

- Pay-to-verify badges
- Automated CAC API integration (19g is research only)
- LLM quote drafting (Sprint 20)

---

## 5. Delivery notes

- Web + mobile parity for quotes, badges, review replies, and service pricing types
- Quote photo upload via customer-scoped media endpoint (`/api/v1/customer/media/upload-url`)
- Merge **PR #6** (Sprint 18) before **PR #7** (Sprint 19) to keep history clean
