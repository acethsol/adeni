# F5 — Smart customer segments, document preview and branch closeout

| Field | Value |
|---|---|
| Status | Approved by user; implemented — see [closeout](squarespace-inspired-v1-closeout.md) |
| Created | 2026-10-26 |
| Branch | `feature/squarespace-inspired-v1`; no new branch |
| Implementation base | Completed F4 `49d0c7b938cb968237fa02fea139f9fd18b9a1d6` |
| Comparison baseline | Audit-hardening `664269b44ca00094b0613a5d50596481c5f214e4` |

Goal: explainable classifications from existing recorded visits, plus safe previews of existing billing PDFs, without building Retention, a CRM or another rendering engine. Authorities: [product direction](../product-direction.md), [architecture](../architecture.md), [F4 closeout](f4-calendar-book-with-adeni-closeout.md). At the planning checkpoint this document was the only repository change; implementation and validation after approval are recorded in the closeout.

## 1. Supported segment definitions

Recommend six fixed, non-exclusive classifications. These are **proposed product defaults for review**, not existing behavior or industry/legal requirements. `D` means the number of business-local calendar dates between a recorded visit date and today. A 30-date window means `0 <= D < 30`, not an unspecified 30×24-hour interval. Windows end at the request's server `asOf` instant; future visits never count as completed.

| ID / label | Exact proposed V1 definition | Example explanation |
|---|---|---|
| `newCustomer` / Newly served | Exactly one recorded completed visit in the resolved scope, with that first visit `D < 30`. | “One recorded completed visit, 8 days ago.” |
| `recentlyActive` / Recently active | At least one completed visit with `D < 30`. | “Last recorded completed visit was 12 days ago.” |
| `lapsed` / Lapsed | At least one recorded completed visit; latest visit `D >= 90`; no upcoming Pending/Confirmed booking in this scope. | “Last recorded visit was 104 days ago; no upcoming booking.” |
| `atRisk` / At risk | At least three completed visits with `D < 180`; latest visit `30 <= D < 90`; no upcoming Pending/Confirmed booking in this scope. | “Three visits in 180 dates; last visit 45 days ago; no upcoming booking.” |
| `vip` / VIP — frequent visitor | At least six completed visits with `D < 180`, including a visit with `D < 30`. **Frequency-based, not a spend claim.** | “Six completed visits in 180 dates; last visit 14 days ago.” |
| `frequentQueueNoShow` / Repeated queue no-shows | At least two distinct queue tickets currently marked NoShow, with recorded `NoShowAt` in the last 90 local dates. | “Two recorded queue no-shows in 90 dates; appointment no-shows are not tracked.” |

No automated denial, pricing change, notifications or campaign action follows a label. Active/new/VIP can overlap; at-risk and lapsed cannot overlap under these definitions. An upcoming booking suppresses at-risk/lapsed only in the same authorized scope, without consulting hidden other-branch bookings.

Existing evidence:

- F1 already exposes Booking-owned Completed/Paid counts, standalone Done queue visits, next appointment, scoped activity and source availability through Application ports. Its activity is deliberately truncated (3/25 booking history and other source caps), so it is **not** a segmentation dataset.
- Booking has no CompletedAt or NoShow status. Use the recorded appointment `StartAt` as its visit date, not UpdatedAt (which can change at payment). Queue has CompletedAt and NoShowAt. For legacy unlinked Done tickets without CompletedAt, use JoinedAt only as an explicitly flagged recorded-date approximation; do not claim an actual completion timestamp. Missing NoShowAt is not invented from cancellation/UpdatedAt.
- Queue can create a linked Booking for a walk-in without linking `TenantCustomer.CustomerId`. The source query must resolve those explicit queue→booking links. Deduplicate by booking ID; standalone Done queue tickets count once; party size does not create extra customer visits. Ambiguous/missing or cross-location linked records produce partial/unknown evidence, not a fabricated zero.
- `TenantCustomer.LastSeenAt` changes when bookings/queue entries are created; it is not proof of a completed visit. Calendar busy blocks, conflicts and sync failures are not customer-behavior evidence.

## 2. Unsupported/deferred segments

