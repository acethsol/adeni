# F2 — Intake / consultation forms closeout

F2 is implemented on `feature/squarespace-inspired-v1`, based on F1 `3fd36d6`. The [approved plan](./f2-intake-consultation-forms-plan.md) and subsequent retention decision are the scope. No F3 work, branch promotion, merge, push or production deployment was performed.

## Delivered behavior

- Owner/manager template management: bounded ordered field editor, draft save, immutable publication, cloned new draft, activation state and one required published template per service. Published content cannot be edited through draft APIs. Publishing in Portal saves the displayed draft before publishing it.
- Seven field types: short/long text, single/multiple choice, yes/no, date and acknowledgement. Server validation checks required values, option/version identity, duplicates, dates, bounds and malformed shapes. False is a valid required yes/no answer; required acknowledgement must be true. No clinical scoring, scripts, branching, files or signature drawing.
- Existing normal booking, accepted-quote and queue conversion paths prepare/stage distinct applicable published versions before the booking save. Booking and required assignments persist together; no after-save assignment event or notification dependency. No-form services remain usable. Service/template changes do not rewrite existing assignments or submissions.
- Authenticated booking-holder completion from Discover confirmation/My Bookings. One final submission, no edits; same key/same typed answers replays the receipt, competing submission or changed key/content conflicts. First submission requires Pending/Confirmed status before the appointment starts. Exact published wording/version and final submission timestamp remain interpretable.
- Authorized status metadata in Portal bookings/customer history and an explicit booking review page. Full answers require a recognized stored Owner/Manager role plus full Bookings permission. Receptionists and assigned practitioners can receive permitted booking status only; unlinked/unassigned practitioners fail closed. Unknown/legacy role normalization cannot authorize sensitive management/review. F1's customer-wide practitioner aggregate remains deferred.
- Additive template/binding/status/customer submission/review/admin privacy/legal-hold APIs; compatible `AdeniApiClient.intake` domain facade and shared Zod contracts. Existing booking/service/customer/F1 payloads remain unchanged. No new plan key, quantity entitlement or purchase gate: approved initially ungated access, with technical resource bounds and backend authorization.
- Standalone Angular Portal pages, shared presentation-only typed field controls, Signals and abort-safe page/batch reads. Status calls are bounded to 50 booking IDs per request and use already-visible bookings; no extra history source bypasses F1 limits. No new SignalR subscriptions or state library.
- Private Discover route is lazy/client rendered, guarded by existing customer authentication, with a safe same-origin return path and no-store/noindex headers. No private form data in TransferState or server-rendered form controls. Public marketplace server rendering, SEO/market routes and existing hydration mechanisms remain intact. English/French strings included.

## Retention, privacy and audit

- Central Intake policy uses **24 calendar months after StartAt** for completed responses. This is an Adeni V1 product policy, not a universal legal mandate. No tenant-configurable retention UI/settings or future-answer reuse.
- Unsubmitted assignments expire **90 days after the booking becomes non-actionable**. No server-side drafts contain customer answers. Future cancelled/rejected/checked-in bookings use their recorded update time; past appointment start closes submission. Missing source linkage is not evidence for deletion.
- Purge physically deletes answer and chosen-option rows from normal application tables, clears the retry key, and records PurgedAt. There is no answer archive, copied response JSON, analytics payload or hidden application cache. Minimal receipt/assignment headers retain identifiers, pin/version, completion/purge/privacy timestamps and concurrency state to preserve completion/provenance and prevent replay/resurrection. Reusable immutable definitions remain intact for history and referenced receipts/submissions; answer deletion never rewrites another submission's version.
- Submission-specific legal holds record placed/released timestamps and actors, with allowlisted concise categories (`legalProceeding`, `regulatory`, `otherLegalRequirement`). No free-form legal narrative is accepted. Existing admin/MFA policy controls hold/export HTTP endpoints. A hold prevents normal/privacy purge; release immediately purges content if retention expired or privacy deletion was deferred. Submission rowversion serializes hold placement against purge, including identical-clock updates; a racing hold causes purge rollback rather than partial answer loss.
- Existing admin customer deletion stages Intake purge in the same save as identity redaction. Held submissions retain a pending privacy-deletion timestamp so hold release still purges even before normal expiry. A dedicated admin intake export preserves existing general export shapes. General reporting/CSV/PDF/F1 activity contain no answers.
- Existing Hangfire infrastructure schedules `intake-retention-v1` hourly; no new scheduler. Processing is tenant-specific and reads in batches. Sensitive response access is suppressed after expiry while awaiting a successful normal purge (except explicit holds); storage cleanup depends on healthy worker execution/retry. Monitor overdue cleanup and job failure rather than treating cron as a hard real-time guarantee.
- Metadata-only audit records cover template/binding changes, customer reads/submissions, review and privacy export. Admin mutations retain existing audit middleware. Hold rows independently retain their responsible actors/timestamps/categories. Request/model-validation errors do not echo malformed answers; answer/request/view ToString representations omit values. Retention jobs log identifiers/counts/status only. SQL backup/transaction-log lifecycle remains an infrastructure responsibility; F2 does not create an application-level recoverable answer copy.

