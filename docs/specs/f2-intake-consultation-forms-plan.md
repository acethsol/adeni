# F2 — Intake / consultation forms implementation plan

**Status: implemented; awaiting F2 review.** See [F2 closeout](./f2-intake-consultation-forms-closeout.md).

Branch: `feature/squarespace-inspired-v1`; implementation base: F1 HEAD `3fd36d6e4d0c1f3a61bc814992847441ac5e6697`.
Authorities: [product direction](../product-direction.md), [architecture](../architecture.md), [database conventions](../../db/README.md), [F1 closeout](./f1-customer-profile-activity-closeout.md). This proposal follows the repository's [spec template](./_template.md); F2 is the explicitly requested scope extension, not a new sprint inferred from older roadmap notes.

Goal: allow businesses to configure small non-clinical consultation forms, pin their published definitions to bookings, and let the authenticated booking holder submit once for authorized review.

## Approved retention addendum

The subsequent V1 decision resolves the earlier retention question: completed responses expire 24 calendar months after the appointment date; unsubmitted non-actionable assignments expire after 90 days. Explicit submission legal holds defer deletion; release reevaluates expired retention and pending privacy deletion. These are V1 product rules, not a claim of legally mandated periods in every market. No tenant retention settings or answer-reuse feature is added. The proposal below records the approved design; the closeout describes actual delivery and limits.

## 1. Existing capabilities discovered

| Area | Inspected implementation / finding |
|---|---|
| Forms/custom fields | No implemented intake, questionnaire, custom-field, form-template or submission model found in Domain/Application/Infrastructure or the Angular apps. Do not represent an unrelated configuration JSON file as a form engine. |
| Service metadata | `Domain/Booking/ServiceOffering`, `IServiceCatalogService`: names/descriptions, pricing, duration, delivery type, menu groups and add-ons. No intake link. Reuse service identity; do not add copied service facts to Intake. |
| Bookings | `BookingRecord`, `BookingLine`, `BookingGuest`, `IBookingService`; up to 10 service lines, booking-holder CustomerId, optional named guests, staff/location, timestamps and operational statuses. Guests have no independent authenticated identity. |
| Creation paths | `BookingService.CreateAsync`, `QuoteRequestService.AcceptAsync`, and `QueueService.CreateLinkedBookingAsync`. Normal bookings have line snapshots; accepted quotes may use only the primary ServiceOfferingId. Queue conversion can create a CheckedIn booking at service start, including a synthetic walk-in customer. |
| Customer access | `BookingsController` resolves customer JWT sub and existing Development/Testing fallback; customer-owned operations query the platform customer and booking ownership. Discover has `CustomerApiService`, `customerAuthGuard`, booking confirmation and `/my-bookings`. No customer intake share-token infrastructure exists. |
| Policies/consent | BusinessProfile stores plain-text policies, including terms. Discover's booking panel has a local policy checkbox when enabled, but `CreateBookingRequest` carries no acceptance text/version/value. This is not a persisted acknowledgement record and cannot be reused as proof of intake consent. |
| Uploads | `ICustomerMediaService` / `CustomerMediaService` support authenticated quote-photo uploads, with public/media storage concerns. They are not a private sensitive-document store. No attachments in F2. |
| Permissions | PortalPermissions has Services, Bookings, BookingsSelf, etc., but no sensitive-intake review permission. Services includes receptionist/ops; full Bookings includes receptionists. Practitioner bookings have assigned-staff scope; customer history does not consistently do so. F1 restricted its new aggregate to full Bookings pending review. |
| Entitlements | Shared JSON: Starter/Professional/Business, 25 catalog fields; runtime 30 including derived/compatibility fields. No intake key or form quantity limits. Existing effective-SKU and contract parity mechanisms remain authoritative. |
| Versioning | PricingCatalogVersion has draft/active/retired transitions and refuses editing non-drafts. BookingLine and billing-document snapshots demonstrate historical-data preservation. Notification overrides are mutable and unsuitable as the historical form model. |
| Audit/logging | `IAuditLogWriter` and metadata audit records exist; generic AuditMiddleware handles admin requests, not all tenant/customer activity. Serilog request logging is configured without answer-body capture. PiiDestructuringPolicy masks explicit wrappers, not arbitrary DTOs automatically. |
| Privacy | `AdminCustomerService` exports identity/bookings and marks/redacts platform identity on erasure; no intake export or answer purge exists. The legal checklist's broader purge wording is not evidence of an implemented purge worker. |
| Persistence/reliability | SQL/DACPAC is schema authority, EF is ORM only. DbContext saves first, then publishes events/flushes work; [outbox assessment](../transactional-outbox-design.md) documents committed-data/request-failure risk. Intake assignment cannot rely on an after-save event. |
| Discover SSR | `app.routes.server.ts` currently uses Server rendering for `**`. Auth0 loading is browser-aware, but new sensitive forms require explicit SSR/hydration tests. Discover already has its own unit-test script. |