- **Active Member, Package Holder, loyalty-based VIP:** unavailable; Retention/membership/package/loyalty models are not implemented. F1 correctly reports Retention as notImplemented. Do not add false/zero placeholders or implement these modules.
- **High Spender and monetary VIP:** defer. F1 `CustomerCommerceQuery` describes recorded completed booking-linked online payment principal, not net lifetime spend. External tills/refunds, attribution and mixed tender must not be silently combined or inferred. F3 gift purchases/redemptions are not additional service spend.
- **Generic appointment “Frequent No-show”:** unavailable. Queue no-shows are supported under the explicitly queue-specific label only. Cancelled/Rejected/missed-looking appointments do not establish no-show facts.
- No AI score, manually edited segment labels, saved audience, referrals, campaign list, rules DSL or generic automation. No new gift-card document type.

Unsupported definitions stay out of the matched-label list; the definitions/availability metadata explains deferral where useful. They are never represented as a supported segment that simply has no matches.

## 3. Threshold recommendations

Use platform V1 defaults and a backend definition version, initially `v1`; no tenant configuration screens or database settings. Thirty dates distinguish recent activity, 90 represents an explicit lapsed threshold, and 180 gives a six-month frequency window. Three visits establish repeat behavior; six represent roughly monthly activity over that window; two queue no-shows identify repeated recorded events. These are intentionally simple hypotheses for product review and later measurement, not an asserted universal beauty/wellness cadence.

The backend owns matching rules. Return definition/reason codes and typed parameters for English/French presentation; do not copy evaluation logic into Angular or introduce a second entitlement/threshold authority. Future threshold changes require a definition-version change and reviewed migration of expectations, not silently reinterpreted historical labels. No historical classification ledger is proposed.

## 4. Currency and time-window handling

V1 implemented labels do not use money, so no currency aggregation, FX conversion or spending ranking is needed. A future spending label must use Finance-owned, bounded, currency-specific net evidence and expose incomplete coverage. Never sum CAD and NGN or treat gift-card balance as an Adeni wallet.

Capture one `TimeProvider` UTC `asOf` per request/batch. Resolve the same location/primary-business scheduling zone as existing Booking policy, validate it, then convert local midnight cutoffs to UTC. Explicit branch scope uses that branch's zone; an authorized tenant-wide view uses the primary business location's zone and returns the chosen zone. An unavailable/invalid zone produces an unavailable classification state rather than a silent host-local/UTC fallback.

Window cutoffs are local midnight at today minus 29/89/179 dates, through `asOf`, with exact edge tests. Future appointments use their real UTC instants. Ottawa DST and Lagos non-DST remain distinct; 23/25-hour dates are not treated as always 24 hours. Results describe current recorded state at evaluation, not a temporal reconstruction of mutable booking status years ago.

## 5. Computation/storage model

Tenancy's existing customer-profile Application area owns a small `ICustomerSegmentsQueryService` and pure versioned policy. Booking owns a new narrow batch facts port implemented in Booking Infrastructure; it aggregates only its bookings/queue data. Tenancy supplies authorized tenant-customer/platform-customer identities and resolved scope. No controllers query EF and no cross-module Infrastructure dependency or generic repository is introduced.

Compute on demand, without persistent segment rows, labels, a global store, event-sourced projection or a new background job. Use set-based source queries for up to **100 distinct authorized customer IDs**; return first/latest visit, required window counts, explicit upcoming-booking evidence and queue no-show counts/coverage. Do not issue one profile query per customer or materialize an entire tenant's history in Angular. Do not use F1's paginated/truncated activity to calculate labels.

Proposed additive APIs:

- `GET /api/v1/tenant/customer-segments/definitions`: authorized fixed definitions, version, source limitations and runtime availability.
- `GET /api/v1/tenant/customers/{id}/segments?locationId=...`: sidecar evaluation for F1.
- `POST /api/v1/tenant/customers/segments/batch`: at most 100 IDs and optional location; server validates each identity/scope. No arbitrary expression/filter syntax.

Response includes `asOf`, definition version, effective location/zone, source-coverage state, supported matched labels and explanation parameters. Distinguish no matching labels from notEntitled/restricted/partial/notAvailable. Missing or inconsistent history must not become a confident negative recency classification.

