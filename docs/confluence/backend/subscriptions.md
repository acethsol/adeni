# Backend module — Subscriptions & Entitlements

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**SaaS tiers** for tenants: checkout, recurring charges (Paystack patterns), **entitlements** gating portal features, admin **MRR/revenue metrics**, and per-tenant commercial terms.

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Subscriptions/` |
| Application | `Adeni.Application/Subscriptions/` — `ISubscriptionService`, billing orchestrator, entitlements |
| Infrastructure | `Adeni.Infrastructure/Subscriptions/` |
| API | `SubscriptionController` — tenant subscription, billing history, webhooks |

Registration: `AddSubscriptionsModule(configuration)`.

---

## Key concepts

| Concept | Description |
|---------|-------------|
| `SubscriptionTier` | Product SKU mapping (Starter, Growth, …) |
| `TenantBillingStatus` | Active, past_due, cancelled, … |
| Entitlements | Feature flags checked in portal/API (`IEntitlementsService`) |
| Commercial terms | Admin overrides per tenant |

---

## HTTP routes

| Method | Route |
|--------|-------|
| GET | `/api/v1/tenant/subscription` |
| POST | `/api/v1/tenant/subscription/checkout` |
| GET | `/api/v1/tenant/billing/history` |
| POST | `/api/v1/subscriptions/webhook` |
| GET | `/api/v1/admin/metrics/subscription`, `/admin/metrics/revenue` |

**Note:** Subscription webhook signature verification is a known hardening item — mirror payment webhook rules before production.

---

## Clients

Portal **Plan** page; Admin **Insights**, **Business billing** wizard, **Transactions**.

---

## Related docs

- `docs/subscription-billing.md`, `docs/pricing-and-monetization.md`
- [Admin](./admin.md)
