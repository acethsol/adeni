# Transactional outbox — deferred reliability design

**Status: design only; no table, worker, migration, or delivery behavior is implemented by the audit.** This is a SQL Server design inside the modular monolith, not a message-bus or microservice proposal.

## Observed failure window

`AdeniDbContext.SaveChangesAsync` enters the scoped after-commit queue, awaits EF's save, drains `IDomainEventCollector.TakeAll`, and awaits each event publication. `DomainEventDispatcher` fans handlers out with `Task.WhenAll`. In `finally`, the outermost queue exit flushes pending delegates. Nested saves defer flushing until save depth reaches zero.

Normally SQL Server commits the EF save before the handlers run. If a handler, Hangfire enqueue, or inline delivery then throws, persisted business data remains but the HTTP request can fail. Some parallel handlers may already have succeeded. The collector has already been cleared; the queue clears a batch before executing it, so a failed work item can also strand the rest of that batch. A process crash between persistence and enqueue loses in-memory work. Request cancellation propagates to post-save work. Hangfire retries jobs that were successfully enqueued, but cannot recover a job that was never enqueued. An outer explicit transaction, if introduced, would also need a real outer-commit boundary; a successful SaveChanges alone would not establish that.

`PostSaveFailureTests` characterizes persisted data plus partial queue execution after a handler failure. This is accepted as a **deferred audit-scope limitation**, not a production reliability sign-off. Retrying an entire mutation after such an error is unsafe without stable idempotency and reconciliation. Existing payment references, idempotency keys, notification deduplication and operator reconciliation mitigate selected paths, not every domain event.

## Persistence and transaction boundary

Propose SQL schema `reliability` with `OutboxMessage` and an optional per-consumer `OutboxDelivery` table. Add through the existing Database Project/SqlPackage process, not an unrelated storage engine.

`OutboxMessage`: stable `EventId` (uniqueidentifier PK), `EventType` (stable contract discriminator), `SchemaVersion`, optional `TenantId`, `AggregateType`/`AggregateId`, optional aggregate sequence, `OccurredAtUtc`, `CreatedAtUtc`, `CorrelationId`, `PayloadJson` (nvarchar(max)), state, attempt count, `NextAttemptAtUtc`, lease owner/expiry, completion timestamp and bounded/redacted failure summary. Index pending state/next-attempt/lease expiry and aggregate ordering keys. Add a unique producer/event identity constraint. `OutboxDelivery(EventId, ConsumerKey)` records completion/retries per handler when fan-out checkpoints are required.

Capture immutable event envelopes **before** the business save; save business mutations and outbox rows in the **same SQL transaction**. Never append outbox records in a second post-commit save. Preserve event IDs through execution-strategy retries, restore retained collector state on rollback, and avoid duplicate envelopes from nested saves. Coordinate external transactions at the outer unit-of-work boundary. An outbox insertion failure must roll back the business write. Do not defer synchronous validation, authorization, or financial invariants that belong in the originating transaction.

## Event contracts and privacy

Use an explicit allowlisted event-name/version registry and System.Text.Json contracts in Application/Domain; do not persist assembly-qualified .NET type names or use unrestricted polymorphic deserialization. Support versioned readers/upcasters, additive fields and rolling-deployment compatibility. Unknown event versions go to operator-visible quarantine, never silent discard.

Persist minimum immutable business facts and stable references, not access tokens, payment secrets, full message bodies or unnecessary customer PII. Where a financial snapshot is required, define its retention and access policy. Notification consumers must recheck applicable delivery/consent rules and distinguish snapshot data from values intentionally resolved at delivery time.

## Worker, leases and retries

Use a scoped dispatcher hosted within the API process or an existing Hangfire recurring job. Multiple instances claim work through a short atomic SQL lease (`UPDATE ... OUTPUT` with an appropriate `UPDLOCK`/`READPAST` strategy), verified against the deployed isolation level. Complete the claiming transaction before calling handlers or providers. Lease expiry makes crashed workers recoverable; use UTC database time and bounded batches. Never hold a SQL transaction across a network call.

Retry transient failures with exponential backoff and jitter (initial proposal: 30 seconds, capped at one hour, ten attempts). Make policy configurable by event/consumer. Retain per-consumer checkpoints so partial fan-out does not unnecessarily replay successful consumers. Run on worker cancellation tokens, not the original request token. Renew bounded leases for long work and handle loss of lease explicitly.

Classify permanent validation/unsupported-version failures as poison messages. Quarantine with event ID, consumer key, version, attempts and redacted error code. Provide audited replay/skip tooling with approval and a reason; retain the original event identity. Do not silently mark poison work delivered or purge unresolved commerce work.

## Idempotency and ordering

Delivery is **at least once**, never claimed exactly once. For database handlers, commit a unique `(EventId, ConsumerKey)` receipt with the handler's mutations. Preserve payment provider-reference and webhook-replay protections. For notifications, use stable event/channel/recipient dedupe identifiers and provider idempotency keys where the selected provider supports them. Provider success followed by a lost acknowledgement remains ambiguous; reconcile provider state before replay where possible. Twilio or another provider must not be assumed to provide generic idempotency support.

Only promise ordering per aggregate where business rules need it. Use an aggregate sequence/version and prevent later events overtaking a failed earlier event for that aggregate; unrelated aggregates continue. GUIDs and timestamps are not a reliable total order. Handle duplicate/stale events explicitly. Define whether poison events block an aggregate and how operators unblock it. Do not impose global ordering on analytics or independent notifications.

## Observability and retention

Measure pending count, oldest-event age, processing latency, retries, lease expiry/reclaims, dead letters and per-consumer success rate. Trace from originating correlation ID through event and job IDs. Log identifiers and error codes, not payloads. Alert on payment/booking backlog age, poison commerce events and worker silence; set final thresholds from staging load and recovery tests.

Proposed starting policy: batch-delete completed envelopes after 30 days and reviewed poison records after 90 days, subject to legal/privacy and incident holds. Keep authoritative ledger/audit records under their own retention policies; the outbox is not a financial ledger. Index cleanup queries, cap batches, monitor database growth and securely erase sensitive payloads when required.

## Migration order and rollout

1. Payments and other commerce events: `PaymentCompleted`, `PaymentFailed`, refunds and subscription billing side effects. First separate invariants that must remain transactional from safely eventual work.
2. Booking lifecycle and related notification enqueueing: confirmation, cancellation, waitlist and receipt/reminder follow-up. Avoid migrating just notification enqueue while leaving the triggering event crash window open.
3. Staff/customer messaging notifications and other externally visible delivery.
4. Retention/rewards when shipped: immutable reward/earning identities, unique earning/reversal keys and ordering before replay is enabled.
5. Analytics and lower-priority projections, which can often rebuild from authoritative data.

Deploy the additive SQL schema and compatible readers first. Feature-gate producers/consumers by event category. Disable the corresponding old in-process delivery path when activating durable delivery; do not dual-publish without a dedupe plan. Exercise real SQL Server contention, concurrent leases, crash-after-commit, crash-after-provider-success, retries, poison events, nested saves and rollback in staging. Run a reconciliation report against business records and provider activity before widening rollout.

Rollback stops new producers/consumers safely while retaining pending records for compatible workers; do not drop the schema or abandon queued commerce events. Keep versioned payload readers available through the rollback window. Production acceptance requires monitoring, replay/runbooks, bounded retention, reviewed ordering guarantees and real-provider recovery tests.
