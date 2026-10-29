# Squarespace-inspired V1 feature branch closeout

**Status: F1–F5 implemented within the explicit scopes below; not production-build-green or approved for production.** Branch `feature/squarespace-inspired-v1`, stacked on audit-hardening `664269b44ca00094b0613a5d50596481c5f214e4`. F5 builds directly on completed F4 `49d0c7b938cb968237fa02fea139f9fd18b9a1d6`. No new branch, protected-branch modification, push, merge, deployment or live-provider activation. Validated 2026-10-26.

## Feature status

| Phase | Delivered | Intentionally unavailable / limitations |
|---|---|---|
| F1 | Permission/location-scoped customer overview and bounded activity via Application source ports; protected cursors and currency-separated recorded payment metadata. | Preferences/Retention unavailable; recorded online principal is not lifetime net spend. Self-only aggregate access deferred. |
| F2 | Non-clinical reusable forms; immutable published versions; service binding; pinned booking/quote/queue assignments; authenticated, idempotent customer completion; Portal review; audit/privacy/holds/retention. | No clinical charting, attachments, signature drawing, answer reuse or reminder campaigns. Approved default retention is product policy, not a universal legal requirement. |
| F3 | Tenant-specific, single-currency ledger-backed gift cards and opaque fixed-amount Pay Links; sandbox issuance/redemption/reversal/refund; private customer pages; metadata-only profile/reporting integration. | **Development/Testing Stub only.** Live settlement/onboarding/provider activation remains blocked. No universal wallet, cross-business value, installments, lending or mixed-tender rollout. |
| F4 | Provider-abstracted Google/Microsoft busy import, optional dedicated-calendar write-back, OAuth/encrypted grants, reconciliation/leases/conflicts; stable Book-with-Adeni link/button using Discover. | Providers/write-back disabled until configured and verified; no live OAuth certification, full bidirectional editing, webhooks or iframe widget. Polling/freshness/reconciliation limits remain. |
| F5 | Six versioned, explainable visit/queue segments; current-page batching/profile sidecar; private booking receipt/invoice PDF preview through existing Finance/Reporting/QuestPDF; final-PDF access alignment. | No spend/member/package/loyalty classification, generic appointment no-show inference, list segment filtering/count summaries, saved audiences, new document catalog or theme editor. |

Related evidence: [F1](f1-customer-profile-activity-closeout.md), [F2](f2-intake-consultation-forms-closeout.md), [F3](f3-gift-cards-pay-links-closeout.md), [F4](f4-calendar-book-with-adeni-closeout.md), [approved F5 plan](f5-smart-segments-document-preview-plan.md). [Product direction](../product-direction.md) remains authoritative. This branch adds selected wellness workflows, not a website builder or generic CRM.

## F5 behavior and architecture

Tenancy Application owns classification policy/query orchestration; Booking owns set-based visit/queue facts. Identity supplies tenant-specific recognized-role membership and erasure evidence. No controller EF, cross-module Infrastructure reference, generic repository, new state library, cache, scheduler or segment projection table. Finance remains owner of money/document mapping; Reporting coordinates preview through its Application ports and the existing QuestPDF renderer.

Version `v1` defaults are non-exclusive, evaluated at one server UTC instant using business-local calendar-date boundaries:

| Label | Rule in the authorized scope |
|---|---|
| Newly served | Exactly one recorded lifetime completed visit; within 30 local dates. |
| Recently active | At least one completed visit within 30 local dates. |
| Lapsed | Latest completed visit at least 90 dates ago; no upcoming Pending/Confirmed booking. |
| At risk | At least three visits within 180 dates; latest 30–89 dates ago; no upcoming Pending/Confirmed booking. |
| VIP — frequent visitor | At least six visits within 180 dates, including one within 30 dates; no spend claim. |
| Repeated queue no-shows | At least two distinct currently NoShow queue tickets with explicit NoShowAt within 90 dates; not appointment no-shows. |