## 2. Proposed module ownership

Add a small **Intake** bounded context inside the existing four layers; no service deployment, repository framework or generic forms platform.

- Intake owns templates, published definitions, service-to-template links, pinned booking requirements, answer validation, submissions and access-controlled review.
- Booking owns services, booking identity, customer/staff ownership, location, appointment time/status and lifecycle. It invokes an Application port to resolve/stage intake requirements at creation.
- Identity supplies tenant-specific business membership/raw role and authenticated customer identity through Application contracts. Never associate responses through phone/email or create a customer as a side effect of a form read.
- Tenancy/F1 may consume status-only Intake DTOs. Reporting, Notifications and Storage do not become response stores.

Proposed contracts: `IIntakeTemplateService`, `IIntakeSubmissionService`, `IIntakeStatusQuery`, a Booking-consumed `IBookingIntakeAssignmentWriter`, Intake-consumed `IIntakeBookingAccessQuery` and service-validation port, plus Identity/Intake privacy contracts where no suitable existing interface exists. Names are provisional. Implement each adapter in the source owner's Infrastructure folder and compose via Application interfaces. Controllers bind/map Result values only.

## 3. Proposed domain/data model

Use nine small relational tables under SQL schema `intake`:

| Entity/table | Principal fields / invariant |
|---|---|
| IntakeFormTemplate / form_templates | Id, TenantId, management Name, IsActive, CurrentPublishedVersionId (nullable before first publish), CreatedAt/UpdatedAt, RowVersion. No hard-delete after publication/assignment. |
| IntakeFormVersion / form_versions | Id, TenantId, TemplateId, VersionNumber, snapshotted Title/Description/Instructions, CreatedAt, PublishedAt nullable for draft; immutable content after publication. Unique tenant/template/version number; at most one draft per template. |
| IntakeFormField / form_fields | Id, TenantId, VersionId, SortOrder, FieldType, Label, HelpText, IsRequired. Unique order per version and composite tenant/version identity. |
| IntakeFieldChoice / field_choices | Id, TenantId, VersionId, FieldId, SortOrder, Label; only for choice fields. Option IDs, not editable labels, are submitted. |
| ServiceIntakeForm / service_form_links | TenantId, ServiceOfferingId, TemplateId, timestamps/concurrency revision. Unique tenant/service: zero or one required template per service. Links follow the template's current publication only for future bookings. |
| BookingIntakeAssignment / booking_assignments | Id, TenantId, BookingId, TemplateId, FormVersionId, AssignedAt; unique tenant/booking/template. No copied CustomerId, service content or mutable template pointer resolution at read time. |
| IntakeFormSubmission / form_submissions | Id, TenantId, AssignmentId, FormVersionId, SubmittedAt, IdempotencyKey, PurgedAt nullable. One final submission per assignment; no business-written/customer-impersonated submissions. |
| IntakeFormAnswer / form_answers | SubmissionId, TenantId, VersionId, FieldId and typed TextValue/BooleanValue/DateValue columns. Unique submission/field; correct value-column/type combination checked. Omitted optional answers have no row; explicit false is distinct from omission. |
| IntakeAnswerChoice / answer_choices | Submission/answer reference plus TenantId, VersionId, FieldId, ChoiceId; unique chosen option per answer. Composite FKs prevent selecting another field's/version's choices. |

