# F1 — Customer profile and activity timeline foundation

Status: **Plan approved; F1 foundation implemented, awaiting implementation review.**

Review outcome: the user approved this plan. See [F1 closeout](./f1-customer-profile-activity-closeout.md) for delivered scope, validation and the deliberately deferred practitioner aggregate slice. The proposal below records the reviewed design; later phases have not started.

Base: `chore/audit-hardening-recommendations` at `664269b44ca00094b0613a5d50596481c5f214e4`.
Stacked branch: `feature/squarespace-inspired-v1`. Product authority: [product direction](../product-direction.md); boundaries: [architecture](../architecture.md).

## Goal and scope

Give business staff a useful, tenant-scoped customer overview and chronological activity view by composing existing module data. Preserve the current roster, booking/queue tabs, statement download, permissions, effective entitlements, and location rules. This is a read-only foundation, not a CRM rewrite.

The branch roadmap remains F1 profiles/timeline → F2 non-clinical intake → F3 tenant gift cards and non-custodial Pay Links → F4 calendar adapters and Book-with-Adeni → F5 constrained segments/document preview/closeout. Only F1 is proposed here. No installments, wallet, WhatsApp API, generic website builder, clinical records, calendar SDKs, or new retention products in F1.

## Existing capabilities audited

| Area | Evidence and reuse |
|---|---|
| Customer identity | `Domain/Identity/Customer` is the global Auth0 consumer; `Domain/Tenancy/TenantCustomer` is the business roster anchor. It has optional platform linkage, contact data, creation/update/last-seen timestamps. Keep these separate. |
| Roster and detail | `ITenantCustomerService` / `Infrastructure/Tenancy/TenantCustomerService`, `TenantCustomersController`: list/search/sort/page, Discover/walk-in filter, profile with recent bookings and queue visits. Existing phone masking stays. |
| Identity association | Booking upsert matches platform ID then tenant phone; walk-in upsert matches tenant phone then linked platform ID. Filtered unique indexes exist for tenant/customer and tenant/phone. Reuse stored linkage; never use email or a cross-tenant phone search to associate financial activity. |
| Booking | `BookingRecord`, lines, staff, location, timestamps, status, settlement source and `PaidAt`; `IBookingService`. Bookings link platform `CustomerId`, not `TenantCustomerId`. Notes are booking-specific, not persistent customer preferences. |
| Visits | `QueueTicket` links `TenantCustomerId`, optional booking/platform customer; has join/call/start/complete/no-show timestamps. Appointment-created queue tickets can overlap booking visits. |
| Finance | `PaymentIntentRecord`, `BillingDocument`, existing provider/orchestrator/access services. Payments link optional booking, not roster customer; documents retain customer snapshots. Amount/currency and settlement data can support a carefully labelled profile metric, not an inferred wallet. |
| Reporting | `ITenantReportExportQueryService.GetCustomerStatementAsync`, `Infrastructure/Dashboard/TenantReportExportQueryService`, QuestPDF renderer/branding and existing Portal download. Statement includes booking charges and payment entries: summing it would double-count spend. Leave rendering and statement semantics unchanged. |
| Messaging | `IConversationService`, conversation/message records, tenant membership checks, moderation/deletion fields, SignalR and wa.me links. New timeline access must not bypass Messages permission, expose hidden messages, or mark messages read. |
| Notifications | Delivery records contain recipient keys and optional tenant ID, but no reliable typed customer/booking correlation. Not a safe customer timeline source today. |
| Retention | `sprint-37-retention-module.md` is Draft; no implemented membership/package/loyalty entities/services found. SaaS Subscriptions is tenant billing, not customer membership. Reporting keys/specs do not establish shipped retention products. |
| Portal/shared | Existing `/customers/:id` detail has overview/bookings/queue, branch refresh and statement action. `packages/api-client/src/customers/customers-api.ts` and root facade expose typed calls; shared schemas/i18n already exist. |
| Audit baseline | Audit closeout records parity/security/boundary work and existing production-build blockers. Preserve all of it; this plan changes no implementation. |

## Gaps and semantic constraints

