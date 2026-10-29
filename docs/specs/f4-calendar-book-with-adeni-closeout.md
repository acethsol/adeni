# F4 — Calendar sync and Book-with-Adeni closeout

Branch: `feature/squarespace-inspired-v1`. Base: completed F3 `e0767963dd450c6ae670809afec2df49c3190b71`. F4 only; no F5, branch creation, protected-branch changes, merge, push or deployment.

## Delivered

- Calendar owns encrypted provider grants, OAuth attempts, selected calendars, complete busy snapshots, sync leases, conflicts and external event mappings. Booking consumes Application availability/admission ports; Calendar reads minimal Booking facts through an Application port. Identity owns persisted-role checks; Tenancy owns public permalinks. Controllers contain no EF queries. No new NuGet/npm dependency or entitlement key.
- Google Calendar free/busy and Microsoft Graph v1 selected-calendar views; provider-expanded recurring instances. Busy-only UTC intervals, with no external subjects, descriptions, attendees, notes or meeting URLs retained. Calendar names are private selection metadata only.
- Tenant-scoped independent consent; one account per provider/practitioner, up to five busy sources and one distinct output calendar. The same provider account can be explicitly authorized in different businesses; no global account exclusivity or automatic cross-business sharing.
- Authorization code + PKCE S256, validated signed OIDC identity/issuer/audience/lifetime/nonce, one-use state, ten-minute attempts, top-level launch and HttpOnly/Secure/SameSite=Lax correlation cookie. Role/membership rechecked at callback. AES-GCM envelopes bind tenant/record/provider; versioned keys, no real-token development fallback. Tokens never reach Angular or Hangfire arguments. Metadata-only audit, safe error categories, HTTP-client logging disabled, sensitive destructuring redacted, OAuth hosting-query diagnostics suppressed.
- Hangfire dispatcher every minute schedules bounded connection workers. Successful sync is due approximately every five minutes plus jitter/queue delay. Complete snapshots publish atomically with a staff-state rowversion; final normal booking admission stages a guard in its booking save. Late snapshot publication races roll back admission. Disconnect/refresh/publication/write saves all contend on the connection rowversion/lease. No transaction spans provider HTTP.
- Fifteen-minute freshness cutoff applies to affected connected staff only. An unconnected practitioner retains normal scheduling. Initial coverage is one past day and about 90 future days, with bounded requested-range expansion; 90 days is a cache window, not a new booking horizon. Missing or partially fetched coverage is never interpreted as free.
- Exponential retry (five minutes up to one hour, with jitter), provider Retry-After respected, reconnect state for revoked grants. Manual refresh does not bypass provider rate-limit delay. Existing accepted appointments remain valid. Assigned overlaps and conservative unassigned-capacity risks are visible; acknowledgment never removes busy time or cancels bookings.
- Optional write-back is separately disabled by default and requires extra consent and a separate output calendar. Generic appointment title/times/opaque ownership marker only. Confirmed assigned bookings create copies; source changes reconcile updates and cancellations remove owned copies. Google uses stable client event IDs; Microsoft uses transaction IDs plus marker lookup. An ambiguous Microsoft create is not blindly posted again. Historical completed/paid copies retain mappings without consuming the active reconciliation bound.
- Stable location permalink `/book/{slug}` survives public-profile slug changes. Server redirect to the current existing business page, noindex/no-store/no-referrer utility response, strict service/practitioner selectors, no price/currency/tenant/redirect overrides. One fixed HTML anchor, no scripts, iframe or styling builder. Portal exposes calendar controls under Staff and link/snippet controls under Public Page.
- Discover retains public business SSR, canonical URLs, hydration/PendingTasks/TransferState and market behavior. Selection is validated and applied once after rendering. Profile/catalog/staff/slot reads abort or ignore stale results; login return preserves only safe selectors. Normal booking creation, F2 intake pinning and existing deposit behavior remain authoritative. F3 custom charges/gift cards and live-payment guard remain untouched.
- Narrow scheduling corrections: branch-specific practitioner eligibility; distinguish a filtered-empty roster from shared-calendar fallback; service-cart eligibility intersection; skip spring DST gaps, select the standard-time fall occurrence once, reject invalid configured zones instead of offering UTC fallback slots; prevent TimeOnly wrapping past late-night closing time.

## Database and rollout

