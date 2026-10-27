# Spec: Sprint 37 — Realtime expansion (SignalR)

| Field | Value |
|-------|-------|
| **Status** | Implemented (portal + API); Discover conversation UI + admin hub client follow-up |
| **Authority** | [sprint-36-messaging-rewrite.md](./sprint-36-messaging-rewrite.md) (messaging push-only) |

## Goal

Extend SignalR beyond portal messaging: customer Discover threads, portal chrome badges, read receipts, dashboard previews, and tenant operational pushes (bookings, queue, quotes, admin verification, payment UI hints).

## In scope

- Messaging hub: Discover client, portal shell `ConversationUpdated` / `ReadUpdated`, dashboard widget live updates
- Operations hub `/hubs/operations`: tenant groups + admin verification group; push after SQL commit
- Portal subscribers: pending bell, queue/quotes pages, payment return hints where applicable

## Out of scope

- Redis SignalR backplane (multi-instance)
- Hub-based writes (REST only)
- Typing indicators

## Hubs

| Hub | Route | Join | Events |
|-----|-------|------|--------|
| Messaging | `/hubs/messaging` | Existing | `MessageCreated`, `ConversationUpdated`, `ReadUpdated` |
| Operations | `/hubs/operations` | `JoinTenant`, `JoinAdminVerification`, `JoinPaymentCheckout` (stub/public checkout) | `PendingBookingsChanged`, `QueueFloorChanged`, `QuotePipelineChanged`, `AdminVerificationChanged`, `AdminBadgesChanged`, `TenantVerificationChanged`, `TenantBadgesChanged`, `PaymentCompleted` |

## Client wiring (summary)

| Surface | Hub | Behavior |
|---------|-----|----------|
| Portal shell | Messaging + Operations | Badges, pending bell, bootstrap on profile |
| Portal messages | Messaging | `ConversationUpdated`, `ReadUpdated`, thread `MessageCreated` |
| Portal dashboard / quotes / queue / payments | Operations | Live counts, floor refresh, payment toast |
| Discover `/my-messages` | Messaging | `JoinConversation` + `MessageCreated` (bot/FAQ same event) |
| Discover checkout stub | Operations | `JoinPaymentCheckout(reference)` → paid UI hint |
| Admin shell + pending | Operations | `JoinAdminVerification` → badge + list refresh |

## Addendum — verification & trust badge pushes

| Event | Group | Payload | Published from |
|-------|-------|---------|----------------|
| `AdminVerificationChanged` | `admin-verification` | `{ pendingCount }` | Business submit (existing) **and** admin approve/reject (`AdminBusinessService.UpdateStatusAsync`) |
| `AdminBadgesChanged` | `admin-verification` | `{ pendingCount }` (pending trust-badge requests) | `VerificationBadgeService` request / grant / revoke / initial grants on approval |
| `TenantVerificationChanged` | `tenant-ops:{tenantId}` | `{ tenantId, status }` — `status` is `verified` \| `rejected` | Admin approve / reject |
| `TenantBadgesChanged` | `tenant-ops:{tenantId}` | `{ tenantId, badgeType \| null, status }` — `status` is `pending` \| `granted` \| `revoked`; `badgeType` null = batch grant after approval | Badge request / grant / revoke / `GrantInitialBadgesOnApprovalAsync` |

Clients: portal shell refreshes profile on `TenantVerificationChanged`; portal Profile page silently reloads profile + badge rows on either tenant event; admin shell updates the nav badge from `AdminBadgesChanged`, and the Trust badges page reloads its queue. Pending counts use `IgnoreQueryFilters()` so tenant-scoped callers (submit, badge request) still publish the global count.

## Follow-ups

- Kiosk floor board (Sprint 29) — still REST/poll unless a kiosk client is added
- Customer conversation **list** preview — no `JoinCustomerInbox`; list refreshes on navigation only
- Admin hub in dev requires JWT admin role (query `dev_auth0_sub` alone may not join)
- Integration tests for Operations hub auth/groups