The answer tables are bounded, schema-specific child entities with an explicit field-type enum and versioned FKs, not a generic EAV database for arbitrary entities. Single/multiple choice validation stays in Intake. No whole-feature JSON blob; no published JSON snapshot is necessary because published relational versions are immutable. This costs more explicit tables/constraints but supports SQL integrity, typed dates/booleans, privacy deletion and old-version rendering.

V1 field types: ShortText, LongText, SingleChoice, MultipleChoice, YesNo, Date, Acknowledgement. Proposed technical limits for review: 30 fields/form, 20 options/choice field, 250-character short text, 4,000-character long text, 64 KiB total submission request. These are resource/validation bounds, not plan entitlements. All author text is plain text, rendered with interpolation; no HTML execution, Markdown engine, script expressions or calculated values.

## 4. Versioning model

1. Create template and draft v1; edit only the draft. Draft title/instructions/fields/choices can change together under optimistic concurrency.
2. Publish validates field types, order, required settings, choice integrity and limits, then atomically freezes that version and changes the template pointer. No editing/deleting published content through the API.
3. Editing a published template clones content into a new draft with new version-specific field/choice identities. Publishing v2 changes only future requirement resolution.
4. Booking assignments and submissions reference v1 permanently. Old title, instructions, field order, choice labels and acknowledgement text render from v1; never join the current template for display content.
5. Use SQL rowversion on mutable management state with an expected revision in mutation contracts. Stale edits/publish/link changes return the existing conflict/error conventions, not silent overwrites. Unique version numbers handle concurrent publication; do not auto-merge fields.

Published-content immutability is enforced in the module's mutation API/domain invariants and regression tests. Do not claim ordinary FKs alone prevent a database administrator from editing content. Privacy purge may delete answer values; it does not rewrite the published definition a customer completed.

## 5. Booking/service association

- Zero or one required template per service; binding requires a same-tenant service and an active template with a published version. A template can serve multiple services. No optional per-service overrides or rule expressions.
- For existing multi-service carts, resolve all distinct booked services, including add-ons; fallback to the primary service where no lines exist (accepted quote path). Deduplicate templates, producing up to one assignment per distinct template. A single publication is selected for each template at the resolution point. Multiple forms per booking arise naturally from existing multi-service carts; there is still only one form per service.
- Resolve requirements inside the existing booking creation flow immediately before persistence. Use a two-step owner-module port: validate/resolve immutable version references without side effects, then stage assignment entities without SaveChanges. The booking, lines and assignments persist in the **same existing SaveChanges SQL transaction**. Do not attach required assignments through BookingConfirmed, Hangfire or after-commit work; Pending bookings need them too.
- Do not wrap existing event-emitting SaveChanges in a new outer transaction: current handlers may execute before that outer commit. Use the existing single-save boundary; validate all requirements before staging or collecting events, and do not let a failed preparation leave partial tracked state that another save could persist.
- Apply the hook to normal booking creation, quote acceptance and queue conversion. Existing booking-idempotency replay returns the original booking/assignments without recomputation or duplication. Capture a coherent published pointer at requirement resolution; a concurrent later publication cannot rewrite the selected immutable version. Pinning is defined at this resolution point, not a claim of serializing all template edits until booking commit.
- Changing/unbinding a service or publishing another version affects future bookings only. Template deactivation is refused while it has service links; the business must explicitly unassign those links first. Already assigned versions remain retrievable/submittable under the booking policy after deactivation. A stale/invalid persisted service binding fails clearly; never silently skip a required form.
- Intake completion is an operational requirement/status, **not a new booking confirmation, payment, check-in or completion blocker in F2**. No change to those product transitions is implied.

**Queue and party-size limitations requiring review:** conversion can create a CheckedIn booking at the current time and may use a synthetic walk-in customer. Still pin its requirements; expose closed-uncompleted status when the authenticated/pre-appointment window is unavailable. Do not fabricate a login, guess identity, let staff accept on behalf of customers or block the existing floor workflow. A separate secure walk-in completion flow is deferred. For named group guests, V1 completion is by the authenticated booking holder for that booking only; it is not consent or a separate completed intake for each guest.

## 6. Submission/edit policy

