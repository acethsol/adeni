# F4 — Calendar sync and Book-with-Adeni

**Status: approved and implemented; see [F4 closeout](f4-calendar-book-with-adeni-closeout.md) for validation and activation prerequisites.**

Baseline: `feature/squarespace-inspired-v1` at completed F3 `e0767963dd450c6ae670809afec2df49c3190b71`. No branch creation, protected-branch change, push, merge or deployment. Authority: [product direction](../product-direction.md), [architecture](../architecture.md), [F3 closeout](f3-gift-cards-pay-links-closeout.md).

## 1. Existing Booking/availability capabilities

Inspected AvailabilityService, BookingService and conflict/slot generation, StaffService/identity/invites, closed dates, quote/queue booking creators, location/slug resolution, scheduling time zones, Auth0/Key Vault/Data Protection, Hangfire, entitlement catalog, Discover business/booking components and SSR, and F1–F3 boundaries/tests.

| Existing capability | Reuse / finding |
|---|---|
| Availability | Business weekly hours, staff hours intersecting business hours, location closed dates, staff leave, service eligibility, guest/cart duration and 14-day slot-query windows. Listing and create-time checking share scheduling helpers. No external-busy source exists. |
| Capacity | Pending/Confirmed bookings block slots; assigned bookings occupy a practitioner, unassigned bookings consume anonymous roster capacity. Empty eligible roster currently means shared-calendar fallback. Filtering all practitioners out must never accidentally activate that fallback. |
| Creation | Customer identity, verified business, PublicPageShowBook, tenant/location checks, entitlement/capability/service validation, server prices and create-time availability under the existing distributed slot lock. Staff may remain unassigned; do not silently introduce automatic assignment. |
| Other writers | Quote acceptance creates a Pending booking without the same availability check; queue creates immediate floor bookings. F2 pins intake in all three paths. Do not assume all writers call normal scheduled-booking creation. |
| Changes | Confirmation/rejection/cancellation and floor/payment transitions exist. No booking reschedule operation was found. Do not add a reschedule product feature merely to test calendar updates. |
| Staff | Tenant roster, active flag, service links, optional branch, weekly hours and leave. StaffMember has no email/login identity. BusinessUser may link StaffMemberId; invite email is not proof of external calendar identity. |
| Location and time | Public business slugs identify active verified **locations**. Branch TimeZoneId is optional; scheduling falls back to configured market default, and an invalid zone can fall back to UTC. Local-to-UTC conversion does not explicitly resolve DST gaps/folds. Markets define Africa/Lagos and America/Toronto. |
| Public booking | `/businesses/{slug}`, existing booking rail/`#book`, public service/staff/slots APIs and authenticated normal booking/deposit flow. No general query preselection parser is present. |
| Public rendering | Business page uses PendingTasks and slug-bound TransferState for public profile/catalog/reviews, canonical business URL and browser-only extras. F2/F3 private routes are client-only/no-store/noindex. |
| Integration infrastructure | Existing HttpClient, JWT/OIDC-related packages, Azure Key Vault configuration, Hangfire SQL jobs and metadata audit. Data Protection exists for F1 cursors, but no configured durable distributed key ring or reusable provider OAuth/refresh-token store was found. F3 HMAC credentials are not token encryption. |

Two directly relevant correctness findings require characterization and narrow fixes before enabling F4 scheduling: the inspected staff/public eligibility queries do not consistently filter StaffMember.BusinessLocationId, and configured-zone/DST fallbacks can yield incorrect instants. Preserve valid existing bookings and unrelated floor behavior; do not broaden this into a scheduling rewrite. Existing slot-lock granularity is not proof against every overlapping start/service race; F4 must not claim to solve that inherited issue.

## 2. Proposed calendar module ownership

Add a focused **Calendar** bounded context (`Domain/Calendar`, `Application/Calendar`, `Infrastructure/Calendar`). It owns provider connections/selections, protected credentials, busy snapshots, sync state/conflicts and external write mappings. Booking owns scheduling, service/practitioner/location eligibility, accepted appointments and their state transitions.

Booking consumes a small Application port for practitioner busy/freshness decisions and a staging guard for snapshot concurrency. Calendar consumes a Booking-owned Application query for minimal booking facts/desired calendar projection, and Identity-owned access contracts. Implement ports in their owning modules; no controllers querying EF, no cross-module Infrastructure classes or generic integration repository/store.