A window of N dates starts at local midnight N−1 dates before today and ends at `asOf`. Completed/Paid booking StartAt is the recorded visit date because Booking has no CompletedAt. Standalone Done queue visits use CompletedAt, with an explicit approximation flag for legacy JoinedAt fallback. Explicit linked booking/queue visits are deduplicated, including synthetic booking customers for unlinked walk-ins; party size never multiplies visits. Missing/dangling/cross-location links or missing no-show dates yield partial evidence rather than invented classifications. No F2 answers, F3 gift/payment value or F4 external event content participates.

On-demand batches accept 1–100 distinct customer IDs and one tenant/location scope. Source reads are bounded set queries (SQL test checks constant query count for 40 customers), not N+1 profile requests or copied history. Reuse F1's location resolver: omitted location may mean a concrete primary/sole branch, and tenant-wide aggregation requires existing MultiLocationReporting. No new indexes were justified by the bounded validation workload; production-scale plans/index tuning remain follow-up, not a claimed load-test result.

Existing full Bookings permission and runtime CustomerRetentionAnalytics + BasicAnalytics govern segments. Professional/Business currently qualify; runtime billing/trial resolution remains authoritative. No entitlement JSON/key/value changes. Unsupported/no match/partial/notEntitled/erased states are distinct. No automated pricing, denial, campaign or notification follows a label.

Current inferred classifications in the resolved default F1 scope are included additively in the existing MFA-protected, audited customer privacy export, with location/evaluation metadata; past and alternate-view evaluations are not stored. Erased or missing linked identities cannot regenerate classifications from retained bookings; there are no projection/cache/history copies to delete. Existing walk-in-only tenant records still lack a general platform-customer erasure workflow; this inherited gap is explicitly deferred. Logs do not contain classifications/reasons, answers or financial secrets.

Receipt/invoice preview is restricted to existing non-void, booking-linked documents. A shared Application service enforces recognized tenant membership, Finance OR full Bookings permission, existing InvoicesReceipts capability and actual Booking-owned location evidence for preview and corresponding authenticated final download. **Approved security tightening:** formerly unscoped/mismatched booking-document downloads can now be denied. Voided documents retain the existing `409 Conflict` response in both private preview and the corresponding final download, with a regression assertion. Public-token/unlinked documents, quotes and tenant-wide customer statements were not expanded into preview and retain their separately tracked legacy policies.

Preview and download share Finance snapshot mapping and the same renderer/template: historical business-name snapshot, current existing logo, fixed Adeni colors/footer, line order, typed amount/date inputs. No new address/contact/color branding contract or logo-history snapshot. Same-renderer fidelity is not a claim of immutable archival bytes after a logo change.

Private preview/direct PDF: no issuance, numbering, payment, email, public token or stored preview side effects; private/no-store, no-referrer, nosniff; server-ID filenames; existing read rate limiter. Bounded private rendering permits at most 100 lines, 2,000 characters per line, 300-character party/method labels, 5 MiB logo and 5 MiB output. Existing storage reads the logo before checking its render-size bound; this does not claim a streaming storage-memory cap or a hard cancellation timeout for synchronous QuestPDF rendering. No new rendering process/framework.

Portal uses authorized Blob fetches, PDF MIME/size validation, locally created/revoked URLs, no persistent preview storage or third-party viewer. Modal focus capture/trap/restore, Escape/Close, text metadata and Open/Download actions are outside the PDF. Pending requests abort/late responses are ignored on document/customer/page/location changes or destruction. No new SignalR subscription. API framing protections remain unchanged; Azure/Portal host CSP must permit the local Blob viewer if embedding is desired, otherwise use the secure Open/Download actions.