## Data and architectural changes

Ten relational Intake tables now include the nine foundation tables plus `submission_holds`. SQL/DACPAC remains authoritative; EF maps composite tenant/version/field/choice relationships, root tenant ownership, unique submissions/assignments, rowversion and typed values. No EF migrations or generic EAV/JSON entity store.

Intake owns definition/submission/retention behavior. Booking-owned Application adapters provide service/booking/ownership facts; Identity owns strict membership checks. Existing privacy orchestration consumes Intake's staged purge port. Controllers perform binding/access-context resolution and Result mapping, never EF queries. New API error mappings are restricted to Intake codes. No new external application dependency or package/lockfile change.

Backend delivery: `f3baa82` (after foundation `89e94bf`). Frontend delivery: `2f5e70c`. The final documentation commit follows these. The changed-file inventory is below.

## Validation

| Command | Result |
|---|---|
| `dotnet build Adeni.slnx -c Release` | Passed, **0 warnings/errors**, including DACPAC and test projects. |
| `dotnet test Adeni.slnx -c Release` | Passed: Domain 26, Application 60, Infrastructure 326, API 234 = **646 passed**. Eight opt-in SQL cases skip in this ordinary invocation; all eight passed separately below. |
| `ADENI_SQLSERVER_TESTS=1 ConnectionStrings__AdeniDb='<local SQL connection>' dotnet test tests/Adeni.Infrastructure.Tests/Adeni.Infrastructure.Tests.csproj -c Release --filter 'Category=SqlServer'` | **8 passed, 0 skipped**, against actual SQL Server 2022 after DACPAC publish. Only the local credential-bearing connection value is replaced in this documented command. |
| `sqlpackage /Action:Publish /SourceFile:db/Adeni.Database/bin/Release/Adeni.Database.dacpac /TargetConnectionString:<local SQL connection> /p:BlockOnPossibleDataLoss=True` | Passed against isolated local test database; not an Azure/production deployment. |
| `npm run typecheck` | Passed all workspace checks, including Portal/Admin/Discover development builds and shared/API-client/UI/brand TypeScript checks. |
| `npm test --workspace @adeni/shared` | **80 passed**. |
| `CHROME_BIN=/tmp/adeni-chrome npm test --workspace @adeni/business` | **34 passed**. |
| `npm test --workspace @adeni/discover` | **7 passed**, including private-route/rendering/TransferState assertions and existing public SEO tests. |
| Private-route HTTP smoke test against development SSR server | HTTP 200, `Cache-Control: no-store`, `X-Robots-Tag: noindex, nofollow`; client shell without form inputs or answer sentinel. |
| `npm run build:business` | Fails inherited styles: services **24.50 kB**, staff **27.41 kB**, plan **28.33 kB**, each against **24 kB** error limit. Initial **2.99 MB**, below **3 MB** error threshold (above warning). New pages are lazy-loaded. |
| `npm run build:admin` | Fails inherited 14 component-style budgets and initial bundle, now approximately **2.18 MB / 2 MB** error limit. Shared additions affect size; no budget increase or waived failure. |
| `npm run build:discover` | Fails inherited shared `_data-grid.scss`: **16.90 kB / 14 kB** error limit. Initial approximately **1.44 MB**, below its error threshold. |
| `git diff --check` | Passed. |

Live SQL covers real deletion/hold/release/minimal receipts, cross-version FK and duplicate-submission rejection, optimistic template concurrency, atomic booking+assignment rollback, and a real two-context concurrent hold versus purge. The inherited Discovery and F1 SQL tests also ran. Running these exposed stale test-fixture dependencies: Discovery needed entitlement registration, and F1's location fixture needed its required business profile. Those fixtures were corrected without production behavior changes. Discover's old service fixture lacked `isAddOn`, and its scaffold test expected a removed hello-world heading; those assertions now match the existing route-outlet application. No unrelated production fixes were made.