SQL/DACPAC is authoritative; no EF migrations. Nine additive tables: `calendar.connections`, `selections`, `staff_state`, `snapshots`, `busy_blocks`, `oauth_attempts`, `conflicts`, `booking_events`, and `tenancy.public_booking_links`. Composite tenant-aware FKs, unique connection/selection/mapping/slug constraints, interval checks and SQL rowversions. Staff/location alternate keys support those FKs; the existing booking tenant/id unique index is reused.

Publish the DACPAC before deploying the new backend. Deploy all availability/admission writers together before enabling a provider. Existing bookings get no fabricated practitioner assignment or historical write-back backfill. Link publication is explicit and idempotent; location deactivation disables resolution without reassigning old slugs. Provider connection and complete initial sync are explicit opt-ins.

Both providers remain **disabled unless explicitly configured**. No real Google/Microsoft account was connected and no live provider consent/read/write/revoke end-to-end test was possible without deployment credentials. HTTP adapter/identity/security tests use controlled local responses. This implementation is not evidence of live integration certification.

Configuration:

| Setting | Requirement |
|---|---|
| `Calendar:Google:Enabled`, `Calendar:Microsoft:Enabled` | Default false; set only after sandbox verification. |
| Provider `ClientId`, `ClientSecret` | Server-only registered OAuth credentials; secrets from existing Azure Key Vault configuration. |
| Provider `WriteBackEnabled` | Default false; additional consent and distinct dedicated calendar required. |
| `Calendar:ApiBaseUrl` | Fixed external HTTPS API origin; callbacks `/api/v1/calendar/oauth/{google|microsoft}/callback`. |
| `Portal:PublicBaseUrl` | Existing HTTPS Portal-origin authority; callback returns only to `/settings/calendars`. |
| `App:PublicWebBaseUrl` | Existing Discover-origin authority for booking links; no duplicate Calendar/Commerce public-origin setting. |
| `Calendar:CurrentKeyVersion`, `Calendar:EncryptionKeys:{version}` | Version name and base64 32-byte AES keys; retain old versions during rotation and securely back up with DB/key recovery policy. |
| Existing SQL Server/Hangfire configuration | Running background workers required; Testing intentionally runs jobs directly rather than hosting Hangfire. |

Enabled providers fail options validation for missing keys/credentials/HTTPS origins; disabled optional providers require none. Production's existing SQL/Auth0/storage/payment validation remains intact. Verify Azure trusted forwarding, real callback TLS/cookies/CORS and exact provider consent/scopes/account audience. Redact callback `code`/`state` and launch `ticket` in Front Door/App Service/access logs and future App Insights/Sentry instrumentation before activation; application redaction cannot configure those external systems.

## Validation

Final results: Runtime: .NET 10.0.402, Node 24.19.0/npm 11.9, Chromium 154, SQL Server 2022 Developer and SqlPackage 170.5.96. Hosted CI Node 22.22.3 and real Auth0/provider/Azure integration were not run. Dependency graph/lockfile unchanged; installed dependencies reused, no npm ci required by dependency changes.