Customer chips use one batch per current list page, with definitions and coverage; profile uses a separate sidecar. Existing search/account filters, server counts/order/pagination and F1 activity/cursors remain intact. Optional global filtering/count summaries are deferred. The existing `/customers/:id` page now loads lazily with the same route/title/permission guard to keep F5 out of startup; no URL change. Discover behavior is unchanged; only additive shared-subpath resolution was needed.

One directly related F3 correction: an omitted location that resolves to the sole historical active location can read business-wide purchased gift metadata. Explicit branch views and businesses with any other historical/inactive branch remain restricted. No codes, balances, money transitions or live-provider behavior changed. Integration tests reproduce and protect this scope distinction.

## Database and API/UI inventory

SQL/DACPAC remains schema authority. **F5 adds no tables, columns, indexes, migrations or backfill.** Across F1–F5: 25 additive tables and two schemas (`intake`, `calendar`), with explicit tenant FKs, uniqueness/check constraints and concurrency mechanisms. F2 adds 10 Intake tables; F3 adds six commerce tables; F4 adds eight Calendar tables plus `tenancy.public_booking_links`. Existing source tables gain only the previously documented supporting keys/columns. Publish the complete feature DACPAC before the backend; EF is the ORM, not an alternative migration authority.

There are **51 additive HTTP actions** in the six new feature controller files: F1 2, F2 18, F3 14, F4 13, F5 4. Existing v1 route names are retained. F5 adds:

- `GET /api/v1/tenant/customer-segments/definitions`
- `GET /api/v1/tenant/customers/{id}/segments?locationId=...`
- `POST /api/v1/tenant/customers/segments/batch`
- `GET /api/v1/tenant/reporting/documents/{id}/preview?locationId=...`

The existing authenticated `/api/v1/finance/documents/{id}/pdf` accepts an optional location for the shared booking-document guard. Existing one-argument API-client downloads remain compatible. `AdeniApiClient.insights` is an additive lazy facade; root shared exports are preserved, with narrow schema/copy subpaths and explicit Angular path mappings. Privacy export has an additive optional `customerSegments` field. No API removals, new NuGet/npm dependencies or lockfile changes.

Portal adds five routes across F2–F4: `/services/intake`, `/bookings/:id/intake`, `/finance/quick-pay`, `/settings/calendars`, `/public-page/booking-links`. F1/F5 extend existing customer/document surfaces; F5 adds no route. Discover adds `/pay`, authenticated `/my-gift-cards`, and the two market-aware `/my-bookings/:bookingId/intake[/:assignmentId]` routes. F4 also adds server utility `/book/:slug` redirect; it is not a separate booking app or iframe. Full HTTP inventory appears below.

## Validation

Runtime: .NET 10.0.402, local Node 24.19.0/npm 11.9, Chromium 154, isolated SQL Server 2022 Developer and SqlPackage 170.5.96. Installed dependencies reused; no dependency change requiring npm ci. Hosted Node 22.22.3, hosted CI/security scans, real Azure/provider credentials and production systems were not verified. Logs are local under `/workspace/scratch/adeni-f5`; SQL credentials were supplied privately, not committed.