No new tables or backfill. After measuring real SQL plans, likely narrow covering-index candidates are bookings `(TenantId, CustomerId, Status, StartAt)` and queue `(TenantId, TenantCustomerId, Status, JoinedAt)` with necessary timestamp/link/location includes. Add only indexes justified by the new queries, with EF/DACPAC parity and publish validation. Existing tenant/status/date indexes do not specifically start with customer identity. Any persisted projection would need a separately reviewed design if measured scale requires it.

## 6. Location/tenant scope

Reuse `ITenantLocationScopeService.ResolveListLocationAsync` and F1's authorization semantics. An omitted location can resolve to a concrete primary/sole branch; null does **not** automatically mean business-wide. Tenant-wide aggregation across locations requires the existing MultiLocationReporting rule. Every Booking/Queue query constrains tenant and resolved location; unlocated history is excluded from branch evaluations.

Label/definition UI must name its scope: “New to this location” is not “New to the business.” A booking in another branch never changes a branch label or explanation. Resolve linked queue/booking identity and branch evidence without expanding beyond the selected branch. Multi-source rows with contradictory location evidence are not silently assigned to a branch.

## 7. Permissions

Segments require the same full `portal.bookings` access as F1 profile aggregates. BookingsSelf-only practitioners remain excluded; accountant-only Finance access does not grant customer segmentation. Tenant membership, customer existence and location access are server checks. No new broad permission key or frontend-only decision.

Reuse/extract the small F1 scope/access resolver if needed to avoid separate contradictory rules, while preserving its existing cursor binding and history behavior verbatim. Unknown/legacy role normalization must not grant new sensitive authority: validate a recognized persisted role/membership in the relevant access port. Do not roll out a global role migration in F5.

Document preview uses existing billing PDF permission semantics: Finance **or** full Bookings, verified for the actual tenant, plus document and proven location scope. BookingsSelf alone is insufficient. There are pre-existing normalization/scoping gaps requiring characterization and explicit review in sections 11/19; they are not silently treated as a safe new preview policy.

## 8. Entitlement recommendation

Use existing **CustomerRetentionAnalytics plus BasicAnalytics** for segment evidence/labels. JSON currently enables both on Professional/Business; Starter has BasicAnalytics but not CustomerRetentionAnalytics. Existing discover/walk-in customer account filtering already checks CustomerRetentionAnalytics. This is the existing commercial boundary, not a new arbitrary key or Retention-module implementation.

Evaluate runtime effective entitlements through `IEntitlementsService`/`TenantBillingPolicy`, including trials/comp/billing states. Frontend reads runtime availability, not a static marketing tier check. Basic F1 summaries, existing account filters and history limits stay as they are.

Preview introduces no separate purchase gate. Billing receipt/invoice capability is currently the derived, universal InvoicesReceipts flag. Existing report exports retain BasicExports/AdvancedReports/ExcelExport rules; do not incorrectly impose AdvancedReports on ordinary billing previews. Multi-location policy remains authoritative. All recommendations remain subject to this plan's review; no entitlement values or catalog keys changed in planning.

## 9. F1 integration

Fetch segments as a separate sidecar for the customer profile and in one bounded batch for the **current customer-list page**. Add concise chips and an expandable explanation/definition with evaluation date, scope and coverage. No F1 activity items, cursor schema, pagination or timeline sorting changes. No F2 answer contents, intake-completion scoring, F3 secrets or F4 external-calendar data enter labels.

**Defer segment filtering, tenant-wide count summaries and saved audiences in V1.** They are optional in the request. Filtering only a fetched page would produce wrong totals; filtering globally safely would require a separate source-owned, server-paged selection design. Keep current list search, discover/walk-in account filter, ordering, counts and pagination unchanged. Do not pretend a page count is a tenant segment count. This keeps the first delivery small and avoids an unnecessary projection/store.

## 10. Privacy and explainability

Treat classifications as protected inferred customer data. No server/Redis/localStorage segment cache, no automatic notifications or third-party analytics payloads containing labels, and no intake/communication/calendar contents in reasons or logs. Operational logs/audit include identifiers, definition version, status/results only; inferred no-show/recency details are not ordinary telemetry.

