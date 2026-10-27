# F1 — Customer profile and activity foundation closeout

Implemented on `feature/squarespace-inspired-v1`, stacked on audit commit `664269b44ca00094b0613a5d50596481c5f214e4`, after approval of the [F1 plan](./f1-customer-profile-activity-foundation.md). No protected branch changed, no merge/promotion/deployment performed. F2–F5 have not started.

## Delivered

- Additive authenticated GETs: `/api/v1/tenant/customers/{id}/profile` and `/activity`. Existing customer detail/list/statement routes and contracts remain intact.
- Read-only customer overview: completed appointment/walk-in counts (deduplicating appointment queue entries), next eligible Pending/Confirmed appointment, optional recorded online payment totals by currency. No new customer aggregate or copied business records.
- Source-backed chronological timeline: booking creation/scheduled time, actual queue join/completion/no-show times, completed payment record updates and visible message metadata. Current status is separate; no invented status-transition timestamps, message bodies, attachments, provider references or tokens.
- Application orchestration via Booking-, Finance- and Messaging-owned ports. A separate Booking adapter supplies Finance with the explicitly associated booking IDs; Finance never queries Booking entities or another module's Infrastructure implementation. Tenancy resolves the roster, effective entitlements and location policy.
- Server-side full Bookings permission for new aggregate access; separate Finance and Messages permissions and effective entitlement checks before calling their source ports. Tenant mismatch fails closed. Walk-in profiles do not gain financial identity via email/phone matching.
- Recent history remains 3 bookings on Starter / 25 on paid effective plans, and 5 queue tickets, across all activity pages. Activity has a 90-day lookback, default page size 25, maximum 50, maximum 100 payment/message records per source, with explicit truncation. Upcoming appointments are displayed separately.
- Deterministic timestamp/ID ordering and Data Protection cursors bound to tenant/customer/platform linkage, location, actor and policy; one-hour cursor expiry. Re-check access on every page. Mutable source data means pagination is best-effort, not a frozen historical snapshot or durable audit log.
- Portal overview and Activity tab reuse the existing customer route, Carbon/Adeni tab conventions and formatting; English/French labels. Page-scoped state aborts obsolete requests, ignores late responses, guards duplicate pagination and cleans up on destruction. No new SignalR subscriptions, global store or package dependencies.

## Scope decisions and limits

**Practitioner aggregate access is deferred for review.** Existing customer detail accepts BookingsSelf without filtering history to staff assignment, whereas booking endpoints enforce assigned-staff scope. F1 leaves those existing routes/policies untouched and requires full Bookings permission on the new aggregate. Integration tests explicitly characterize the difference. Do not enable the practitioner aggregate until a consistent policy is reviewed; do not silently expand its visibility.

**Finance is a labelled partial view, not lifetime net spend.** Sum only Amount on linked Completed payment intents; exclude Refunded, RefundPending, failed/pending records, standalone/unlinked payments, external till settlements and separate TipAmount. The existing provider initialization sends Amount separately from stored tip metadata. Keep currencies separate, with no inferred conversion. Deposit/balance payments are actual separate records; do not sum invoice/service charges on top. UpdatedAt is a record-update timestamp, not guaranteed provider settlement time. Changes to refund state can change totals; this view cannot reconstruct historic net spend.

**Messaging in branch mode is explicitly unavailable.** Conversations lack an intrinsic reliable branch assignment, so F1 returns `unscoped` availability and does not call the source in a branch view. In tenant-wide scope, visible non-deleted/non-moderated messages contribute metadata only. No read receipts are changed. Existing source screens retain their access controls. Only existing booking deep links are added to timeline rows; Finance/Queue/Messaging have no new invented detail links.

Memberships, packages, loyalty and persistent customer preferences remain unimplemented; the response reports `notImplemented` availability instead of false zero balances. No new retention products, clinical data, gifts/wallets, payment plans, intake forms, calendar integration, document engine or WhatsApp API was introduced.

## Files and architecture

- Application: customer profile read contracts/orchestrator in Tenancy; Booking activity, Finance commerce/booking-evidence and Messaging activity ports.
- Infrastructure: six source/context/cursor adapters, including the Booking evidence adapter, registered in the existing four module registration files. EF is restricted to Infrastructure; new controller has Application dependencies only.
- API: `TenantCustomerProfileController` handles binding, actor resolution and Result-to-HTTP mapping.
- Shared/API-client: `customer-profile.ts`, schemas/tests, additive CustomersApi and compatible root facade methods, bilingual messages. Old methods retain their contracts and request behavior.
- Portal: profile data service/panel and tests; minimal integration into existing customer detail, with stale-load guards and route-parameter lifecycle handling.
- Tests: eight new test files spanning Application, Infrastructure, opt-in SQL Server, API integration, shared and Portal.
- Documentation: approved foundation status and this closeout. No Domain/entity, SQL/DACPAC schema, migration, entitlement JSON, dependency manifest/lockfile, route reorganization or budget configuration changes.