| Exact command | Result |
|---|---|
| `dotnet build Adeni.slnx -c Release` | Passed, zero warnings/errors; includes DACPAC. |
| `dotnet test Adeni.slnx -c Release` | **778 passed:** Domain 26, Application 86, Infrastructure 386, API 280. 27 opt-in SQL cases skipped here and run separately below. |
| `sqlpackage /Action:Publish /SourceFile:db/Adeni.Database/bin/Release/Adeni.Database.dacpac /TargetConnectionString:<isolated-test-connection> /p:BlockOnPossibleDataLoss=True` | Passed against isolated SQL Server 2022. Tool launched with `DOTNET_ROOT=/workspace/dotnet DOTNET_ROLL_FORWARD=Major`. |
| `ADENI_SQLSERVER_TESTS=1 ConnectionStrings__AdeniDb=<isolated-test-connection> dotnet test tests/Adeni.Infrastructure.Tests/Adeni.Infrastructure.Tests.csproj -c Release --no-restore --filter 'Category=SqlServer'` | **27 passed, zero skipped**, including three new aggregate/deduplication/window/batching cases and all F1–F4 SQL cases. |
| `npm run typecheck` | Passed: shared/api-client/UI/brand TypeScript and Portal/Admin/Discover development builds. |
| `npm test --workspace @adeni/shared` | **90 passed**. |
| `CHROME_BIN=/workspace/scratch/adeni-f4/chrome npm test --workspace @adeni/business` | **59 passed**. Wrapper adds container-only Chromium `--no-sandbox`. |
| `npm test --workspace @adeni/discover` | **16 passed**, including existing SSR/hydration/redirect coverage. |
| `NG_BUILD_MAX_WORKERS=2 npm run build:business` | Failed only the three existing stylesheet error categories below. Initial bundle ~**2,963.66 kB**, below 3 MB; F4 was ~2,996.25 kB. Existing 2.5 MB warning persists. |
| `npm run build:admin` | Failed existing initial and 14 stylesheet budget categories below. |
| `npm run build:discover` | Failed existing shared data-grid stylesheet error below. |
| `git diff --check`; source/controller/dependency review | Passed; no new product controller EF or forbidden Domain/Application dependency. |

No standalone Admin, api-client or UI unit-test scripts exist; their applicable typechecks/builds and public transport compatibility checks were run through the existing suites. Initial diagnostic runs found a SQL translation issue (default projected Boolean), a viewer signal dependency that restarted loads, missing explicit Karma subpath aliases and a Portal startup-budget regression. These were corrected and relevant suites/builds rerun. No budget relaxation, ignored assertion or test removal. The pre-existing intermittent QuestPDF byte-determinism test remains a follow-up; the final full suite passed without reproducing it. Preview fidelity tests compare renderer selection/inputs rather than adding another flaky byte-equality assertion.

**970 distinct automated cases passed**: 805 backend including real SQL, plus 165 shared/frontend. F5 adds **72 cases** (53 backend including three SQL; four shared; 15 Portal) across **12 new test-source files**. Compared with audit-hardening's discovered/source baseline of 656 cases (563 backend including its two opt-in SQL cases, 73 shared, 15 Portal, five existing Discover cases), this is approximately **314 net additional cases**. Existing Discover cases omitted from the old audit execution table are included in this discovery comparison; repeated runs are not added to totals.

New coverage includes threshold/time-zone/DST edges, queue-only semantics, linked walk-in identity/deduplication, bounded real SQL batching, missing evidence, cross-tenant/branch isolation, known-role permissions, Starter availability, erasure/export, receipt/invoice renderer/branding/line/money parity, pre-render denial/input/output limits, no issuance side effects, F3 sole/historical-location scope, API public imports/auth/body/response handling, current-page counts, stale request cancellation, Blob lifecycle, focus/keyboard controls and route compatibility.

## Production build blockers — exact current error categories

These remain **production blockers**, not accepted release conditions. No budgets or stylesheet sources were changed. Existing Carbon/data-grid styling and component stylesheet size require a separate measured cleanup; F5's initial Portal growth was resolved by lazy-loading the affected customer detail.