Relative to F1: **45 new backend cases** (including five live SQL cases), nine Portal, three shared and two Discover cases = **59 new cases**. Existing booking integration now additionally verifies a required version is pinned during actual booking creation. Unique passing cases across the final suites total **775** (654 backend including SQL, 80 shared, 34 Portal, seven Discover); repeated executions are not counted again.

Environment: .NET 10.0.402 and SQLPackage 170.5.96; Node 24.19.0/npm 11.9.0. CI uses Node 22.22.3, so hosted validation remains required. The Chromium wrapper adds `--no-sandbox` only for this execution environment. Dependencies were already installed; no npm-ci rerun is claimed. Logs are local `/tmp/adeni-f2-*.log` artifacts, not committed evidence of a hosted CI run. Production builds are **not green**.

## Deployment order and limitations

1. Publish the additive schema before starting the new API/job code. Drain/upgrade all old booking creators before businesses activate service links; mixed old/new creators could otherwise omit requirements. No automatic backfill or default service association: historical bookings remain unassigned. Rollback must coordinate links/creators and retain schema/history, not discard responses.
2. Verify Hangfire server, hourly schedule, SQL permissions, failed-job alerting, retention lag and backup/log retention before collecting real sensitive information. Expiry is deterministic; physical purge is scheduled/retried, not hard real time. No claim of legal/compliance sign-off or secure deletion of infrastructure backups is made.
3. Immediate/synthetic walk-ins can have a required version pinned when already checked in, making completion closed-uncompleted. No staff impersonation, anonymous token flow or floor-operation requirement gate was added. Group booking completion is by the authenticated booking holder, not separate guest consent.
4. Final submissions cannot be edited, reset or reused. Unsaved responses exist only in memory and are lost on refresh. No attachments, drawn signatures, clinical records, reminder campaigns or document engine.
5. Full answers remain Owner/Manager only; practitioner-sensitive review needs separate policy approval. F1's legacy customer-self-scope discrepancy is unchanged. No answers enter the general timeline.
6. Forms/status/review screens are deliberately small. Legal hold operations are privileged API/client operations, not a new Admin case-management UI. Broader device/browser accessibility/visual acceptance and live Auth0/provider staging checks remain release validation, not asserted local test results.
7. Inherited production budget failures remain blockers. No arbitrary CSS/JS budget changes. Existing CI filters target dev/qa/main, so a stacked PR is not proof of a CI run. Do not merge or promote until required checks and the deployment/privacy review are satisfied.

F2 stops here for review. F3 gift cards/Pay Links and all later features have not started.

## Changed files

The inventory includes the approved plan and foundation through this closeout, compared with F1 HEAD.