Tenancy owns the stable public location-link identity; Booking validates service/practitioner preselection through an Application port. Discover remains the public booking application. Notifications, Intake and Finance retain ownership unchanged.

## 3. Provider abstraction

Propose `IExternalCalendarProvider` with provider identity/capabilities, authorization-code exchange/refresh/revocation support, calendar metadata listing, bounded busy reads, and owned-event upsert/delete/reconciliation. Separate provider credential access behind an internal protector/token service; provider secrets/tokens never cross public/Application DTOs intended for controllers. Booking sees normalized UTC intervals and freshness, not OAuth/HTTP/event DTOs.

Use typed HttpClient REST adapters initially, with existing platform JSON/OIDC/crypto support; no Google/Microsoft SDK dependency in Domain/Application. Only fixed, allowlisted provider hosts/paths; validate pagination links so provider-supplied URLs cannot cause SSRF or credential forwarding to another host. Microsoft Graph **v1.0**, not beta.

- Google: selected calendar IDs through [freebusy.query](https://developers.google.com/workspace/calendar/api/v3/reference/freebusy/query); minimal calendar-list metadata scope for selection. Let Google expand recurrences.
- Microsoft: `getSchedule` is mailbox-oriented and [does not support delegated personal accounts](https://learn.microsoft.com/en-us/graph/api/calendar-getschedule?view=graph-rest-1.0). It is not a universal selected-calendar API. Prefer [selected-calendar calendarView](https://learn.microsoft.com/en-us/graph/api/calendar-list-calendarview?view=graph-rest-1.0) with Calendars.ReadBasic, `$select` for necessary availability fields and provider expansion of instances/exceptions. Retain no subject/body/attendees/attachments/meeting URLs. Do not fetch arbitrary mailboxes from staff-supplied email.

Recommend one account connection per provider/practitioner, with up to five selected busy-source calendars and one optional distinct write target per connection as technical V1 bounds, not paid-plan quotas. No groups, resource-mailbox discovery, organization-wide application access or domain-wide delegation. Validate actual granted scopes and calendar ACLs before activating a selection.

## 4. OAuth/token security approach

Calendar consent is separate from Auth0 application login. Use authorization-code flow with PKCE S256, one-use high-entropy state, nonce where OIDC identity is used, fixed registered callback and validated issuer/audience/account identity. Never accept provider tokens or provider subject/account identity from Angular. Request offline access only for the background integration and extra write permission only when write-back is explicitly enabled.

Authenticated Owner/Manager initiates an attempt bound to tenant, target staff, actor, provider, requested scope and a short expiry (propose ten minutes). A top-level API launch step can establish an HttpOnly/Secure/SameSite=Lax correlation cookie before provider redirect, avoiding reliance on cross-site fetch setting a cookie. Callback consumes state/correlation once, checks PKCE/identity, and revalidates actor membership/role, staff activity and enabled provider before persistence. Reject account substitution/replay or stale authorization. Angular receives a sanitized completion status, never authorization code/access/refresh tokens. Do not trust an arbitrary returnUrl; return only to the fixed Portal integration screen.

Code/state query values are unavoidable at the provider callback; redact them in API, edge/proxy, App Insights, Sentry and HTTP client telemetry before enabling OAuth. Use no-store/no-referrer callback responses. Persist only a state hash; protect the PKCE verifier and any short-lived token exchange material. Expire/delete attempts promptly.

For token storage, propose a Calendar-only AES-GCM envelope using built-in .NET crypto: ciphertext, random nonce/tag and key version; AAD binds tenant/connection/provider. Versioned 256-bit encryption keys and OAuth client secrets come from existing Key Vault configuration. Do not reuse F3 HMAC keys, default cursor protection or a development fallback for real provider tokens. Preserve old versions while re-encrypting records; validate enabled-provider configuration at startup. Disabled integrations must not require optional provider settings.

SQL lease/fencing plus rowversion serializes refresh and disconnect; no transaction across HTTP. Persist rotated refresh/access token and expiry together. A crash after provider rotation but before persistence can still require reconnect; do not claim exactly-once OAuth refresh. Late responses cannot resurrect a disconnected generation.

Approved clarification: one connection per provider/practitioner, with independently authorized tenant-scoped grants/selections. No global provider-account exclusivity. Shared accounts never automatically share selections, credentials or access across practitioners/tenants; no email-based auto-link. Disconnect removes this connection locally; global grant revocation must not disrupt another explicitly authorized connection. Disconnect deletes local credentials/busy data, stops jobs and invalidates the generation. Revoke the application grant only through an appropriately scoped provider facility; do not revoke a user's unrelated Microsoft sessions. Where provider-side revocation is unavailable, instruct the account owner how to remove consent. Existing written events remain in the dedicated output calendar after disconnect; disclose manual cleanup rather than retain hidden tokens indefinitely.

## 5. External busy/free model and admission

Store normalized half-open UTC intervals `[start,end)` keyed by tenant/staff/calendar selection and snapshot generation, plus covered window, last successful sync and safe error/status metadata. Free is the complement **only inside a completely synchronized window**. Google busy-only snapshots need no event IDs. Microsoft reads may use event IDs transiently for page/dedupe handling; retain references only where reconciliation requires them, never event content.

Microsoft policy proposal: busy, tentative, out-of-office and unknown availability block; explicitly free/working-elsewhere does not. Canceled events do not block. Do not add time rounding that invents free gaps. Merge overlapping intervals for the practitioner; an external busy block follows that practitioner across their eligible business locations. Room capacity/travel time are out of scope.

Integrate the same decision into public slot generation and final normal booking admission, after existing hours/leave/eligibility. Selected staff must be free; “any practitioner” means at least one genuinely eligible available practitioner after accounting for existing assigned/unassigned bookings. Keep a distinct no-roster versus no-available-practitioner state; never fall back to a shared calendar after filtering a roster to zero.

Use cached SQL snapshots, no provider network call inside booking or its distributed lock. Propose polling every five minutes and a **15-minute maximum snapshot age**: within that age keep the last complete snapshot on a transient failure; beyond it, or without coverage/initial sync, exclude only the affected connected practitioner from new admission. Unconnected staff and already accepted appointments retain normal behavior. Manager disconnect explicitly returns to local-only scheduling, with audit/confirmation.

Propose a rolling one-day past/90-day future import window, chunked to bounded provider requests. Outside coverage, request bounded background expansion and return temporary unavailability; never silently treat it as free. Exact horizon/freshness/cost tradeoff requires review and quota testing, rather than a new global booking horizon limit.

Stage optimistic concurrency guards for the snapshot versions used during admission in the same EF booking save, following F2's Prepare/Stage pattern with Application DTOs, not exposed EF entities. Snapshot replacement and booking admission must contend on the same per-staff sync-generation row. SQL tests must show either admission sees the new snapshot or it rolls back/rechecks if publication wins. A provider event not yet fetched remains an unavoidable eventual-consistency window.

Quote acceptance and immediate queue paths need explicit handling: unassigned quotes do not identify a calendar to project; do not silently rewrite quote approval/capacity rules. Flag detected conflicts for accepted quotes/assigned floor bookings and preserve immediate walk-in semantics. No claim of calendar protection for an unassigned writer that bypasses normal scheduled admission.

## 6. Time-zone model

Reuse location/market time-zone policy for Adeni schedules. Store provider busy/event instants as UTC datetimeoffset, with provider/calendar zone ID metadata only where needed. Map IANA/Windows zones explicitly in adapters and validate selected locations/zones; no new server-local or silent UTC fallback for enabled integrations.

External explicit offsets identify real instants, including both fall-back occurrences. Date-only all-day boundaries use the source calendar's zone and exclusive end date, producing 23/24/25-hour days as appropriate. Microsoft requests use explicit UTC range offsets and normalized UTC responses. Reject/quarantine malformed or unsupported zone data without publishing a partially empty snapshot.

For Adeni local schedule generation, propose skipping invalid spring-forward wall times and consistently selecting the standard-time occurrence for an ambiguous fall-back wall time, once. Do not add a second ambiguous slot without a UI design. This is a narrow documented scheduling correction with characterization tests, not a change to historical stored instants. Test Toronto/Ottawa spring/fall, Lagos, endpoint touching and conversion round trips.

## 7. Optional booking write-back model

Include write-back as a separately enabled slice **after busy import works**, off by default, with explicit additional provider consent. Target a dedicated pre-existing calendar excluded from all busy-source selections for that practitioner. Otherwise the provider free/busy result can feed Adeni's own copied booking back as an external conflict. Reject that overlap; do not subtract arbitrary anonymous busy intervals to guess event ownership.

Only persisted Confirmed bookings assigned to a staff member with write-back enabled are created externally. Preserve the copy through CheckedIn/Completed/Paid; cancel/remove it for Cancelled/Rejected or an assignment change as appropriate. Pending or unassigned bookings are not fabricated into practitioner calendars. No new rescheduling UI/API; projection reconciliation notices legitimate time changes if/when such a source operation exists. Test changed source snapshots directly.

Write only a generic “Adeni appointment”, start/end and an opaque ownership marker. No customer identity, service/treatment names, intake answers, financial details, notes, attendees, invitations or conferencing. Existing Adeni notification behavior remains unchanged. Do not use an external event's edits to change an Adeni booking.

## 8. Idempotency and reconciliation

Persist a unique mapping per tenant/booking/connection/target, stable operation identity/ownership marker, provider event ID, desired/applied fingerprint, state, last attempt/result, ETag/version where supported, and rowversion/lease. The source booking is authoritative; always re-read the latest desired state before retry, including cancellation and reassignment.

Google supports a [caller-specified event ID](https://developers.google.com/workspace/calendar/api/v3/reference/events/insert); derive a stable valid ID from the mapping and verify ownership on duplicate/conflict. Microsoft supports [transactionId and event extensions](https://learn.microsoft.com/en-us/graph/api/user-post-events?view=graph-rest-1.0); use a stable create transaction ID plus a recoverable ownership marker/mapping, not an assumption of indefinite provider deduplication. An ambiguous create is reconciled before another POST. Updates use conditional versions where practical; already-missing owned deletions succeed. Never update/delete foreign events from a supplied external ID.

Use a durable desired-projection table and periodic Booking Application source reconciliation; event handlers may enqueue an acceleration hint but are not the only discovery mechanism. Poll upcoming eligible bookings plus all existing mappings needing deletion/update, including cancellations outside a moving window. Full bounded sweeps and overlapping scans prevent lost UpdatedAt/cursor boundaries. This is integration projection state, not a generic transactional outbox rollout. No exactly-once external delivery claim.

## 9. Failure/conflict handling

- Revoked/expired unrecoverable grant: ReconnectRequired; no repeated refresh storm; stale policy excludes affected connected practitioner after the approved freshness window.
- 429: honor Retry-After, bounded exponential backoff/jitter and provider/connection request budgets. 5xx/timeouts: retain prior complete snapshot, retry; do not replace it with free time.
- Partial pages/calendar errors: publish only a complete validated source/window generation; advance coverage/cursor atomically. If a configured source fails, combined availability is not declared complete.
- New external busy overlapping an accepted appointment: keep the appointment, upsert a deduplicated metadata-only CalendarConflict and show it to authorized operators. For unassigned capacity, report a capacity risk rather than pretending a particular practitioner was assigned. Resolve by changing the external event or normal Adeni operations; acknowledgment alone must not erase a real busy interval.
- Existing provider-written event removed/edited: flag drift; reconcile only the owned projection from Adeni's latest source state. No external-to-Adeni edits.

Conflict/sync metadata contains identifiers, interval and safe category, not meeting text. Existing booked appointments remain valid during an outage. Repeated provider failure/poison writes remain visible with an audited manual retry/reconnect action; no silent success.

## 10. Permissions recommendation

Use explicit persisted Owner/Manager role plus existing Staff permission to manage organization calendar integrations, restricted to authorized tenant/staff/location. Use an Identity Application port; never unknown-role-to-Owner normalization. Actual provider account owner must grant consent; staff email/invite data does not authorize it.

**Defer practitioner self-management** despite existing StaffSelf/BookingsSelf: there is no current external-integration policy. A later approved self-flow must require an authenticated BusinessUser.StaffMemberId match, no arbitrary staff ID. Calendar busy internals/tokens are not public or general customer-profile data. Existing staff-calendar read scopes remain intact; show only a generic external-busy indicator where authorized.

Book-with-Adeni generation uses existing PublicPage permission and valid location scope; ordinary public visitors need no special permission to resolve the link. Normal booking authentication/authorization remains unchanged. No new permission keys unless implementation proves the current policy cannot represent this scope, then report before adding them.

## 11. Entitlements recommendation

Inspected Starter/Professional/Business JSON and runtime TenantEntitlements/effective SKU model: no calendar-sync key exists. PublicProfile/UnlimitedBookings are universal catalog fields; OnlinePayments/Deposits are universal runtime compatibility flags. Preserve backend IEntitlementsService/TenantBillingPolicy authority.

Recommend Book-with-Adeni links/buttons initially universal wherever normal public booking is allowed. Recommend calendar sync initially ungated during this integration rollout, with technical provider bounds and explicit consent/readiness, rather than misusing AdvancedAnalytics/AdvancedPermissions as a hidden gate. Professional/Business gating is a reasonable later commercial decision requiring catalog/TS/.NET parity and backend enforcement together. No entitlement behavior change before approval; no arbitrary plan quantity limits.

## 12. SQL/DACPAC changes

Propose explicit `calendar` tables: connections; protected connection credentials; selected calendars; short-lived OAuth attempts; staff sync state/snapshot generations and busy blocks; write-back mappings/desired state; conflict records. A separate table is not required for a generic job queue or event JSON store.

Composite tenant/staff/connection/selection FKs, tenant/practitioner/provider connection uniqueness, unique external mapping/ownership keys, valid interval constraints, one write target per connection, state/generation rowversions, bounded lease indexes and due-work/interval indexes. Treat external IDs as opaque case-sensitive values; use fixed-size hashes for long unique keys and retain the protected/original reference only where required. Do not assume all provider IDs fit GUIDs or SQL's short index-key limits.

Add tenant/ID alternate keys to existing staff/booking/location tables only where needed for FK authority. Booking facts are referenced, not copied into another booking aggregate. All changes through Database Project/SqlPackage and EF model parity; no EF migrations.

For stable links, add Tenancy-owned `public_booking_links` with immutable unique public slug, tenant/location FK and timestamps/status. This is a public permalink registry, not a secret/public payment-token table. It is separate from mutable location slugs, as described below.

## 13. Background-job model

Reuse Hangfire: dispatcher every five minutes claims bounded due connection/window work using SQL leases; per-connection worker refreshes, reads/stages/publishes snapshots and detects conflicts. A separate bounded projection worker reconciles desired booking events. Fast-path enqueue is optional; durable state/full reconciliation is the recovery path after process crashes or post-save handler failure.

No SQL transaction during provider HTTP, no access/refresh tokens in Hangfire arguments or serialized retry exceptions. Use IDs/generations only. Enforce timeouts, response/page/window limits and cancellation. Fencing rejects stale workers after disconnect or lease loss. Initial sync is not active coverage until complete. Expose last success, coverage, stale age, retry status and conflict/write backlog. Alert on worker silence and oldest unresolved work.

Prune superseded snapshots/expired OAuth attempts and busy data outside the operational window; retain minimum conflict/mapping history needed for ongoing reconciliation. Set short operational retention in deployment policy, separate from F2 sensitive-response retention. Do not delete a pending write/delete mapping merely because its booking aged out of the fetch window.

## 14. Public Book-with-Adeni URL model

Recommend one stable, location-scoped utility URL:

`https://<discover-origin>/book/<public-booking-slug>`

Seed its immutable public slug from the location's current existing slug on first explicit publication. The registry keeps it stable when the location's **profile slug changes**; current BusinessLocationService allows such changes and has no alias history. The separate `/book` namespace prevents an old link being reassigned merely because another location later takes an old profile slug. Publish one link per location idempotently; a general-business action defaults to its primary location and says so. No fictitious tenant-wide business slug is assumed.

Resolve only a currently active location belonging to a verified tenant with normal booking visibility, then server-redirect to its current `/businesses/{locationSlug}?...#book`. Do not silently substitute another branch for a retired/invalid location. The alias stays noindex with canonical destination supplied by the ordinary business page; no second booking application or duplicate SEO landing page. Existing direct business URLs remain valid.

This registry/one utility route is the small addition needed for a genuinely stable permalink. A direct current-slug `#book` URL is simpler but would break on rename; that alternative is intentionally not called permanently stable.

## 15. Preselection/deep-link model

Only optional `service=<existing-public-service-id>` and `practitioner=<existing-public-staff-id>`; location is already fixed by the permalink. These GUIDs are existing public catalog selectors, not authorization, while the primary experience remains a readable slug. Do not add new service/practitioner slug schemes or accept tenant/location/price/currency/rule/entitlement overrides.

A bounded Booking Application validator resolves active same-tenant/same-location eligible service/practitioner, rejects add-on-only cart roots and unsupported combinations using existing workflow rules. Reject malformed, duplicate or unknown parameters at the utility resolver with a safe public validation response; invalid slugs/inactive/unverified locations are not-found. Do not accept an arbitrary redirect URL or include private data in the destination.

After normal public SSR/hydration, the business component applies validated selection through existing booking-panel actions, once, without creating a booking or skipping user confirmation. Direct business-page links receive the same safe parser/server validation for recognized preselection, rather than treating URL values as trusted state. Preserve selection across login via a sanitized same-origin path only; no credential or price in login return state. Changes in slug/query abort stale catalog/staff/slot requests. Selection cannot override a later backend availability/booking check.

## 16. Embed/link recommendation

**Link/button only in V1.** A minimal generated HTML anchor has a server-approved HTTPS href, correct attribute/URL escaping and fixed “Book with Adeni” text. Optional target=_blank must include noopener/noreferrer. No script, arbitrary HTML/CSS, theme customization or user-supplied attribute template.

Defer iframe/widget: current API CSP has frame-ancestors none/X-Frame-Options DENY, while Discover hosting policy needs its own assessment. Third-party cookies/Auth0, cross-origin payment redirects, mobile sizing, accessibility and third-party site restrictions add material complexity. Do not weaken security headers to make an iframe work. CMS/social sites can use the ordinary link without migrating their website.

## 17. SSR/SEO implications

Implement `/book` resolution as a server-side utility redirect (Node/server handler or equivalent hosting route), noindex/no-store and with bounded validated query handling. Public business destination remains existing server-rendered content with its current canonical URL, PendingTasks, slug-bound TransferState, market and hydration behavior. Query preselection must not change SEO metadata or place private sync state in TransferState. No new sitemap aliases or indexable variants.

Use location/market data from the resolved public profile, never the browser's remembered market to change tenant, branch, currency or time zone. OAuth callback/Portal integration state is private/no-store/noindex, using F2/F3 private-route practices. Link publication/rename/retirement needs redirect and cache-invalidation tests. Do not introduce a shared cache containing tokens or per-customer availability internals.

## 18. F2 integration

Every Book-with-Adeni submission uses normal BookingService and F2 intake Prepare/Stage, retaining the applicable published version. Preselection does not carry or change intake requirements. Existing authenticated customer completion, immutable versions, final-submission policy, permissions and retention/holds remain untouched. No intake data in external events, conflicts, URLs or telemetry.

## 19. F3 interaction boundaries

Normal existing booking deposits/payment rules remain authoritative. No implicit custom-charge Pay Link/gift tender and no F3 live enablement. Calendar write-back is driven by accepted booking state, not browser payment success. Existing Finance deposit-confirmation events are reconciled through the Booking projection, without Calendar importing Finance Infrastructure or changing settlement behavior. F3's live-payment blockers remain separate launch prerequisites.

## 20. Proposed additive APIs

| Surface | Proposed purpose |
|---|---|
| Tenant `/api/v1/tenant/calendar/connections` | List sanitized connections; initiate authorization for validated staff/provider. |
| Tenant connection `/{id}/calendars`, `/selection`, `/sync`, `/disconnect` | Provider-proven metadata, select busy/write calendars, bounded manual sync, explicit disconnect. |
| Tenant `/api/v1/tenant/calendar/conflicts` | Permission-scoped metadata/status and audited acknowledgment; no meeting content. |
| OAuth `/api/v1/calendar/oauth/...` | Fixed top-level launch/callback, one-use state/cookie proof; no JWT assumption at provider return. |
| Tenant `/api/v1/tenant/booking-links` | Idempotent publication and safe URL/snippet generation for a permitted location/selection. |
| Public `/api/v1/public/booking-links/{slug}` | Verified resolution and preselection validation, public rate limiting, no private IDs/tokens beyond existing catalog selections. |
| Existing public availability/booking APIs | Same HTTP contracts, authoritative external-busy filtering and existing mutation validation. |

Keep Result/stable error conventions, versioned routes, shared Zod/localized errors and compatible api-client facade. No existing endpoint removal. Internal Application ports can gain explicit decision/snapshot DTOs without changing public booking responses. Management statuses expose health/coverage, not credentials, provider event descriptions or arbitrary raw errors.

## 21. Portal UI

A focused staff/integrations panel in existing Portal: select practitioner/provider; explicit account-consent flow; selected busy calendars; optional separate output target; sync health/last success/coverage, reconnect/disconnect and a small conflict list linking to normal authorized bookings. Distinguish not connected, syncing, healthy, stale, needs reconnect and conflict. No external calendar event content.

Public Page/location settings gains Book-with-Adeni: select authorized location, optional service/practitioner, preview destination and copy link/HTML. Fixed branding, no page designer or arbitrary snippet editor. Use standalone Angular, Carbon/Adeni controls, Signals for local state and lifecycle-safe RxJS/cancellation. No new global store or duplicate realtime subscription. Runtime readiness, permissions and entitlements are separate UI concepts and server-authoritative.

## 22. Discover/public UI

Reuse the existing business page and booking panel. Apply safe service/practitioner context after public data is ready; preserve normal cart/guest/terms/auth/slot/confirmation/deposit behavior. An invalid context gives an explicit safe error/reset path, never silently books a different service/practitioner/branch. Public slot responses reveal availability only, not whether a practitioner has a private meeting or token failure.

No standalone booking app, iframe, calendar UI, write-back controls or F1/F2/F3 data in public pages. Existing Flutter future client can use the same normal booking API and public-link resolver; mobile implementation is deferred.

## 23. Test strategy

- Calendar Application/Domain: normalized interval union, endpoint boundaries, active/disabled/source completeness, provider account binding, strict role/self-scope denial, no-roster versus roster-filtered-empty, any/selected practitioner capacity, no private event content in contracts/log/error representations.
- OAuth/security: state/correlation/PKCE/nonce/identity validation, missing/duplicate/expired callback, login CSRF/account substitution, tenant/staff mismatch, role revoked during consent, encryption/AAD/key rotation, no real-token fallback, refresh versus disconnect/lease loss, no secrets in API/Hangfire/telemetry.
- Provider HTTP contracts: exact scopes/endpoints/field selection, Google calendar errors, Microsoft personal/work accounts, expanded recurring exceptions, cancellations/all-day/free/tentative semantics, pagination limits/unsafe next links, 401/invalid_grant/429/Retry-After/timeouts and partial-snapshot preservation. Sandbox end-to-end consent/refresh/read/write/revoke for both providers before launch.
- Time: Ottawa spring gap/fall fold and 23/25-hour all-day events; Lagos no DST; UTC offsets and Windows/IANA equivalents; unsupported zone fails safely; stored historical instants unchanged.
- Booking: slot list and admission agree, external busy blocks selected staff, other free staff remain bookable, stale only affects connected staff, source publication racing admission, late external conflict never cancels, quote/queue writers have explicit characterized behavior, all normal booking/idempotency/permissions/intake/deposit regressions.
- Write-back: duplicate create/lost response, mapping uniqueness, stable IDs/transaction IDs/ownership verification, latest-state update, changed time/assignment snapshot, cancellation before/after create, repeat/missing delete, provider-side drift, separate input/output calendar and no feedback conflicts, process crash before enqueue repaired by polling.
- **Real SQL Server**: tenant/staff/selection/booking FKs, duplicate account/mapping/callback constraints, lease fencing and rotated-token CAS, snapshot/cursor races and atomic replacement rollback, booking admission guard versus snapshot publish, conflict dedupe, disconnect racing worker. InMemory cannot establish these properties.
- Public links: stable slug/current profile rename, duplicate publish, retired/unverified/unknown location, cross-tenant/location or inactive staff/service, add-on roots, invalid/duplicate/unknown query keys, no price/currency override, encoded anchor XSS/open redirect rejection, safe auth return context and no automatic submission.
- Angular/SSR: existing public profile SSR content/canonical/PendingTasks/TransferState/hydration/market remains correct; utility redirect real HTTP status/headers; apply preselection once; abort late slug/query/tenant changes; no duplicated SignalR subscriptions; F2 pins and F3 private routes remain unchanged. Portal copy/snippet/permissions/health/conflict tests and shared API import/contract parity.

Planned validation after approval: `dotnet build Adeni.slnx -c Release`; `dotnet test Adeni.slnx -c Release`; isolated DACPAC publish then `ADENI_SQLSERVER_TESTS=1 dotnet test tests/Adeni.Infrastructure.Tests/Adeni.Infrastructure.Tests.csproj -c Release --filter 'Category=SqlServer'` with an external test connection; shared/Portal/Discover tests; `npm run typecheck`; all three production builds. Use existing scripts and CI Node version, restore with npm ci if needed. Report the inherited stylesheet/initial-bundle blockers and any new growth precisely, never raise budgets to conceal them.

## 24. Deployment/configuration requirements

Separate enabled-provider options for Google and Microsoft: registered server OAuth clients, consent screen/publishing/verification as required by actual scopes/account audience, fixed public HTTPS callback/Portal return origins, exact least-privilege scopes, provider account types and optional write consent. Key Vault-managed client secrets and versioned Calendar encryption keys. No settings required for a disabled optional integration; enabled integration missing its keys/client/callback fails clearly.

Configure trusted forwarded proxy/scheme handling, OAuth launch/correlation-cookie behavior across deployed domains, CORS, callback/URL telemetry redaction and provider host allowlists. Validate these against real Azure/Front Door infrastructure, not just local callbacks. Do not silently enable real integrations with test credentials/fallback protection.

Publish additive DACPAC first, deploy all scheduling writers/availability consumers, then enable provider per controlled rollout after real SQL/sandbox validation. Do not activate sync using mixed old/new availability writers. Initial complete sync precedes booking enforcement. Rollback disables writes/sync deliberately and exposes local-only scheduling; never silently drop external blocking policy while calling it synchronized. Preserve mapping/credential keys until disconnect/migration is safely handled. Existing Angular build blockers and F3 live-commerce prerequisites are still unresolved.

## 25. Risks/open decisions for review

1. Approved: conservative stale policy (15 minutes), five-minute polling and 90-day initial cache with bounded expansion (not a booking horizon); disconnected/unconnected behavior differs deliberately from stale connected scheduling. Provider quota/load measurement may require operational tuning.
2. Approved: tenant-scoped connections, one account per provider/practitioner; no global account exclusivity. Owner/Manager-only management and technical selection bounds remain. No automatic cross-tenant grant sharing; self-management is deferred.
3. Approve optional write-back with a dedicated output calendar, generic content and no attendee notifications; output events remain after disconnect for manual cleanup.
4. Approve the narrow branch-eligibility and DST corrections in the touched shared scheduling path; do not conflate them with a full capacity/locking refactor. Existing different-start/service slot-lock races and quote/walk-in semantics remain explicit limits.
5. Bookings without assigned practitioners cannot be projected or attributed to one calendar. Conflict reporting for anonymous capacity must be honest.
6. Approve stable `/book` permalink registry versus accepting mutable profile URLs; existing editable location slugs alone do not provide durable renamed links.
7. Provider consent verification, Outlook account/ACL differences, encrypted-token key lifecycle and rare refresh-rotation crash recovery are deployment dependencies, not completed capabilities.
8. Polling inevitably leaves an external-change window. No promise of global real-time/no-conflict scheduling or exactly-once external delivery; the full transactional outbox stays deferred.
9. Existing bundle budgets are already failing; Portal initial bundle is near its threshold. Keep new Portal pages/provider clients lazy and avoid eager shared-schema growth where practical.

## 26. Explicitly deferred functionality

Website builder/CMS/designer/themes/white-labeling, iframe/JS widget/plugins/browser extension, generic calendar product/marketplace, external CRM/marketing attribution/campaign automation, customer calendar sync, full two-way edits, recurrence engine, application-wide mailbox delegation, automatic practitioner assignment, new rescheduling feature, attendee invitations/conferencing, clinical/intake content, practitioner self-management, multi-business shared OAuth grant semantics, Retention/F5, WhatsApp API, transactional-outbox rollout and live F3 payment enablement.

## 27. Recommended implementation sequence

1. Review this plan, account/permission scope, stale/horizon policy, optional write-back and permalink/DST/branch decisions. **Approved before implementation.**
2. Characterize existing scheduling/branch/DST/any-staff behavior; add Application ports/contracts and targeted regression tests. Confirm the shared admission/snapshot concurrency seam before coding provider sync.
3. Add SQL models/keys/leases, credential encryption and OAuth state flow; verify real SQL/security tests and enabled-provider validation.
4. Implement Google busy adapter and Microsoft selected-calendar adapter, complete-snapshot publication, stale admission policy and conflict reporting; sandbox consent/refresh plus real SQL race tests.
5. Add bounded Hangfire reconciliation and focused Portal connection/health UI; enable import only after initial successful coverage and operational checks.
6. Add the separately enabled write-back projection/mapping/worker with duplicate-create/delete/drift/failure tests; no outbox or rescheduling rollout.
7. Add immutable location permalink, server redirect, validated preselection and minimal Portal link/snippet UI, reusing normal Discover SSR/booking. Prove F2 assignment and existing payment rules remain unchanged.
8. Run backend/frontend/SQL/provider-sandbox/HTTP SSR checks, document exact results and remaining blockers, commit reviewable slices and stop for F4 review. Do not start F5.

Planning validation: repository/source/branch checks and official provider-documentation review only. No application code/schema/configuration was changed and no application builds/tests or OAuth/provider operations ran during this planning pass.