## Validation (2026-10-25)

| Exact command | Result |
|---|---|
| `dotnet build Adeni.slnx -c Release` | Passed; 0 warnings/errors, including DACPAC and all test projects. |
| `dotnet test Adeni.slnx -c Release` | Passed: Domain 26, Application 50, Infrastructure 306, API 224 = **606 passed**; **3 SQL Server tests skipped**. |
| `dotnet test tests/Adeni.Application.Tests/Adeni.Application.Tests.csproj -c Release` | Initial targeted run identified a test fixture relying on an uninitialized catalog; fixed to use explicit fake entitlements and covered by the passing full run. |
| `dotnet test tests/Adeni.Infrastructure.Tests/Adeni.Infrastructure.Tests.csproj -c Release --filter FullyQualifiedName~CustomerProfileSourcesTests` | Initial compile caught an xUnit assertion convention; corrected, then all 9 source/cursor cases passed in the full run. |
| `npm test --workspace @adeni/shared` | **77 passed**, including 4 new contract/localization cases and existing entitlement parity tests. |
| `CHROME_BIN=/tmp/adeni-chrome npm test --workspace @adeni/portal` | **25 passed**, including 10 new API-client, request lifecycle, rendering and permission cases. |
| `npm run typecheck` | Passed all workspaces: shared, API-client, UI, brand and Portal/Admin/Discover development builds including SSR. |
| `npm run build:portal` | Failed existing 24 kB component-style error budgets: services 24.50 kB, staff 27.41 kB, plan 28.33 kB. Initial 2.98 MB remains below 3 MB error threshold, above 2.5 MB warning. |
| `npm run build:admin` | Failed existing 14 component-style error budgets (same paths/sizes as audit closeout) and initial bundle **2.17 MB / 2.00 MB error limit**. Shared bilingual additions affect bundle size; no new failure category. |
| `npm run build:discover` | Failed existing shared `_data-grid.scss` **16.90 kB / 14 kB error limit**. Initial 1.42 MB; SSR build otherwise runs. |
| `git diff --check` | Passed. |

This adds **60 discovered test cases**: 45 passing backend cases, 1 opt-in SQL case and 14 passing frontend/shared cases. The three SQL skips are the two inherited discovery tests and the new profile-source test. The new test executes actual source LINQ, money grouping and branch scoping inside a rollback transaction against the published SQL database; it runs under the existing `Category=SqlServer` CI selection when `ADENI_SQLSERVER_TESTS=1` and `ConnectionStrings__AdeniDb` are configured. It was **not executed against a live SQL Server locally**; InMemory results do not prove SQL translation.

Environment: .NET 10.0.402 via `/workspace/dotnet/dotnet`, with `DOTNET_CLI_HOME=/workspace/dotnet-home NUGET_PACKAGES=/workspace/nuget TMPDIR=/workspace/dotnet-tmp`; Node 24.19.0/npm 11.9.0. `/tmp/adeni-chrome` is a local test wrapper adding Chromium `--no-sandbox`, not an application setting. Existing CI uses Node 22.22.3, so hosted validation remains required. Dependencies were already installed; no new dependency or npm-ci claim. Build budget limits/styles were not raised or refactored. See [audit closeout](../audit-hardening-closeout.md) for the full inherited stylesheet inventory. This branch is **not production-build-green**.

## Follow-up and deployment assumptions

1. Resolve practitioner/customer self-scope policy before enabling the new aggregate for practitioners. Existing policy discrepancy remains, with characterization coverage.
2. Require the SQL Server test and hosted CI on the configured Node version before merge/promotion. Existing workflow branch filters target dev/qa/main; a stacked PR into the audit branch may not trigger that workflow. Do not assume a green status exists without an actual run.
3. Data Protection key rings/application identity must be shared across API instances if activity cursors need to survive instance switching/restarts. Otherwise a rejected cursor requires refreshing activity; no data is exposed. No Redis or new key-storage dependency was added.
4. Monitor large-customer query plans. The Booking evidence port projects that customer's booking IDs to Finance; payload pages are bounded, but this identity list and lifetime aggregate work scale with source history. Optimize through explicit module-owned read contracts/indexes if measured volume warrants it, not a generic repository or duplicated ledger.
5. The read view relies on existing links/current records and does not remedy phone-merge provenance or the known post-save event failure mode. Preserve the audit's outbox design as deferred work; no outbox implementation was added.
6. Resolve inherited stylesheet/initial-bundle blockers separately, without arbitrary budget increases. F2 has not started; review F1 first.