- No composed profile summary or unified timeline port; current detail queries Booking directly inside Tenancy Infrastructure. New composition must use Application contracts, not expand that pattern.
- `LastBookingAt`/`LastSeenAt` are updated when roster upserts occur. They are not proof of attendance or the last completed service.
- Booking has current status and mutable `UpdatedAt`, but no complete transition history or cancellation/completion timestamps. Never fabricate past status-transition events from current status or `UpdatedAt`.
- Queue has richer timestamps; appointments and their queue tickets must not count as two visits. Count completed/paid bookings once, plus Done walk-ins without BookingId. These are operational visits, not clinical encounters.
- No structured persistent customer preferences, retention balances, or trustworthy identity link for standalone walk-in payments. Do not show empty balances as zero or invent preference writes in F1.
- No complete refund/external-till transaction ledger suitable for a definitive lifetime net-spend figure. `RefundPending`, external settlement and currency separation require explicit treatment.
- No booking NoShow status exists; only queue NoShow. Frequent-no-show segments belong to F5 and must respect that limitation.

## Proposed implementation

### Domain and database

Reuse existing entities unchanged. No new profile aggregate, duplicated customer facts, timeline table, event store, balance table, or EF migration. F1 timeline is a source-backed read view, not a durable audit trail.

No schema change is required for the bounded read-only scope. If SQL query plans justify an index, propose additive SQL Database Project indexes separately (for example tenant/customer/start or tenant/customer/queue timestamp), verify against real SQL Server and publish via SqlPackage. Do not introduce EF migrations: `docs/database-setup.md` is authoritative over the stale generic migrations row in AGENTS.md.

Persistent preferences, true transition history, and standalone commerce attribution would require later owner-module schema designs and review. They are not prerequisites for this foundation.

### Application and Infrastructure

Add a Tenancy-owned `ICustomerProfileQueryService` and implementation which resolves the roster identity, effective entitlement policy, caller access, and location scope before querying any sources. It composes small Application ports, implemented within their owning modules:

- Booking `ICustomerBookingActivityQuery`: completed visit summary, next appointment, bounded booking/queue activity. Requires explicit tenant ID, resolved identity/location and allowed history window. Returns read DTOs, never EF entities.
- Finance `ICustomerCommerceQuery`: linked completed online payment totals grouped by currency and bounded payment activity. F1 label: **Recorded online payments**, not lifetime/net spend. Include Completed only; exclude Refunded/RefundPending/failed/pending, no implicit FX conversion, no double-counting booking prices, invoices, tips or deposits. Document that totals reflect qualifying current records and may change with refund status; external till and unlinked payments are not covered. Final tip inclusion policy must match existing orchestrator Amount semantics during implementation tests.
- Messaging `ICustomerCommunicationActivityQuery`: bounded metadata for visible customer messages, only with existing Messages access and Messaging entitlement. Do not return bodies, attachment URLs, recipient keys, tokens or provider references. Link to the existing conversation view, which retains its own authorization. Reuse membership checks without invoking read-receipt mutations.

Names are proposals; reuse an existing suitable port when implementing. New services depend only on Application contracts/Domain, never another module's Infrastructure class. Keep controllers thin, Result-based, and registrations explicit in the composition root. Do not broadly refactor legacy customer service internals.

Resolve actor permissions on the server using existing membership/permission infrastructure, never a client-supplied allowed-source list. Finance requires Finance permission even when customer-page access is already granted; messaging requires Messages permission. Omit restricted fields/sources entirely with explicit availability metadata, not zero values. Do not broaden customer route access to Accountant or other roles as part of F1. Preserve existing Bookings/BookingsSelf route semantics; characterize self-scoping before coding and do not let a new aggregate expand its visibility. An unresolved self-scope discrepancy blocks that slice for review rather than silently changing existing policy.

### Proposed additive API