| Command/check | Final result |
|---|---|
| `dotnet build Adeni.slnx -c Release` | Passed, zero warnings/errors; includes DACPAC. |
| `dotnet test Adeni.slnx -c Release` | Passed: Domain 26, Application 66, Infrastructure 386, API 250 = **728 passed**; 24 opt-in SQL cases skipped in this run. |
| `sqlpackage /Action:Publish /SourceFile:db/Adeni.Database/bin/Release/Adeni.Database.dacpac /TargetConnectionString:<isolated-test-connection> /p:BlockOnPossibleDataLoss=True` | Passed against isolated SQL Server 2022. SqlPackage launched with `DOTNET_ROOT=/workspace/dotnet DOTNET_ROLL_FORWARD=Major`; credentials supplied privately. |
| `ADENI_SQLSERVER_TESTS=1 ConnectionStrings__AdeniDb=<isolated-test-connection> dotnet test tests/Adeni.Infrastructure.Tests/Adeni.Infrastructure.Tests.csproj -c Release --filter 'Category=SqlServer'` | **24 passed**, zero skipped, including **8 new F4 SQL cases** and all 16 F1–F3 SQL cases. |
| `npm run typecheck` | Passed: Discover, Portal, Admin development builds; shared/api-client/UI/brand TypeScript checks. |
| `npm run test --workspace @adeni/shared` | 86 passed. Includes new API-client facade/HTTP contract coverage; api-client has no standalone test script. |
| `CHROME_BIN=<local-Chromium-wrapper> npm run test --workspace @adeni/business` | 44 passed, Chromium 154. Wrapper only adds the container's required `--no-sandbox`. |
| `npm run test --workspace @adeni/discover` | 16 passed, including SSR/TransferState/once-after-hydration/stale context and redirect safety. |
| `NG_BUILD_MAX_WORKERS=2 npm run build:business` | Failed: existing component stylesheet errors: services **24.50 kB**, staff **27.41 kB**, plan **28.33 kB**, each over **24 kB**. Initial bundle about **2,996.25 kB**, under 3 MB error limit; 2.5 MB warning remains. |
| `NG_BUILD_MAX_WORKERS=2 npm run build:admin` | Failed: existing initial budget **2 MB**, actual about **2,186.96 kB**, plus 14 existing component styles over **24 kB** (25.25–37.71 kB). No Admin product code or styles changed. |
| `NG_BUILD_MAX_WORKERS=2 npm run build:discover` | Failed: existing shared `_data-grid.scss` **16.90 kB** over **14 kB**. Initial bundle about **1.47 MB**, under 3 MB error limit. |
| Real local Node SSR utility HTTP | Valid `/book/studio?service=<public-id>` returned **302** to the current business route with `#book`, no-store/noindex/no-referrer. Unsafe `price=1` returned **400**. Used a local API stub; not a live provider test. |
| `git diff --check` and controller/dependency architecture tests | Passed. Protected dev/audit refs unchanged; qa/main absent locally and untouched. |

**898 distinct automated cases passed across the separate suites**: 752 backend including real SQL, plus 146 frontend/shared. **64 new F4 cases**: 52 backend (including 8 real SQL), 12 frontend/shared. The opt-in skips are accounted for by the separate SQL run, not described as SQL passes in the ordinary suite.

New SQL coverage: snapshot-publication/admission rollback; duplicate connections and tenant FKs; mapping uniqueness/tenant FKs; late refresh/disconnect fencing; disconnect during an in-flight read preventing late publication; failed partial replacement rollback; repeat sync/selection idempotency; competing worker leases. Other new tests cover signed OIDC nonce validation, state/correlation replay, independently consented same-account use across tenants, strict role denial, encryption/redaction, source request minimization, untrusted pagination, ambiguous writes/ownership checks, freshness/coverage expansion, retry/backoff/revocation, conflict/write-back cancellation/history, UTC/Toronto DST/Lagos and late-night closing boundaries, stable renamed links and rejected preselection.


A full-suite run intermittently failed the unchanged `ReportRendererTests.RenderDocumentAsync_same_fixture_is_deterministic` byte-comparison assertion. Its isolated rerun passed. This is reported as an observed test-reliability issue; F4 does not modify Reporting or claim its cause was established. The subsequent full-suite rerun passed, as recorded above. Reporting source/tests were left unchanged; retain this flake as a follow-up.

## Production blockers and limitations

- All three production Angular builds still fail existing budget categories; budgets and stylesheet sources were not changed. Portal's F4 startup growth was removed with lazy API operations/schema/copy subpaths; its initial bundle remains below the 3 MB error threshold. Detailed measured failures are in validation.
- Provider registration, Google consent verification as required by scopes, Microsoft personal/work account consent/admin policies, Key Vault keys and sandbox end-to-end verification remain activation prerequisites. Local tests do not prove Azure callback behavior or provider production quotas.
- Polling leaves an unavoidable external-change window; neither cached availability nor provider writes offer global exactly-once/instantaneous synchronization. Refresh-token rotation can require reconnect if the process dies after provider rotation but before SQL persistence. Full transactional outbox remains deferred; durable source/mapping reconciliation repairs missed post-save hints.
- Microsoft ambiguous create with no recoverable marker remains `create_unresolved` for operator reconciliation. No blind retry that could duplicate an appointment. Provider-side edits to an owned copy are reconciled from Adeni; no separate generic calendar drift editor.
- Explicit connection disconnect removes local credentials/selections/busy data and fences workers. Already written/in-flight external events may remain; disclose/manual cleanup in the dedicated calendar. Removing an entire provider application grant is an account-owner action; Adeni does not revoke unrelated sessions or other independently authorized connections.
- Quote acceptance and immediate queue creation retain existing semantics. They are not silently converted into normal scheduled admission. Unassigned bookings cannot be written to a practitioner's calendar; capacity-risk flags are conservative review hints, not invented assignments. No reschedule product API was added; changed source timestamps are reconciled if such a transition is added later.
- Technical work bounds: 20 due connections per dispatch; 90-second worker budget/three-minute lease; at most 12 extra retained coverage windows (16 request rows before pruning); 1,000 active mappings/candidate projections per connection. Limits are operational bounds, not plan gates. Overflow is visible as `projection_limit`; increase/paginate only after quota/load tests. Pending writes/deletes are not silently discarded because they aged out. Completed/paid historical mappings can archive once the appointment is past.
- Existing different-start/service overlapping booking-lock races and broad capacity design remain separate scheduling debt. F4's SQL snapshot guard addresses the calendar-publication/admission race; it is not a full scheduling concurrency rewrite.
- Calendar key rotation tooling, richer health metrics/alerts, high-volume reconciliation pagination, provider webhook hints/renewals, practitioner self-management, an iframe/widget and source attribution remain deferred. No CMS, website builder, external CRM, conferencing, customer calendar sync, full bidirectional editing, new scheduler, F5, Retention, WhatsApp API or live F3 payment enablement.