| App / stylesheet or bundle | Actual | Error budget |
|---|---:|---:|
| Portal `services.component.scss` | 24.50 kB | 24 kB |
| Portal `staff.component.scss` | 27.41 kB | 24 kB |
| Portal `plan.component.scss` | 28.33 kB | 24 kB |
| Admin initial bundle | ~2,187.37 kB | 2 MB |
| Admin `customers.component.scss` | 25.75 kB | 24 kB |
| Admin `audit-logs.component.scss` | 25.39 kB | 24 kB |
| Admin `pending.component.scss` | 25.25 kB | 24 kB |
| Admin `businesses.component.scss` | 25.25 kB | 24 kB |
| Admin `markets.component.scss` | 25.38 kB | 24 kB |
| Admin `reviews.component.scss` | 25.61 kB | 24 kB |
| Admin `pricing-entitlements.component.scss` | 25.39 kB | 24 kB |
| Admin `pricing-catalog.component.scss` | 26.73 kB | 24 kB |
| Admin `transactions.component.scss` | 25.45 kB | 24 kB |
| Admin `business-billing.component.scss` | 32.74 kB | 24 kB |
| Admin `business-subscription-wizard.component.scss` | 27.61 kB | 24 kB |
| Admin `business-commercial-audit.component.scss` | 32.83 kB | 24 kB |
| Admin `trust-badges.component.scss` | 25.25 kB | 24 kB |
| Admin `business-overview.component.scss` | 37.71 kB | 24 kB |
| Discover shared `_data-grid.scss` | 16.90 kB | 14 kB |

## Final architecture review, risks and deferred work

Review against the hardening baseline found no new Domain/Application→Infrastructure dependency, cross-module Infrastructure reference, generic repository, new product-controller DbContext query, disabled tenant middleware, removed permission/entitlement gate, changed entitlement catalog, removed public route, unexpected external dependency or enabled live provider. HealthController's pre-existing optional DbContext connectivity probe is the diagnostic exception. The explicit F5 booking-PDF access tightening and F3 scope correction are documented above rather than called behavior-identical.

| Classification | Item / required next work |
|---|---|
| Production blocker | Resolve measured Angular CSS/Admin-initial budgets and run hosted Node 22 CI, dependency/security gates, branch protections and artifact promotion checks. |
| Provider activation blocker | F3 live merchant settlement/onboarding, provider confirmation/idempotency/refund behavior and market-specific compliance remain unfinished. Stub behavior is not live readiness. |
| Provider activation blocker | Real Google/Microsoft consent/scopes/account audience, refresh/revoke/read/write/retry verification, provider quotas and dedicated write-back calendar verification. |
| Deployment blocker | Azure TLS/trusted forwarding/callback cookies/CORS, external access/query-log redaction, Key Vault/provider secrets/versioned calendar keys and recovery/rotation, actual hosted SQL publish/backup/recovery, Hangfire health/queues/reconciliation. |
| Accepted for this branch only | Post-save in-process events/enqueue can fail after database commit or be lost at process crash; polling has a change window. Existing [outbox design](../transactional-outbox-design.md) remains deferred. No reliability guarantee or outbox implementation added. |
| Deferred inherited security/privacy debt | Existing unlinked/public-token billing and tenant-wide statement policies, global legacy-role normalization and walk-in-only customer erasure need focused review. New previews/segments do not bypass them or claim to solve all inherited access issues. |
| Deferred scale/history work | Production-size segment query-plan/index/load tests, snapshot consistency across multiple source reads, immutable branding history, fully streaming logo/output limits and isolated hard-timeout rendering if required by measured load. Current labels describe recorded mutable state, not a historical scoring ledger. |
| Deferred scheduling/integration debt | Existing overlapping scheduling/capacity races outside F4's snapshot guard, high-volume reconciliation bounds, Microsoft ambiguous-create operator recovery, key rotation tooling and richer integration monitoring. |
| Legal/privacy review | Market-specific intake retention/holds, inference explainability/deletion, gift-card policy, financial record minimization and existing public document sharing. No legal-compliance certification is implied. |
| Test reliability follow-up | Investigate the unchanged intermittent QuestPDF deterministic-byte assertion without weakening fidelity checks. |

No Retention module, memberships/packages/loyalty/referrals, campaigns/CRM DSL/automation, installment/BNPL/lending, wallet/cross-business value, live Stripe/Paystack enablement, calendar webhook expansion, customer calendar integration, iframe booking widget, CMS/site builder/white-label theming, generic report designer, AI scoring, transactional outbox implementation or WhatsApp API was added.

