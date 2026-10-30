# Spec: Sprint 37.5 — Retention analytics

> **Parent:** [sprint-37-retention-module.md](./sprint-37-retention-module.md) §37.5, §6.4, §6.6.

| Field | Value |
|-------|-------|
| **Sprint** | 37.5 — Retention analytics |
| **Status** | **Implemented** (dev/local) |
| **Depends on** | 37.1–37.4, Reporting 39B export plumbing |

## Goal

Own retention metric calculations in **`IRetentionAnalyticsService`**; gate portal and exports with **`advancedRetentionAnalytics`** (Pro/Business). Reporting must not query `retention.*` tables directly.

## Delivered

- Seed key `advancedRetentionAnalytics`: false / true / true (Starter / Pro / Business).
- **`IRetentionAnalyticsService`**: `GetMembershipMrrAsync` (wraps `IMembershipMrrService` + entitlement), `GetSummaryAsync` (package ledger utilization + loyalty points in range + current MRR snapshot).
- **`GET /api/v1/tenant/retention/analytics/summary`**, **`GET …/memberships/mrr`** gated via analytics service.
- Report **`retention-membership-mrr`** in registry (CSV, `RequiresAdvancedRetentionAnalytics`).
- Portal: membership MRR panel on **Memberships** page when entitled; Starter sees upgrade hint.

## Out of scope (follow-up)

- Referral/tier/reward conversion widgets, churn rate, PDF export for MRR.
- Dashboard layout widgets (can reuse summary API later).