Recommended V1: **one final submission per assigned form, no server-side drafts and no customer edits after submission**. The customer sees a clear final-submit notice; local in-memory editing before submit is allowed. No reset/void/correction workflow or staff-edited answers in this phase.

- Retrieve only the version pinned to the owned booking. Accept first submission while booking is Pending/Confirmed and server time is strictly before current StartAt; CheckedIn/Completed/Paid/Cancelled/Rejected and past-start bookings are closed. Rescheduling later preserves the version and any final submission; an unsubmitted form follows the new valid booking time/status.
- Require Idempotency-Key using existing normalization/header conventions. Same key + same normalized typed answer set returns the original receipt/timestamp after a lost response; a successful replay remains a replay if the time window subsequently closes. Changed content/key or a competing second final submission returns conflict. DB unique AssignmentId closes the concurrent-submit race; re-check ownership before any replay.
- Reject unknown/duplicate field IDs, cross-version fields/options, scalar/array/type mismatches, unknown keys, duplicate choices, invalid counts and oversized input. Required text cannot be whitespace; required YesNo accepts false; required Acknowledgement must be true; optional unchecked acknowledgement records false if submitted. Optional missing is not silently coerced to false.
- Dates are strict valid `YYYY-MM-DD` / DateOnly / SQL date, not localized strings, DateTime or timezone-converted instants. Do not invent clinical date constraints or age/assessment rules.
- Acknowledgement records its actual Boolean answer and submission timestamp against the exact immutable label/help text/version. It is not a legal e-signature; no drawn signature, IP/device fingerprint or automatic marketing consent.

## 7. Authorization model and permission gap

**Gap:** there is no existing permission that specifically authorizes sensitive intake-answer review. Full Bookings is too broad to assume that receptionists should see answers; BookingsSelf is not sufficient to reuse customer-wide access. Do not automatically add a permission key, entitlement gate or widen any role.

**Recommended conservative V1 policy, explicitly subject to this review:**

| Action | Proposed server authority |
|---|---|
| Template draft/publish/activation and service binding | Same-tenant membership, an explicitly stored recognized Owner/Manager role, and existing Services permission. Do not grant this to every Services role automatically. |
| Required/pending/completed/time metadata on a booking | Full Bookings, or BookingsSelf with an actual linked StaffMemberId matching that booking; fail closed for an unlinked practitioner. Location scope must also be validated. No customer-wide practitioner endpoint. |
| Full form submission review | Same-tenant recognized Owner/Manager role **and** full Bookings. Receptionist/Ops/Accountant and practitioner answer review denied in V1. |
| Customer read/submit | Authenticated sub → existing platform customer → exact booking ownership → exact assignment/version. Customer route derives tenant from the owned booking; no client-provided tenant/customer ID grants access. |
| Privacy export/erasure | Existing authenticated admin/MFA/audit flow with a specific privacy operation, through an Intake Application port; no general admin-directory response gains answers. |

Use an Identity-owned tenant-specific access adapter returning the persisted role/membership when needed. Existing role normalization maps unknown/legacy values to Owner; **do not use that fallback as sensitive-review authority**. Unknown raw roles fail closed for new sensitive actions, without rewriting the global role policy.

If practitioners must review answers in F2, that is an unresolved permission-design decision: explicitly approve an appropriate sensitive-review policy/permission and assigned-booking rules before that slice is implemented. Do not infer approval from access to F1, Messages or Services. Backend checks are authoritative; client action visibility is only presentation, not a substitute for capability or entitlement rules.

## 8. Sensitive-data handling