## Recommended review and deployment sequence

1. Review/land audit-hardening independently; review this stacked branch against that baseline, with F1–F5 commits and documented partial features. Do not treat local tests as hosted release approval.
2. Address build/security/CI blockers; use PR promotion `dev → qa → main`, required checks/reviews and protected production branches. No direct production commits or branch promotion performed here.
3. Publish/verify the additive DACPAC in staging with backup/rollback procedures; deploy the backend with live F3 blocked and Calendar disabled, then shared/client and Angular artifacts. Verify private document scope and Portal Blob-viewer CSP/accessibility.
4. Run real Auth0 multi-role/location/erasure smoke tests, hosted SQL and Hangfire operations, monitoring/log-redaction and backup/key recovery checks. Review product/privacy policies and existing public billing sharing.
5. Treat live commerce and real calendar activation as separately reviewed release work after provider end-to-end/settlement tests. Prioritize the DB-backed outbox and scheduling correctness before stronger delivery/concurrency guarantees; add segment indexing/projections only from measured scale evidence.

## Final comparison and review commits

Comparison authority: `664269b44ca00094b0613a5d50596481c5f214e4` (audit-hardening), not dev. Exact final file/test-file counts and commit identifiers are recorded below after staging; schema and action counts above refer to source objects/actions, not diff line counts. The final review commit is the branch HEAD; its SHA is returned in the task result to avoid a self-referential commit hash in this document.

- **298 total files changed** against audit-hardening, including **53 test-source files** (test-support files are excluded from that test-source count).
- **65 files changed in F5**, including its approved plan/closeout and 12 new test-source files; 72 new cases.
- Review commits: `cd32d67` backend/SQL/security tests; `361cb23` shared/client/Portal/lifecycle tests; `c1ffaf0` plan/closeout; the final review commit preserves voided-PDF conflict responses and updates this closeout.
- No dependency/lockfile or entitlement-catalog drift; 25 tables/two schemas and 51 HTTP actions added across F1–F5; F5 adds no schema objects or Angular route paths.
- Protected local refs remain dev `0cfecfc75417f72257ec89478aa0168c4a594960` and audit-hardening `664269b44ca00094b0613a5d50596481c5f214e4`. qa/main are absent locally and untouched. No merge/push/promotion performed.

## HTTP action inventory added against hardening

Paths retain source route constraints. OAuth/public-token access is intentionally distinct from Portal/customer-account authorization, as documented in the phase closeouts. No existing endpoint is removed.

**TenantCustomerProfileController.cs**

- `GET /api/v1/tenant/customers/{id:guid}/profile`
- `GET /api/v1/tenant/customers/{id:guid}/activity`

**IntakeControllers.cs**

- `GET /api/v1/tenant/intake/templates`
- `POST /api/v1/tenant/intake/templates`
- `GET /api/v1/tenant/intake/templates/{id:guid}`
- `PUT /api/v1/tenant/intake/templates/{id:guid}/draft`
- `POST /api/v1/tenant/intake/templates/{id:guid}/draft`
- `POST /api/v1/tenant/intake/templates/{id:guid}/publish`
- `PATCH /api/v1/tenant/intake/templates/{id:guid}`
- `GET /api/v1/tenant/intake/services/{id:guid}`
- `PUT /api/v1/tenant/intake/services/{id:guid}`
- `GET /api/v1/tenant/intake/bookings/{bookingId:guid}`
- `POST /api/v1/tenant/intake/bookings/statuses`
- `GET /api/v1/tenant/intake/bookings/{bookingId:guid}/forms/{assignmentId:guid}/submission`
- `GET /api/v1/bookings/{bookingId:guid}/intake`
- `GET /api/v1/bookings/{bookingId:guid}/intake/{assignmentId:guid}`
- `POST /api/v1/bookings/{bookingId:guid}/intake/{assignmentId:guid}/submission`
- `GET /api/v1/admin/customers/{id:guid}/intake-export`
- `POST /api/v1/admin/intake/tenants/{tenantId:guid}/submissions/{id:guid}/hold`
- `POST /api/v1/admin/intake/tenants/{tenantId:guid}/submissions/{id:guid}/hold/release`