Integrate an additive, existing-admin-MFA/audited privacy export section or modular `/api/v1/admin/customers/{id}/segments-export` endpoint. Export current available classifications and definitions/evaluation time for linked tenant records, explicitly noting that past evaluations are not retained. Do not generate a fictional historical classification history or Starter classifications that were never available.

No projection rows exist to purge. Nonetheless, new evaluation must check Identity-owned erasure state so retained bookings cannot immediately regenerate customer-linked classifications after privacy deletion. No hidden historical copies or cached reasons. Walk-in-only tenant records without a platform CustomerId need an explicit existing tenant-record privacy process; the current platform-customer erasure entry point does not cover them automatically. Report that gap rather than inventing a competing deletion system. F2 holds/retention and F3 financial integrity remain unchanged.

## 11. Document preview scope

Recommend V1 preview of existing **booking-linked receipts and invoices** from the Finance Documents surface, including existing Draft/Issued semantics but excluding Void. Preview never issues a document, consumes a document number, initializes payment, emails, persists a preview or creates a public token. Display Draft status clearly in the UI rather than pretending preview is legal issuance.

Defer standalone/unlinked invoices, F3 custom-charge/gift-purchase receipt previews, quotes, customer statements and gift certificates. Receipt/invoice rendering already exists; no new document catalog is needed. Quotes and statements also have templates, but widening scope is not necessary for a useful first preview. In particular, current customer-statement export is tenant-wide and has no location argument; it must not be reused as branch-safe preview.

Billing documents do not store an explicit location. A linked booking can supply authoritative location evidence through a Booking Application port. Do not guess a historical document's branch from today's primary/only active branch. Unsupported/unscoped documents show a clear unavailable reason; preserve existing unrelated document actions while separately reporting their inherited access limitations.

The new preview and corresponding authenticated **booking-linked final-PDF** path must share one application access guard. This is a proposed narrow security alignment: trusted tenant membership, recognized role, current capability, linked booking location and existing scope resolution, with no reliance on user-supplied tenant headers alone. It can deny old unscoped/mismatched requests that previously passed: that tightening is explicitly presented for review, not claimed to preserve those unsafe access outcomes. If alignment requires broader contract/policy changes than this, stop and report before coding it. Do not ship a weaker preview endpoint as a workaround.

## 12. Branding inputs

Reuse `IReportPdfBrandingLoader`, existing tenant name/profile LogoImageKey, bounded existing file-storage access, QuestPDF brand colors and Adeni footer. Billing name comes from `BusinessNameSnapshot`; retain that historical choice rather than replacing it with today's tenant name. Existing logo loading is live and can fall back to a text header when absent/unreadable.

Address/contact and public-page accent settings exist elsewhere, but the current billing render contract does not provide a complete branch-address/contact/custom-color branding snapshot. V1 preview will not invent those fields, a theme editor or a second business-branding authority. Preserve the current final layout and explicitly document name/logo-only customization and fixed Adeni colors. Future immutable logo/address snapshots are a separate document-history decision.

## 13. Renderer/preview architecture

Reporting owns a narrow `IDocumentPreviewService`; Finance supplies authorized document metadata and the existing PDF generation path; Booking supplies location evidence; Identity/Tenancy supply access/scope. Delegate to the same `IBillingDocumentService.GetPdfAsync` → `IReportRenderer` → BillingDocumentQuestPdfTemplate path as final PDF after applying the shared guard. Keep Finance's snapshot/data mapping in Finance. Do not move money/document business rules into Reporting or Angular.

Proposed additive authenticated endpoint: `GET /api/v1/tenant/reporting/documents/{id}/preview?locationId=...`. Return application/pdf bytes directly, with only safe metadata needed by the private Portal viewer. Add optional scoped inputs to the corresponding business download helper as necessary; existing public token routes are not the preview mechanism and are not expanded.

There is no client-side recreation of PDF layout and no new document engine. Same inputs/renderer yield the same document content/layout. A later download rechecks current access/status and may reflect a newly voided document or changed logo; no persistent preview transaction/snapshot is promised. The observed existing QuestPDF byte-determinism test flake must not be mistaken for evidence that different render engines are needed.

## 14. Preview security/caching