- Treat both definitions and answers as potentially sensitive despite non-clinical intent. Authoring guidance must forbid diagnoses, prescriptions, treatment plans, medical charting/scoring and requests for clinical histories. Users can still type sensitive information; a disclaimer or field whitelist is not a guarantee that data is harmless.
- No answer/title/field-label contents in request logs, exceptions, audit MetadataJson, analytics, session replay, error breadcrumbs, notifications, SignalR broadcasts or F1 activity. Log allowlisted IDs/action/status/count/correlation metadata only; validation errors identify safe field IDs rather than echoing rejected values.
- Authenticated form/answer endpoints return `Cache-Control: no-store`; no shared Redis/CDN cache, localStorage/sessionStorage/IndexedDB drafts, service worker answer cache or share tokens in URLs. Browser state is cleared on logout/customer switch/destroy. Render escaped text; no innerHTML. Exclude input values and review screens from any analytics/session capture.
- SQL Server/Azure storage access and at-rest encryption follow existing deployment standards; do not claim a new application-encryption or clinical-compliance guarantee. Access-control testing is mandatory.
- Explicit metadata audit entries are needed for management mutations, submission and sensitive review because generic middleware only covers admin routes. Use the existing audit infrastructure; fail answer-review responses if their required audit write fails. Existing writer calls SaveChanges: validate before staging mutations and never use it to accidentally commit partially prepared booking state. Post-save audit failure can still surface after a mutation persisted; idempotent submission and management refresh/conflict handling must tolerate it. No outbox implementation in F2.
- Extend privacy through Intake-owned export/purge ports. Proposed dedicated `GET /api/v1/admin/customers/{id}/intake-export` keeps the existing identity/booking export shape intact, uses existing admin MFA and explicit audit, and returns sensitive data only for this intentional privacy operation. Generic CSV/PDF/business exports remain unchanged and exclude answers.
- Hook the existing erasure request into a same-save staged Intake answer purge/redaction, leaving minimum assignment/version metadata and `PurgedAt`. Do not merely anonymize global Customer while leaving response text readable. Repeated purge is safe; do not resurrect deleted answers on idempotency replay. Select lawful retention/hold rules before enabling real-customer data; a focused Intake retention cleanup may use existing Hangfire if required, not a new scheduler or generic privacy rewrite. Until that decision is implemented/tested, production intake collection is blocked.

## 9. Proposed additive APIs

Names/payload details will be finalized after review. All controllers use existing Result/ApiResults/stable error code/localized-error conventions. No existing booking/service/customer/F1 DTO is changed to carry answers.

| Method | Route | Purpose |
|---|---|---|
| GET / POST | `/api/v1/tenant/intake/templates` | Paginated management list / create draft; conservative management policy. |
| GET | `/api/v1/tenant/intake/templates/{id}` | Management detail/current draft/publication summaries. |
| POST | `/api/v1/tenant/intake/templates/{id}/draft` | Clone publication into one editable draft. |
| PUT | `/api/v1/tenant/intake/templates/{id}/draft` | Replace bounded ordered definition using expected revision. |
| POST | `/api/v1/tenant/intake/templates/{id}/publish` | Validate and freeze draft/update pointer. |
| PATCH | `/api/v1/tenant/intake/templates/{id}` | Management name/active state under concurrency policy. |
| GET / PUT | `/api/v1/tenant/intake/services/{serviceId}` | Read/replace or clear the single service-template binding. |
| POST | `/api/v1/tenant/intake/bookings/statuses` | Bounded read-only batch of booking IDs → required/count/status/timestamps; authorize each booking, no answer contents, no bulk-ID disclosure. |
| GET | `/api/v1/tenant/intake/bookings/{bookingId}` | Authorized booking assignment status list. |
| GET | `/api/v1/tenant/intake/bookings/{bookingId}/forms/{assignmentId}/submission` | Restricted explicit full-answer review, including pinned definition. |
| GET | `/api/v1/bookings/{bookingId}/intake` | Authenticated booking holder's assignment/completion list, including an empty not-required result. |
| GET | `/api/v1/bookings/{bookingId}/intake/{assignmentId}` | Pinned form plus the holder's final response if submitted, or editable definition if allowed. |
| POST | `/api/v1/bookings/{bookingId}/intake/{assignmentId}/submission` | Typed answers, pinned version precondition and Idempotency-Key; never accept another chosen version or customer identity. |
| GET | `/api/v1/admin/customers/{id}/intake-export` | Dedicated admin-assisted privacy export as described above, not general staff reporting. |