**CommerceControllers.cs**

- `GET /api/v1/tenant/finance/pay-links`
- `POST /api/v1/tenant/finance/pay-links`
- `POST /api/v1/tenant/finance/pay-links/{id:guid}/cancel`
- `POST /api/v1/tenant/finance/pay-links/{id:guid}/refund`
- `POST /api/v1/tenant/finance/pay-links/{id:guid}/reverse-gift-redemption`
- `GET /api/v1/tenant/finance/gift-cards`
- `GET /api/v1/tenant/finance/gift-cards/{id:guid}/transactions`
- `GET /api/v1/public/pay-links/resolve`
- `POST /api/v1/public/pay-links/checkout`
- `POST /api/v1/public/pay-links/redeem`
- `GET /api/v1/me/gift-cards`
- `POST /api/v1/me/gift-cards/{id:guid}/reveal`
- `GET /api/v1/admin/customers/{id:guid}/commerce-export`
- `GET /api/v1/tenant/customers/{id:guid}/purchased-gift-cards`

**CalendarControllers.cs**

- `GET /api/v1/tenant/calendar/connections`
- `POST /api/v1/tenant/calendar/connections`
- `GET /api/v1/tenant/calendar/connections/{id:guid}/calendars`
- `PUT /api/v1/tenant/calendar/connections/{id:guid}/selection`
- `POST /api/v1/tenant/calendar/connections/{id:guid}/disconnect`
- `POST /api/v1/tenant/calendar/connections/{id:guid}/sync`
- `GET /api/v1/tenant/calendar/conflicts`
- `POST /api/v1/tenant/calendar/conflicts/{id:guid}/acknowledge`
- `GET /api/v1/calendar/oauth/launch`
- `GET /api/v1/calendar/oauth/{provider}/callback`
- `POST /api/v1/tenant/booking-links`
- `GET /api/v1/public/booking-links/{slug}`
- `GET /api/v1/public/booking-context/{slug}`

**CustomerSegmentsController.cs**

- `GET /api/v1/tenant/customer-segments/definitions`
- `GET /api/v1/tenant/customers/{id:guid}/segments`
- `POST /api/v1/tenant/customers/segments/batch`

**DocumentPreviewController.cs**

- `GET /api/v1/tenant/reporting/documents/{id:guid}/preview`

## F5 file inventory