Authenticate and authorize before rendering. Reject cross-tenant/branch/unknown-role/self-only/unproven-location requests before reading/generating private content. Use Result/error conventions, private/no-store, no-referrer, nosniff and no public URLs/tokens. Server-derived safe filenames never contain customer names/emails or arbitrary headers.

Bound line/logo/output sizes using existing upload/storage limits and reviewed render-input limits; cap PDF output for the viewer (propose 5 MiB), use existing rate-limiter infrastructure and request cancellation where supported. QuestPDF generation is synchronous today: do not claim a cancellation token alone can forcibly terminate arbitrary native rendering. Input bounds and admission limits are necessary; no new scheduler/process engine in F5.

Angular fetches through the authorized API client, validates PDF response type, and creates a local Blob URL. Never embed a bare API URL expecting bearer authentication, use public billing-token URLs, or send documents to an external PDF viewer. Revoke object URLs/abort pending requests on close, document/tenant change and destruction. No localStorage, service-worker preview cache or persistent temporary blob storage.

Keep API frame-ancestors none/X-Frame-Options DENY. Inspect the Portal host's frame/object CSP; only a narrowly scoped same-origin/blob viewer allowance if needed. Do not weaken global framing protections. Browser viewing failure must leave secure Open/Download actions available.

## 15. Portal UI

- Customer profile: deferred segment panel/chips, scope/availability label and explanations; no clinical intake contents or activity changes.
- Customer list: one batch of chips per visible page; retain all existing search/account filters/counts/pagination. Cancel stale page/location/tenant requests. No generic audience builder.
- Finance Documents: Preview button only for eligible records; lightweight `AdeniModalComponent`-based viewer, document kind/number/status, loading/error state and keyboard-accessible Open/Download/Close controls. No new standalone app/page designer or existing route changes.
- Test focus trap/initial focus/Escape/focus restoration and provide text metadata outside the PDF. Reuse existing CDK/Carbon/Adeni facilities as needed; native PDF content is not the only accessible action surface.
- Signals for local/computed state; lifecycle-safe RxJS/AbortSignal for async work. No new state library, SignalR subscription or Discover behavior.
- Use F4's lazy API/schema/copy subpath pattern and deferred components: Portal has only about 3.75 kB initial-budget headroom. Keep heavy PDF/segment code out of initial bundles; no budget increases or unrelated component rewrite.

## 16. Test strategy

- Pure policy: exact 29/30/89/90/179/180-date edges; 1/2/3/6 visit/no-show thresholds; overlap rules; upcoming booking suppression; no recorded visits; partial/missing data; no invented appointment no-show; deterministic version/reasons; no machine-local time or mixed-currency arithmetic.
- Source facts: booking/queue dedupe, walk-in-generated booking with unlinked tenant customer, party size, legacy missing timestamps/location, void/cancel/reject exclusion as appropriate, same tenant/customer identity, future bookings, no F2/F3/F4 private data dependencies.
- Scope/auth/entitlements: cross-tenant IDs and batches, branch versus business-wide views, foreign/inactive/invalid location, self-only/unknown roles, Starter versus runtime Professional/Business/trial/billing overrides, no Finance-only access to F1 segments.
- Real SQL Server: grouped/min/max/window queries, queue-linked identity and dedupe translation, null/location/time boundaries, index/query-plan checks and DACPAC parity. Include the existing full SQL category to protect F2 retention and F3 money/F4 lease behavior.
- Privacy: current classifications exported under existing admin controls; erasure prevents rederivation; no projection/cache residue or sensitive logs; walk-in-only privacy gap explicitly tested/documented as unavailable rather than covered by a nonexistent workflow.
- Preview: access guard shared with supported final PDF; wrong tenant/role/location rejected before renderer; unsupported/unlinked/void cases; same document type, snapshot name, logo bytes, line ordering, amount/date inputs and renderer selection; no issuance/payment/email side effects; safe filenames/private headers/type/size limits.
- Frontend: segment explanations/unavailable state; one request per page rather than N+1; stale request cleanup; existing filters/counts and F1 cursor unaffected; PDF Blob lifecycle, no arbitrary URL trust, focus/keyboard actions, fallback/download, no duplicate realtime subscriptions. Shared schema/API public-import compatibility and English/French copy.