```text
apps/discover/src/app/app.routes.server.ts
apps/discover/src/app/app.routes.ts
apps/discover/src/app/app.spec.ts
apps/discover/src/app/core/guards/customer-auth.guard.ts
apps/discover/src/app/core/seo-jsonld.spec.ts
apps/discover/src/app/pages/intake/intake.component.spec.ts
apps/discover/src/app/pages/intake/intake.component.ts
apps/discover/src/app/pages/my-bookings/my-bookings.component.html
apps/discover/src/app/shared/booking-panel.component.html
apps/discover/src/app/shared/booking-panel.component.ts
apps/business/src/app/app.routes.ts
apps/business/src/app/core/intake-api.spec.ts
apps/business/src/app/pages/bookings/bookings.component.html
apps/business/src/app/pages/bookings/bookings.component.ts
apps/business/src/app/pages/customers/customer-detail.component.html
apps/business/src/app/pages/customers/customer-detail.component.ts
apps/business/src/app/pages/intake/intake-booking-status.service.spec.ts
apps/business/src/app/pages/intake/intake-booking-status.service.ts
apps/business/src/app/pages/intake/intake-manager.component.html
apps/business/src/app/pages/intake/intake-manager.component.spec.ts
apps/business/src/app/pages/intake/intake-manager.component.ts
apps/business/src/app/pages/intake/intake-review.component.ts
apps/business/src/app/pages/services/services.component.html
apps/business/src/app/pages/services/services.component.ts
db/Adeni.Database/Security/intake.sql
db/Adeni.Database/booking/Tables/bookings.sql
db/Adeni.Database/booking/Tables/service_offerings.sql
db/Adeni.Database/intake/Tables/answer_choices.sql
db/Adeni.Database/intake/Tables/booking_assignments.sql
db/Adeni.Database/intake/Tables/field_choices.sql
db/Adeni.Database/intake/Tables/form_answers.sql
db/Adeni.Database/intake/Tables/form_fields.sql
db/Adeni.Database/intake/Tables/form_submissions.sql
db/Adeni.Database/intake/Tables/form_templates.sql
db/Adeni.Database/intake/Tables/form_versions.sql
db/Adeni.Database/intake/Tables/service_form_links.sql
db/Adeni.Database/intake/Tables/submission_holds.sql
docs/specs/f2-intake-consultation-forms-closeout.md
docs/specs/f2-intake-consultation-forms-plan.md
docs/specs/f2-intake-foundation-checkpoint.md
packages/api-client/src/index.ts
packages/api-client/src/intake/intake-api.ts
packages/shared/src/i18n/errors.ts
packages/shared/src/i18n/messages/overlay-en.ts
packages/shared/src/i18n/messages/overlay-fr.ts
packages/shared/src/index.ts
packages/shared/src/intake.test.ts
packages/shared/src/intake.ts
packages/ui/src/index.ts
packages/ui/src/intake/intake-fields.component.ts
src/Adeni.Api/Controllers/IntakeControllers.cs
src/Adeni.Api/Errors/ApiErrorResponseMapper.cs
src/Adeni.Api/Extensions/HangfireExtensions.cs
src/Adeni.Application/Booking/IBookingIntakeAssignmentWriter.cs
src/Adeni.Application/Intake/IIntakeRetentionService.cs
src/Adeni.Application/Intake/IIntakeServiceBinding.cs
src/Adeni.Application/Intake/IIntakeSubmissionService.cs
src/Adeni.Application/Intake/IIntakeTemplateService.cs
src/Adeni.Application/Intake/IntakeValidation.cs
src/Adeni.Domain/Intake/IntakeModels.cs
src/Adeni.Infrastructure/Admin/AdminCustomerService.cs
src/Adeni.Infrastructure/Booking/BookingService.cs
src/Adeni.Infrastructure/Booking/BookingServiceCollectionExtensions.cs
src/Adeni.Infrastructure/Booking/IntakeBookingQuery.cs
src/Adeni.Infrastructure/Booking/IntakeServiceQuery.cs
src/Adeni.Infrastructure/Booking/QueueService.cs
src/Adeni.Infrastructure/Booking/QuoteRequestService.cs
src/Adeni.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
src/Adeni.Infrastructure/Identity/IdentityServiceCollectionExtensions.cs
src/Adeni.Infrastructure/Identity/IntakeManagementAccessQuery.cs
src/Adeni.Infrastructure/Intake/BookingIntakeAssignmentWriter.cs
src/Adeni.Infrastructure/Intake/IntakeDefinitions.cs
src/Adeni.Infrastructure/Intake/IntakeRetentionHangfireJob.cs
src/Adeni.Infrastructure/Intake/IntakeRetentionService.cs
src/Adeni.Infrastructure/Intake/IntakeServiceBindingService.cs
src/Adeni.Infrastructure/Intake/IntakeServiceCollectionExtensions.cs
src/Adeni.Infrastructure/Intake/IntakeSubmissionService.cs
src/Adeni.Infrastructure/Intake/IntakeTemplateService.cs
src/Adeni.Infrastructure/Persistence/AdeniDbContext.cs
src/Adeni.Infrastructure/Persistence/IntakeModelConfiguration.cs
tests/Adeni.Api.Tests/Integration/BookingIntegrationTests.cs
tests/Adeni.Api.Tests/Integration/IntakeIntegrationTests.cs
tests/Adeni.Application.Tests/Intake/IntakeValidationTests.cs
tests/Adeni.Infrastructure.Tests/Discovery/DiscoverySearchSqlServerTests.cs
tests/Adeni.Infrastructure.Tests/Intake/IntakeRetentionTests.cs
tests/Adeni.Infrastructure.Tests/Intake/IntakeSqlServerTests.cs
tests/Adeni.Infrastructure.Tests/Intake/IntakeTemplateServiceTests.cs
tests/Adeni.Infrastructure.Tests/Tenancy/CustomerProfileSqlServerTests.cs
```