- `apps/admin/tsconfig.json`
- `apps/discover/tsconfig.json`
- `apps/business/src/app/app.routes.ts`
- `apps/business/src/app/core/customer-insights-api.spec.ts`
- `apps/business/src/app/core/customer-route-compatibility.spec.ts`
- `apps/business/src/app/pages/customers/customer-profile-panel.component.html`
- `apps/business/src/app/pages/customers/customer-profile-panel.component.ts`
- `apps/business/src/app/pages/customers/customer-segment-chips.component.spec.ts`
- `apps/business/src/app/pages/customers/customer-segment-chips.component.ts`
- `apps/business/src/app/pages/customers/customer-segments-data.service.spec.ts`
- `apps/business/src/app/pages/customers/customer-segments-data.service.ts`
- `apps/business/src/app/pages/customers/customer-segments-panel.component.ts`
- `apps/business/src/app/pages/customers/customers.component.html`
- `apps/business/src/app/pages/customers/customers.component.spec.ts`
- `apps/business/src/app/pages/customers/customers.component.ts`
- `apps/business/src/app/pages/payments/document-preview/document-preview-data.service.spec.ts`
- `apps/business/src/app/pages/payments/document-preview/document-preview-data.service.ts`
- `apps/business/src/app/pages/payments/document-preview/document-preview.component.spec.ts`
- `apps/business/src/app/pages/payments/document-preview/document-preview.component.ts`
- `apps/business/src/app/pages/payments/payments.component.html`
- `apps/business/src/app/pages/payments/payments.component.ts`
- `apps/business/tsconfig.json`
- `docs/specs/f5-smart-segments-document-preview-plan.md`
- `docs/specs/squarespace-inspired-v1-closeout.md`
- `packages/api-client/src/finance/finance-api.ts`
- `packages/api-client/src/index.ts`
- `packages/api-client/src/insights/insights-api.ts`
- `packages/api-client/src/insights/insights-operations.ts`
- `packages/shared/package.json`
- `packages/shared/src/customer-insights-copy.ts`
- `packages/shared/src/customer-segments.test.ts`
- `packages/shared/src/customer-segments.ts`
- `packages/shared/src/i18n/messages/catalog-en.ts`
- `packages/shared/src/i18n/messages/catalog-fr.ts`
- `packages/shared/src/index.ts`
- `packages/shared/src/schemas.ts`
- `src/Adeni.Api/Controllers/BillingDocumentsController.cs`
- `src/Adeni.Api/Controllers/CustomerSegmentsController.cs`
- `src/Adeni.Api/Controllers/DocumentPreviewController.cs`
- `src/Adeni.Application/Admin/IAdminCustomerService.cs`
- `src/Adeni.Application/Auth/IPortalTenantAccessQuery.cs`
- `src/Adeni.Application/Booking/ICustomerSegmentFactsQuery.cs`
- `src/Adeni.Application/Reporting/DocumentPreviewService.cs`
- `src/Adeni.Application/Reporting/IDocumentPreviewService.cs`
- `src/Adeni.Application/Tenancy/CustomerGiftCardsQueryService.cs`
- `src/Adeni.Application/Tenancy/CustomerSegmentPolicy.cs`
- `src/Adeni.Application/Tenancy/CustomerSegmentsQueryService.cs`
- `src/Adeni.Application/Tenancy/ICustomerSegmentsQueryService.cs`
- `src/Adeni.Application/Tenancy/ITenantLocationScopeService.cs`
- `src/Adeni.Infrastructure/Admin/AdminCustomerService.cs`
- `src/Adeni.Infrastructure/Booking/BookingServiceCollectionExtensions.cs`
- `src/Adeni.Infrastructure/Booking/CustomerSegmentFactsQuery.cs`
- `src/Adeni.Infrastructure/Booking/DocumentBookingScopeQuery.cs`
- `src/Adeni.Infrastructure/Finance/BillingDocumentService.cs`
- `src/Adeni.Infrastructure/Finance/FinanceServiceCollectionExtensions.cs`
- `src/Adeni.Infrastructure/Identity/IdentityServiceCollectionExtensions.cs`
- `src/Adeni.Infrastructure/Identity/PortalTenantAccessQuery.cs`
- `src/Adeni.Infrastructure/Reporting/ReportingServiceCollectionExtensions.cs`
- `src/Adeni.Infrastructure/Tenancy/CustomerSegmentContextQuery.cs`
- `src/Adeni.Infrastructure/Tenancy/TenancyServiceCollectionExtensions.cs`
- `src/Adeni.Infrastructure/Tenancy/TenantLocationScopeService.cs`
- `tests/Adeni.Api.Tests/Integration/CustomerSegmentsIntegrationTests.cs`
- `tests/Adeni.Api.Tests/Integration/DocumentPreviewIntegrationTests.cs`
- `tests/Adeni.Application.Tests/Tenancy/CustomerSegmentPolicyTests.cs`
- `tests/Adeni.Infrastructure.Tests/Tenancy/CustomerSegmentsSqlServerTests.cs`