After approval run `dotnet build Adeni.slnx -c Release`; `dotnet test Adeni.slnx -c Release`; isolated DACPAC publish plus `ADENI_SQLSERVER_TESTS=1 ... dotnet test tests/Adeni.Infrastructure.Tests/Adeni.Infrastructure.Tests.csproj -c Release --filter 'Category=SqlServer'`; `npm run typecheck`; shared, Portal and Discover test scripts; Admin tests only if an existing applicable script/surface changes. Run `npm run build:business`, `build:admin`, `build:discover` and record exact budgets. Use npm ci if dependencies/environment require it; introduce no new dependencies. Preserve every failure, including the existing PDF flake, rather than hiding it with a budget or test relaxation.

## 17. Final branch closeout approach

Create `docs/specs/squarespace-inspired-v1-closeout.md` **after implementation/validation**, with completed/partial/unavailable features, architecture, security/privacy, SQL objects/indexes, versioned API/Portal additions, evidence/test totals, production blockers, deployment order, deferred work and recommended next priorities. Do not label it completed during planning.

Current committed F1–F4 comparison against audit baseline (not final F5 totals): **250 files, 10,771 insertions/84 deletions; 41 actual test-source files changed** (43 test-directory/spec paths if two support files are included); **25 tables and two schemas added**. Last F4 recorded validation: **898 distinct passed cases**, including 24 real SQL cases; approximately **242 net cases** over the audit baseline when its five existing Discover tests are included. Final F5 closeout must recalculate discovery/source counts and disclose parameterized/opt-in accounting, not simply add rerun totals.

Inventory APIs by actual route/action and frontend routes from source; current examples include F1 profile/activity, F2 intake/holds, F3 sandbox commerce, F4 calendar/permalink endpoints, Portal intake/quick-pay/calendar/link pages and Discover intake/pay/gift pages plus server `/book` redirect. Expected F5 adds scoped segment sidecars and preview, not a new standalone frontend route.

Review the entire diff from baseline: no Domain/Application→Infrastructure references, no new cross-module Infrastructure calls, thin controllers, preserved versioned/public interfaces, runtime permission/entitlement authority, no unexpected dependencies/secrets, safe exports and no live feature activation. The existing HealthController DbContext connectivity probe is a diagnostic exception, not a product-data query; no new service-locator pattern is proposed.

Record findings separately: F1 is a foundation with unavailable preferences/Retention; F2 is non-clinical with approved retention/holds; F3 is Development/Testing Stub only; F4 providers stay disabled until configured/verified; F5 supports only the explicit labels/preview subset. Do not imply memberships, packages, no-show appointments, net-spend intelligence or arbitrary branding exist.

Deployment order: review baseline hardening first, additive DACPAC/index publish and rollback plan, backend policies/ports, shared/client and Portal deployment, hosted/staging verification, then separately approved optional provider activation. Recommend stacked PR review against hardening; integration/promotion by PR through dev→qa→main with protections/CI. Do not merge or promote any branch in F5.

## 18. Known production blockers carried forward

- Portal production CSS: services 24.50 kB, staff 27.41 kB, plan 28.33 kB versus 24 kB. Its initial bundle is about 2,996.25 kB against a 3 MB error threshold.
- Admin: initial about 2,186.96 kB versus 2 MB, and 14 component styles above 24 kB (25.25–37.71 kB).
- Discover: shared `_data-grid.scss` 16.90 kB versus 14 kB. No arbitrary budget increases; rerun and report F5 measurements.
- Hosted CI/Node 22.22.3 and branch-protection verification remain outstanding; recorded F4 local runtime was Node 24.19.0. Existing PDF byte-comparison test failed intermittently despite later passes.
- Live F3 merchant settlement/onboarding/provider idempotency/refund/confirmation prerequisites remain incomplete and blocked; no live Stripe/Paystack enablement.
- Google/Microsoft real OAuth/provider consent/scopes/refresh/read/write/revoke verification, Azure TLS/trusted forwarding/callback cookies/CORS/query-log redaction, Key Vault/client secrets/versioned encryption keys, production quota tests.
- Local SQL/DACPAC/24 category tests passed at F4; actual hosted staging/production SQL deployment/backup/recovery and Hangfire health/backlog/retention/reconciliation behavior still require verification.
- Database commit can succeed before post-save event/enqueue failure. Keep the documented [transactional outbox design](../transactional-outbox-design.md) deferred; no implementation in F5. Existing calendar polling/write ambiguity and scheduling concurrency limits remain.
- Market-specific privacy/legal review for intake retention/holds, customer-inference explanations/deletion, gift-card policy and existing public document links. These are product policies/technical controls, not claims of legal compliance.
- Existing billing/statement location and legacy-role gaps described below must be tracked explicitly; no unconditional production-ready claim.