Status DTOs distinguish `notRequired`, `pending`, `completed`, `closedUncompleted` and `purged`; include assignment/version IDs, submission timestamp when appropriate and an explicit canSubmit/reason for the owned booking. A rejected/deactivated template does not become a new customer's readable form through a guessed ID. Use safe not-found/denial conventions for mismatched booking/assignment/customer IDs. New errors can include `intake.validation`, `intake.conflict`, `intake.submission_closed` and inactive/unpublished-binding failures; keys/locales must be added together. Batch status POST is read-only and still authenticated, bounded and ownership-checked.

## 10. Portal UI changes

- Small Forms section within the existing Services area, visible only for the approved management policy. Standalone Angular components, Carbon/Adeni controls, Signals for synchronous state and RxJS/lifecycle cancellation for async changes.
- Ordered field editor with type selector, required toggle, choices and move-up/down buttons; a read-only published preview, explicit draft/publish distinction, revision conflicts and active-state feedback. No drag dependency, visual-builder package or conditional logic.
- Existing service editor gains a single published-template selector using the separate association API; inactive/draft-only templates cannot be selected.
- Booking detail/list shows required/pending/completed metadata with bounded batch loading; full answers open only through an explicit authorized booking intake review panel. No general document library or answer search.
- Review panel renders the actual completed version, timestamp, acknowledgement text/value and answer types, with unavailable/purged states. No inline answer edits, generic export button or copies in UI logs/storage.

## 11. Customer-facing UI approach

Use **Angular Discover**, not a new application: completion links from booking confirmation and My Bookings to a guarded `/my-bookings/{bookingId}/intake/{assignmentId}` route. CustomerApiService attaches the existing JWT; dev shortcuts remain Development/Testing only. A URL containing IDs is a navigation aid, never authorization. No anonymous completion link or reusable bearer token in F2.

Show remaining forms, plain non-clinical instructions, typed fields, final-submit confirmation and completion receipt. Retry network failures with the same idempotency key; do not save sensitive drafts in persistent browser storage. Losing an unsent draft on refresh is an explicit V1 limitation. One booking holder submits; no fabricated guest identities or staff impersonation.

Because Discover uses catch-all SSR today, make the **new private intake route browser-rendered** (or prove a no-sensitive-data SSR alternative in explicit tests), with no private TransferState/hydrated answer payload, cacheable HTML, sitemap entry or SEO metadata containing customer information. Limit the render-mode change to the new private route. Preserve public marketplace SSR, TransferState/PendingTasks, hydration, market resolution and existing public URLs. customerAuthGuard currently redirects login to `/my-bookings`; add a safe same-origin return-path mechanism for the new route, not an arbitrary redirect parameter.

Shared typed APIs remain mobile-ready for later Flutter implementation; Flutter will mirror contracts through existing OpenAPI/DTO conventions, not import npm or use embedded public-token forms. No Flutter work in F2.

## 12. F1 profile/activity integration

Recommend no F1 API/timeline contract changes in F2. Display a status-only booking intake section inside the existing customer experience using a separate bounded status query over bookings already visible under F1's full-Bookings/location/history rules. Do not use an unbounded Intake query to bypass the 3/25 history cap or expose a practitioner customer aggregate.

F1 timeline remains unchanged: no new activity-kind enum, new sensitive source, answer text or field labels. A metadata-only timeline adapter is optional later work after review; it is not required for practical booking intake visibility now. Full-answer access always requires the separate review policy, even if metadata is visible.

## 13. Entitlement recommendation

The current catalog has no intake feature or quantity gate. Starter already includes unlimited services/bookings, public profile, messaging/basic analytics; Professional/Business add retention/reporting and other flags, not a form entitlement. Do not repurpose CustomerRetentionAnalytics, AdvancedPermissions or a capability flag as intake purchase authority.

**Recommendation for explicit approval: initially ungated by plan**, with identical security/technical validation bounds across SKUs. Universal at launch is the simplest operational intake model; paid-only access and business quantity limits would need a separate product/catalog/parity/backend-enforcement decision. Nothing in this planning change alters entitlement behavior. If a gate is introduced later, already assigned forms/submission access must not silently disappear on downgrade.

## 14. SQL/DACPAC changes

Create `intake` schema and the nine tables in `db/Adeni.Database`, with EF mapping in central persistence configuration and ITenantEntity filters plus explicit tenant predicates. No EF migrations, runtime EnsureCreated schema mutation or unvalidated JSON columns.

