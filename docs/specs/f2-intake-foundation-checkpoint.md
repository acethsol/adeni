# F2 foundation checkpoint

F2 is **not complete**. The approved plan is [here](./f2-intake-consultation-forms-plan.md). This checkpoint builds on F1 commit `3fd36d6` on `feature/squarespace-inspired-v1`.

Implemented independent foundation:

- Nine relational Intake entities/tables, SQL schema, tenant/version-aware foreign keys, typed answer columns, uniqueness and basic CHECK constraints. Additive tenant/ID unique indexes on booking/service tables support tenant-safe references. SQL/DACPAC remains authority; no EF migrations.
- EF mappings and tenant filters in a partial DbContext configuration. No collection API or runtime service registration yet.
- Bounded central field-definition/answer validation for seven field types, required/optional values, typed Boolean/date/choice answers, Unicode, duplicate/unknown field/option rejection and errors without echoed content.
- Internal template service supports create/read/save draft/publish/clone. Published fields are not edited by draft mutations; cloning allocates new field/option identities. Expected rowversion checks and SQL uniqueness conflict handling are included.
- Identity-owned management access port checks same-tenant explicit stored owner/manager roles. Unknown-role normalization cannot grant sensitive management authority. Controllers do not query EF; no new controllers exist yet.
- Nineteen new executed tests: ten validation cases and nine management/versioning/access cases. Application validation and InMemory tests do not establish live SQL constraint/concurrency correctness.

Validation:

- `dotnet build Adeni.slnx -c Release`: passed, including DACPAC, zero warnings/errors.
- `dotnet test Adeni.slnx -c Release`: 625 passed (Domain 26, Application 60, Infrastructure 315, API 224); three inherited opt-in SQL tests skipped.
- Targeted `dotnet test tests/Adeni.Infrastructure.Tests/Adeni.Infrastructure.Tests.csproj -c Release --no-build --filter FullyQualifiedName~IntakeTemplateServiceTests`: nine passed.
- `git diff --check`: passed. No frontend changes; frontend suites were not rerun. The inherited production stylesheet/bundle failures remain.

Execution uses `/workspace/dotnet/dotnet` with the existing DOTNET_CLI_HOME/NUGET_PACKAGES/TMPDIR workspace directories. Sandbox named-pipe denial required an execution exception for .NET checks. No database was published, migration applied, provider called or branch promoted.

Remaining before F2 can be called complete:

1. Confirm response retention after appointments and legal-hold behavior. The approved plan section 17 item 6 expressly requires this before real collection; no duration or exception has been supplied. A question is pending with the user. Do not invent a legal policy.
2. Finish template lifecycle, audit integration, service binding and same-save assignment hooks for normal booking, quotes and queue conversion. Internal services are deliberately unregistered until their complete audited request flow is ready.
3. Implement owned-customer final submission/idempotency, status/review access, privacy export/purge and configured retention lifecycle.
4. Add additive APIs, shared schemas/client methods and localized errors, Portal editor/status/review and guarded private Discover completion with SSR/hydration coverage. Preserve F1 contracts.
5. Add real SQL publish/constraint/concurrency/rollback tests, endpoint security and frontend tests; rerun complete checks. Draft ordering is normalized by the module; mutable SortOrder is intentionally not an EF alternate key.

No response collection, service link activation, frontend behavior, booking state transition, entitlement catalog or public API contract changes are enabled by this checkpoint. The new tables are a schema proposal until explicitly published through the normal deployment process. Protected branch refs remain unchanged; no push or merge is performed.
