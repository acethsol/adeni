# Spec: Sprint 33b-2 — Price lock & commercial binding

| Field | Value |
|-------|-------|
| **Sprint** | 33b Phase 2 |
| **Status** | Implemented |
| **Depends on** | [sprint-33b-admin-commercial-config.md](./sprint-33b-admin-commercial-config.md), [sprint-33b-4-catalog-publish.md](./sprint-33b-4-catalog-publish.md) |

## Goal

On first paid subscription checkout, freeze **catalog version**, **agreed list price**, and **currency** on the tenant so catalog publishes do not silently reprice existing subscribers.

## Resolution order (server)

Documented in `TenantPricingResolver`:

1. **Admin override** — `CustomSaasMonthlyAmount` (+ currency / optional SKU scope)
2. **Bound agreed price** — `AgreedMonthlyAmount` while price lock active (`PriceLockEndsAt` null or future)
3. **Active catalog** — `IPricingCatalogService.GetMonthlyMajor`

Discount fields apply to the resolved **list** price only.

## Data (tenant columns)

| Column | Purpose |
|--------|---------|
| `AgreedMonthlyAmount` | List price frozen at first paid activation |
| `AgreedCurrencyCode` | ISO currency for agreed amount |
| `PricingCatalogVersionCode` | Catalog version code at bind time |
| `PriceLockEndsAt` | Optional; after this instant, agreed price is ignored for resolution |

Script: `db/scripts/tenant-commercial-binding-columns.sql`

## Bind trigger

`SubscriptionBillingOrchestrator` on activation events (`subscription.create`, `invoice.payment_succeeded`, …) when `AgreedMonthlyAmount` is still null — bind from current `TenantPricingResolution.ListPriceMajor`.

## Admin / portal

- Admin commercial GET includes agreed + bound catalog version; audit timeline from `admin.audit_logs` for `tenant.commercial_terms_updated` and `tenant.commercial_bound`.
- Portal `/plan` usage payload exposes catalog vs agreed/charged amounts.

## Tests

- `TenantPricingResolverTests` — override → agreed → catalog order
- `SubscriptionBillingOrchestratorTests` — bind on first paid webhook

## Out of scope (33b-3+)

- `TenantCommercialOverride` table migration
- `BillingProfile` billing-country immutability enforcement