Composite tenant/version/field/choice/submission FKs prevent cross-tenant and cross-version relationships. Add additive unique `(TenantId, Id)` keys to Booking service/booking tables as needed for tenant-safe cross-schema FKs; these are relational constraints, not cross-module Infrastructure dependencies. Include TemplateId in the published-pointer/assignment FK shape to prevent pinning another template's version. Add unique assignment/submission keys, ordered-child indexes, date/type/nullability checks and rowversion columns. Use NO ACTION on historical parent deletes to avoid accidental cascaded response loss/multiple cascade paths; deliberate privacy purge deletes only permitted response data through Intake.

Published rows are insert/freeze/read through Intake; choices and fields cannot be mutated indirectly via draft update APIs. Relational CHECKs cannot enforce every cross-table field-type/required-value rule: server validation and real SQL tests remain necessary. Schema creation/constraints/cyclic template-pointer insertion order and SQL provider compatibility must be verified by DACPAC build/publish, not only EF InMemory tests.

## 15. Test strategy and validation

- **Domain/Application:** supported types/order/limits, required false versus missing/acknowledgement true, strict date parsing, choices and duplicate/unknown field rejection, final-submit/idempotency policy, active/draft/publication transitions and exact historical acknowledgement text. No clinical fields/scoring or arbitrary script execution.
- **Security/API:** unauthenticated/forged JWT context, cross-tenant IDs, another customer's booking/form/submission, inactive/draft template leakage, management/review raw-role checks (including unknown-role fallback), denied receptionist answer access, assigned/unassigned/unlinked practitioner status access, location scope and unauthorized batch IDs. Response/cache/log/audit/telemetry tests assert sentinel answers never escape into metadata, errors, F1, SignalR or normal exports.
- **Versioning/association:** v1 booking and submission survive v2 publication/reordering/renaming, service link changes/unlink/deactivation, competing draft/publish edits, normal/quote/queue creation and no-form cases, cart deduplication, add-ons, group booking-holder limitation, idempotent booking replay after template edits and assignment atomicity on SaveChanges failure. Verify required forms are not a new booking/payment/floor transition gate.
- **Submission:** correct values, missing required fields, malformed/oversized bodies, invalid option/version/assignment injection, simultaneous duplicate POSTs, retry after lost response, changed-key/body conflicts, appointment-time boundary/status changes, no final edit API, purged-response retry and cancelled/rejected read-only behavior.
- **Architecture:** controller constructor/query boundary checks; no Infrastructure references from Application/Domain or another module's adapter. Intake read hooks use ports, no post-save assignment event and no new generic repository.
- **Real SQL Server:** DACPAC publishes on existing schema, tenant/version composite FKs and typed CHECKs reject invalid writes, unique submission under concurrent connections, rowversion conflicts/publish atomicity, booking+assignment rollback, and actual review/status/date queries against historical versions. Include SQL-category tests in the existing hosted SQL job; InMemory alone is insufficient.
- **Portal:** draft/editor ordering/choices/required validation, publish/concurrency/inactive states, service binding, permission visibility, metadata versus answer review, historical-version rendering, purge state, cancellation/destroy/stale-route requests and no duplicated realtime subscription.
- **Discover:** field controls/access/final submit/retry and status UI, auth return-path safety, no browser-persistent answers; explicit server-render/browser-route/hydration tests proving no private answer data in HTML/TransferState, no authenticated prerender fetch, no public SSR/SEO/market regression and no hanging PendingTasks. Use its existing `@angular/build:unit-test` runner; extend server checks only as needed without a new framework rollout.
- **Shared/API-client:** Zod request/response parity, enums/date/choice types, backend error/localization parity, compatible root exports, JWT/tenant scope/idempotency/cancellation behavior, and unchanged existing booking/service/customer/F1 contracts and entitlement parity.