## Review commits

- `6d88538`: backend Application ports, provider/OAuth/sync/write-back logic, public-link APIs, SQL authority and backend/security/SQL tests.
- `d435f99`: lazy Portal calendar/link pages, safe Discover redirect/preselection/hydration, compatible shared/api-client additions and frontend tests.
- Final documentation commit: this closeout and the approved F4 plan. See branch HEAD for its SHA.

## File inventory

93 files in F4, grouped by their existing source paths:

- `apps/admin/tsconfig.json`
- `apps/discover/src/app/pages/business/business.component.html`
- `apps/discover/src/app/pages/business/business.component.ts`
- `apps/discover/src/app/shared/booking-panel.component.ts`
- `apps/discover/src/app/shared/booking-preselection.directive.spec.ts`
- `apps/discover/src/app/shared/booking-preselection.directive.ts`
- `apps/discover/src/booking-link-handler.spec.ts`
- `apps/discover/src/booking-link-handler.ts`
- `apps/discover/src/server.ts`
- `apps/discover/tsconfig.json`
- `apps/business/src/app/app.routes.ts`
- `apps/business/src/app/pages/calendar-sync/booking-links.component.html`
- `apps/business/src/app/pages/calendar-sync/booking-links.component.ts`
- `apps/business/src/app/pages/calendar-sync/calendar-permission.guard.ts`
- `apps/business/src/app/pages/calendar-sync/calendar-sync.component.html`
- `apps/business/src/app/pages/calendar-sync/calendar-sync.component.spec.ts`
- `apps/business/src/app/pages/calendar-sync/calendar-sync.component.ts`
- `apps/business/src/app/pages/public-page/public-page.component.html`
- `apps/business/src/app/pages/staff/staff.component.html`
- `apps/business/tsconfig.json`
- `db/Adeni.Database/Security/calendar.sql`
- `db/Adeni.Database/booking/Tables/staff_members.sql`
- `db/Adeni.Database/calendar/Tables/booking_events.sql`
- `db/Adeni.Database/calendar/Tables/busy_blocks.sql`
- `db/Adeni.Database/calendar/Tables/conflicts.sql`
- `db/Adeni.Database/calendar/Tables/connections.sql`
- `db/Adeni.Database/calendar/Tables/oauth_attempts.sql`
- `db/Adeni.Database/calendar/Tables/selections.sql`
- `db/Adeni.Database/calendar/Tables/snapshots.sql`
- `db/Adeni.Database/calendar/Tables/staff_state.sql`
- `db/Adeni.Database/tenancy/Tables/business_locations.sql`
- `db/Adeni.Database/tenancy/Tables/public_booking_links.sql`
- `docs/specs/f4-calendar-book-with-adeni-closeout.md`
- `docs/specs/f4-calendar-book-with-adeni-plan.md`
- `packages/api-client/src/calendar/calendar-api.ts`
- `packages/api-client/src/calendar/calendar-operations.ts`
- `packages/api-client/src/discovery/discovery-api.ts`
- `packages/api-client/src/index.ts`
- `packages/shared/package.json`
- `packages/shared/src/booking-preselection.ts`
- `packages/shared/src/calendar-copy.ts`
- `packages/shared/src/calendar.test.ts`
- `packages/shared/src/calendar.ts`
- `packages/shared/src/i18n/errors.ts`
- `packages/shared/src/i18n/messages/en.ts`
- `packages/shared/src/i18n/messages/fr.ts`
- `packages/shared/src/index.ts`
- `src/Adeni.Api/Controllers/CalendarControllers.cs`
- `src/Adeni.Api/Extensions/HangfireExtensions.cs`
- `src/Adeni.Api/Program.cs`
- `src/Adeni.Application/Booking/IAvailabilityService.cs`
- `src/Adeni.Application/Booking/ISchedulingTimeZone.cs`
- `src/Adeni.Application/Calendar/ICalendarService.cs`
- `src/Adeni.Application/Calendar/IExternalCalendarProvider.cs`
- `src/Adeni.Application/Tenancy/IBookingLinkService.cs`
- `src/Adeni.Domain/Calendar/CalendarModels.cs`
- `src/Adeni.Domain/Common/ErrorCodes.cs`
- `src/Adeni.Domain/Tenancy/PublicBookingLink.cs`
- `src/Adeni.Infrastructure/Booking/AvailabilityService.cs`
- `src/Adeni.Infrastructure/Booking/BookingInternals.cs`
- `src/Adeni.Infrastructure/Booking/BookingPreselectionQuery.cs`
- `src/Adeni.Infrastructure/Booking/BookingService.cs`
- `src/Adeni.Infrastructure/Booking/BookingServiceCollectionExtensions.cs`
- `src/Adeni.Infrastructure/Booking/CalendarBookingQuery.cs`
- `src/Adeni.Infrastructure/Booking/SchedulingTimeZone.cs`
- `src/Adeni.Infrastructure/Booking/StaffService.cs`
- `src/Adeni.Infrastructure/Calendar/CalendarConnectionSession.cs`
- `src/Adeni.Infrastructure/Calendar/CalendarCredentialProtector.cs`
- `src/Adeni.Infrastructure/Calendar/CalendarOptions.cs`
- `src/Adeni.Infrastructure/Calendar/CalendarProviderHttp.cs`
- `src/Adeni.Infrastructure/Calendar/CalendarReconciliationJob.cs`
- `src/Adeni.Infrastructure/Calendar/CalendarService.cs`
- `src/Adeni.Infrastructure/Calendar/CalendarServiceCollectionExtensions.cs`
- `src/Adeni.Infrastructure/Calendar/ExternalCalendarAvailability.cs`
- `src/Adeni.Infrastructure/Calendar/GoogleCalendarProvider.cs`
- `src/Adeni.Infrastructure/Calendar/MicrosoftCalendarProvider.cs`
- `src/Adeni.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/Adeni.Infrastructure/Identity/BookingLinkAccessQuery.cs`
- `src/Adeni.Infrastructure/Identity/CalendarAccessQuery.cs`
- `src/Adeni.Infrastructure/Identity/IdentityServiceCollectionExtensions.cs`
- `src/Adeni.Infrastructure/Logging/CalendarOAuthLogFilter.cs`
- `src/Adeni.Infrastructure/Logging/PiiDestructuringPolicy.cs`
- `src/Adeni.Infrastructure/Persistence/AdeniDbContext.cs`
- `src/Adeni.Infrastructure/Persistence/CalendarModelConfiguration.cs`
- `src/Adeni.Infrastructure/Tenancy/BookingLinkService.cs`
- `src/Adeni.Infrastructure/Tenancy/TenancyServiceCollectionExtensions.cs`
- `tests/Adeni.Api.Tests/Integration/CalendarIntegrationTests.cs`
- `tests/Adeni.Infrastructure.Tests/Calendar/CalendarAvailabilityTests.cs`
- `tests/Adeni.Infrastructure.Tests/Calendar/CalendarLoggingTests.cs`
- `tests/Adeni.Infrastructure.Tests/Calendar/CalendarProviderTests.cs`
- `tests/Adeni.Infrastructure.Tests/Calendar/CalendarSafetyTests.cs`
- `tests/Adeni.Infrastructure.Tests/Calendar/CalendarSqlServerTests.cs`
- `tests/Adeni.Infrastructure.Tests/Calendar/CalendarTestSupport.cs`