## 19. Risks/open questions requiring plan review

1. Approve the six exact defaults/labels, including frequency-only VIP and queue-only repeated no-shows. Beauty/wellness repeat cycles differ; V1 is deliberately simple/explainable and must not become automated adverse treatment.
2. Approve deferred monetary/member/package/general appointment-no-show classifications, list filtering/count summaries, quotes/statements and unlinked document previews. They are not reliable/small under current data/scope contracts.
3. **Preview access alignment:** `BillingDocumentsController` currently permits Finance OR Bookings and checks document tenant, but lacks F1-style location resolution; `GetPdfAsync(id)` itself does not verify actor/location. `PortalPermissionRoles.Normalize` promotes unknown roles to Owner. Characterize membership/header/claim resolution and use a shared recognized-role/tenant/location policy for the supported preview/final-PDF subset. This is an explicit proposed security tightening, not a silent assertion that existing guards already suffice. These behaviors predate the feature branch in the inspected files; no broad legacy authorization rewrite is proposed.
4. **Direct F3 regression candidate:** F1 resolves a sole active location to a non-null ID even when location was omitted; `CustomerGiftCardsQueryService` then rejects all non-null resolved locations. Therefore its advertised business-wide gift metadata is unavailable in the normal single-location profile path. Existing tests mock a null resolved location and miss this interaction. Recommend one characterization/integration test and a small scope-evidence fix during closeout if reproduced: distinguish an implicit sole-location business view from an explicit branch view, without widening explicit/multi-branch access, exposing codes, or changing money behavior. Stop/report if a larger scope-policy redesign is needed.
5. Legacy linked queue records and mutable completion/payment status can limit historical certainty. Missing identity/location/timestamps must be visible; no placeholder negatives. No attempt to reconstruct an event history that does not exist.
6. Existing platform-customer privacy deletion does not automatically define a workflow for unlinked walk-in-only tenant records. No projections avoids new deletion rows, but do not claim that wider inherited privacy gap has been solved.
7. PDF name snapshots and current logo bytes can change at different times; same-renderer fidelity is not a promise of immutable archival bytes. Existing loader/storage size and synchronous rendering limits need tests, not speculative hard timeout claims.
8. Added customer-scoped indexes and lazy UI boundaries must be justified by real SQL/build results; if scale requires a new projection or broader report-authorization contract, stop and return a focused design rather than expanding F5.

## 20. Implementation sequence

1. **Review this plan; stop before implementation.** Confirm defaults, deferred scope, preview/final access alignment and the proposed small F3 characterization/fix.
2. Add characterization tests for F1 scope/history, linked walk-in identity, document membership/location policy and the F3 single-location regression. Resolve only small directly related gaps approved here; report larger changes.
3. Add Tenancy Application segment policy/access orchestration and Booking batch facts port; source SQL/index evidence and real SQL tests. Add explainable/versioned contracts, privacy export/erasure suppression and entitlement/security tests. No projection tables or new jobs.
4. Add sidecar/batch APIs and lazy Portal chips/explanations; preserve current lists/F1 activity, existing public exports and Discover behavior.
5. Add the shared supported-document guard and Reporting preview adapter over the existing Finance/QuestPDF path; safe direct PDF response and lightweight private Portal modal. Test fidelity, privacy, sizes, URL cleanup and accessibility. No document layout/catalog expansion.
6. Reproduce/fix the small F3 scope interaction only within the reviewed evidence bounds, then perform full F1–F5 architecture/security diff review and complete backend/SQL/frontend/build validation. Create final closeout with exact totals and remaining blockers, commit reviewable slices and stop. No merge, promotion, live provider activation or further feature phase.