Implementation validation commands: `dotnet build Adeni.slnx -c Release`; `dotnet test Adeni.slnx -c Release`; configured SQL-category test run after SqlPackage publish; `npm test --workspace @adeni/shared`; `npm test --workspace @adeni/portal`; `npm test --workspace @adeni/discover`; `npm run typecheck`; `npm run build:portal`; `npm run build:admin`; `npm run build:discover`. Preserve/report inherited style/initial-bundle failures; never raise budgets to conceal them. CI branch filters target dev/qa/main, so a stacked PR into the audit branch is not proof of a hosted run.

## 16. Migration/backfill considerations

- Additive schema deployment first, application hooks/API/UI second; verify deployment ordering so a mixed-version application does not silently create unpinned required bookings. No automatic enabling/associating templates or new live collection before the approved rollout.
- Empty new tables on upgrade; **do not backfill requirements onto existing bookings** or infer historical requirements from today's service links. Existing bookings return no assigned intake. No invented historical consent or phone/customer relinking.
- Use synthetic non-clinical Development test fixtures only. Service links are explicitly configured by the business after publication; activate collection after all booking-creation instances support pinning. Prevent link activation during a mixed-version deployment until old creators are drained.
- Rollback removes/disables new writes/UI but retains tables and immutable historical data. If services have live links, do not roll back to code that silently ignores them: unassign only future requirements deliberately, preserve old assignments, or require a coordinated maintenance rollback.
- Add Intake privacy export/purge support before real response collection. Select retention/hold policy and verify that deleting/redacting answers preserves only appropriate minimal history. No broad historical migration or automatic form creation.

## 17. Risks / decisions for review

1. Approve or revise the conservative existing-role management/review policy. Practitioner full-answer access requires a separately reviewed policy/permission; F1's discrepancy remains deferred.
2. Approve one final submission/no edits and informational required status without booking/payment/floor blocking. If corrections or hard workflow gating are needed, review that product change first.
3. Approve one template per service with deduplicated multiple forms for a multi-service booking, plus booking-holder-only completion for groups.
4. Approve the closed-uncompleted treatment for immediate/synthetic walk-in conversions; a secure walk-in completion mechanism is not included and cannot be pretended to exist.
5. Approve initially ungated plan access and the proposed technical limits; no existing entitlement key implies an intake gate.
6. Set retention/legal-hold/privacy-export access requirements for Canada/Nigeria. Non-clinical wording does not remove the sensitivity of free text; real-customer collection is blocked until privacy lifecycle handling is approved and implemented.
7. Verify publication/submission concurrency, template deactivation/link races and all three creation hooks against real SQL Server. Avoid domain-event assignment or wrapping the audited save pipeline in an unsafe outer transaction.
8. Preserve private Discover rendering and existing public SSR; routing/auth guards currently need targeted adaptation and tests, not a public booking rewrite.
9. Production builds are already blocked by inherited CSS/Admin bundle budgets; local InMemory/Node checks do not replace hosted SQL/CI validation. No claims of production readiness based only on this plan.

## 18. Recommended implementation sequence (after approval only)

1. Resolve the decisions above, especially sensitive-review authority, submission/queue policy, entitlements and privacy retention. Finalize exact DTO/error contracts and small acceptance criteria before schema coding.
2. Add Intake domain/validation/Application contracts and SQL schema/EF mappings; publish/test tenant/version/choice/uniqueness/concurrency integrity on real SQL Server.
3. Add owner/manager template management, draft/publish and service association APIs and simple Portal editor; test that no customer collection is possible from drafts or invalid links.
4. Add same-save requirement preparation/staging to normal, quote and queue creation with idempotency/rollback regression tests. No live links until all deployed creation paths are ready.
5. Implement customer read/final submit, privacy purge/export and explicit review authorization/audit/cache handling; exercise security, concurrency and sensitive-payload exclusion before any real data.
6. Add guarded Discover completion UI with private-route SSR/hydration tests; wire confirmation/My Bookings. Add booking/customer status metadata and restricted Portal review, without changing F1 contracts.
7. Run full backend/frontend/SQL checks; report inherited blockers and remaining decisions, commit small reviewable slices on this branch, and stop for F2 review before F3.

This planning pass changes documentation only. No F2 code, SQL, API, permission, entitlement, UI, dependency or deployment changes were made; no application test/build results are claimed for a feature that has not been implemented.