Leave existing list/detail/statement response shapes and methods intact.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/tenant/customers/{id}/profile` | Roster identity reference, source availability, visits/next appointment and permitted currency totals; optional validated locationId. |
| GET | `/api/v1/tenant/customers/{id}/activity` | Bounded reverse-chronological activity; optional validated locationId, bounded pageSize and opaque cursor. |

Both require business JWT, existing tenant middleware/header agreement and current customer permissions. Cross-tenant IDs return existing not-found/denial conventions. Use ApiResults/error codes and shared localized errors. No anonymous profile endpoint or mutations.

Profile DTO proposal: `tenantCustomerId`, `asOf`, `scope`, `availability`, permitted `visitSummary`, `nextAppointment`, `recordedOnlinePaymentsByCurrency`. Retention/preferences availability is `notImplemented`, not a fictitious product balance. Absence of platform linkage is distinguished from access denial/no records.

Activity DTO proposal: stable `id` (source/type/entity), `kind`, `occurredAt`, `source`, `sourceId`, optional `locationId`, allowlisted display data, `items/nextCursor/asOf/isTruncated`. Server returns typed facts; UI localizes descriptions. No arbitrary HTML/free-form server labels.

Initial kinds: booking created (CreatedAt, current status labelled as current), appointment scheduled (StartAt, past only in activity; future appears as next appointment), queue joined/completed/no-show (actual timestamps), recorded successful online payment (UpdatedAt explicitly documented as record-update time, not guaranteed provider settlement time), visible message sent (CreatedAt). No synthetic cancellation/completion history. Queue/booking related entries may both appear with distinct labels, while visit metrics deduplicate them.

Order deterministically by timestamp DESC then source/type/stable ID. Cursor encodes version, snapshot upper bound, last tuple and source position; validate tenant/customer/location/policy binding and recompute permission/entitlement every request. Mutable sources mean this is best-effort pagination, not transactionally frozen history; changes in permission/plan invalidate pagination. Bound per-source queries, push filtering into SQL, support cancellation. Avoid a generic plugin/provider framework or N+1 queries.

### Entitlements: existing model before any new gates

The shared JSON has Starter, Professional, Business; 25 catalog fields. Runtime .NET has 30 including derived/compatibility fields; parity tests were added by the audit. `IEntitlementsService` and TenantBillingPolicy effective SKU remain authoritative (including trial/canceled/suspended behavior).

| Existing behavior | Starter | Professional | Business |
|---|---|---|---|
| Recent customer booking history | 3 | 25 | 25 |
| Recent queue visits | 5 | 5 | 5 |
| customerRetentionAnalytics; source filter | false | true | true |
| messaging/basicAnalytics/basicExports | true | true | true |
| multiLocationReporting | false | false | true |
| Location/practitioner maxima | 1 / 3 | 1 / 10 | 3 / 25 |

InvoicesReceipts/OnlinePayments/Deposits are currently universal runtime compatibility flags. MultiLocation derives from location allowance, separately from purchased MultiLocationReporting. Retention product flags discussed in the draft spec are not in the current catalog.

Recommendation: no new SKU keys or arbitrary plan gates for F1. Preserve 3/25 booking and 5 queue history caps across the entire pageable view, so pagination cannot bypass them. Upcoming appointment is operational, not deeper historical analytics. Use existing BasicAnalytics for simple visit/payment summaries, existing Messaging for communication source and existing multi-location policy for scope. Do not add retention cohorts/segments. Proposed analytics exposure is subject to review here; do not silently map it to CustomerRetentionAnalytics. New finance/message sources need finite technical lookback/result bounds, not invented paid-plan history privileges; propose 90-day activity lookback, pageSize default25/max50, per-source hard cap100 with explicit truncation. No UI gate substitutes for backend enforcement.

### Angular and shared contracts

Extend existing Portal customer detail with compact summary/next-appointment cards and an Activity tab; retain booking/queue tabs, route/query behavior, branch selector and statement action. Show availability/coverage labels; omit unavailable retention/preferences cards rather than cluttering the page with future features.

Keep presentation in the component, use a small page-scoped facade only if independent request orchestration warrants it. Signals/computed for view state; switchMap/takeUntilDestroyed or equivalent lifecycle-safe cancellation for customer/branch changes and pagination. Ignore stale responses. No new SignalR subscriptions in F1: refresh on page/branch/manual refresh using existing patterns. No global store.

Add Zod DTO schemas/types/localized strings to shared, CustomersApi methods with compatible root facade exports, and thin authorized-client calls. Reuse Carbon/Adeni components and currency/timezone formatting for Lagos/Ottawa. No shared business logic in UI. No Admin, Flutter or Discover changes; Discover SSR/SEO/hydration untouched.

## Security, reliability and performance risks

- Roster permissions are broader than Finance permission; do not leak finance facts in cards or timeline. Re-check message moderation/deletion and tenant membership in the source query.
- Apply validated location filters consistently to summary, next appointment and every source; exclude unscopable cross-location conversations in branch mode unless an existing authorized context safely establishes scope. No tenant-wide fallback to fill a sparse branch view.
- Existing auto phone linking and partially linked walk-ins can misattribute identity. Reuse explicit associations, characterize collisions, do not add merge/write behavior in this read-only feature.
- Empty, denied, unsupported and truncated results have different meanings. Never label incomplete payment data lifetime spend, or treat an unavailable package balance as zero.
- Restrict PII to existing contact conventions; no raw bodies/notes in the unified view or logs. No shared cache initially; no new cache invalidation or outbox dependency.
- Parallel EF queries on the same scoped DbContext are unsafe. Compose sequentially with bounded queries initially; optimize only with evidence and appropriate context ownership.
- The read view cannot reconstruct lost domain events; the audit's post-save reliability/outbox design remains deferred. F1 must not claim durable activity history.
- Existing production blockers remain: Portal/Admin/Discover component-style budgets and Admin initial bundle. No budget increases or broad styling changes in this feature.

## Test and validation plan

1. Application tests with fake source ports: composition, deterministic merge/ties/cursors, history caps across pages, policy changes, availability, no restricted-port invocation, cancellation and known source failures. Surface errors explicitly rather than silently pretending a source is empty.
2. Owner-module Infrastructure tests: linked/unlinked customers, identical phone across tenants, visit dedupe, excluded canceled/rejected/incomplete bookings, next eligible Pending/Confirmed appointment (pending labelled awaiting approval), queue timestamps/no-show, currency separation, payment status/tip semantics and no charge/payment double count. Messaging hidden/deleted/body exclusion and read-state invariance.
3. API integration tests: owner/manager/receptionist/practitioner/Accountant, BookingsSelf characterization, Finance/Messages denial, missing JWT, forged tenant/header, cross-tenant customer/source IDs, invalid location/cursor/size, Starter vs paid history caps, trial/effective SKU and Business location aggregation. Existing detail/statement contracts unchanged.
4. SQL Server tests for actual LINQ translation, tenant/location predicates, ordering and pagination. InMemory tests alone do not establish production query correctness.
5. Shared/API-client tests: Zod shapes, nullable/omitted/restricted fields, same root imports, exact new request paths/query encoding/auth headers, unchanged old customer methods. Existing entitlement parity tests must remain green.
6. Portal Jasmine tests: loading/error/empty/availability/truncation, source links permission-safe, tab/back navigation, branch/customer stale-request cancellation, currency/timezone display, unsubscribe on destruction and no additional realtime subscription. Existing booking/queue/statement actions unchanged.
7. Implementation validation commands: `dotnet build Adeni.slnx -c Release`; `dotnet test Adeni.slnx -c Release`; targeted tests in Application/Infrastructure/API plus configured SQL Server CI job; `npm test --workspace @adeni/shared`; `npm test --workspace @adeni/portal`; `npm run typecheck`; `npm run build:portal`; `npm run build:admin`; `npm run build:discover`. Use existing workspace typechecks; npm ci only if dependencies need installation. Report inherited production failures precisely, never waive by raising budgets.

## Recommended review and delivery sequence

1. Review this scope, metric definitions, source permissions/self-scope, 90-day bounds and plan-cap preservation. Resolve ambiguity before coding the affected slice.
2. Add contract DTOs/ports and authorization/entitlement composition tests; keep existing endpoints unchanged.
3. Implement Booking/Queue source and profile/next appointment with SQL-backed tests, then minimal API and Portal overview/activity slice.
4. Add permission-isolated Finance recorded-payment source and Messaging metadata source with regression tests. Keep each commit reviewable.
5. Run full checks, document coverage limitations and remaining schema/index needs; stop for F1 review before F2.

Acceptance: no copied business state or schema required; correct tenant/location/permission/plan boundaries; deterministic bounded source-backed timeline; honest coverage labels; no changes to existing contracts, payment state transitions, booking writes, SSR or entitlement catalog values.

This planning pass ran repository inspection and branch/status checks only. No application tests/builds were rerun because no executable code changed. The audit's recorded validation is historical baseline evidence, not a new validation result.
