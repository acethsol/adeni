# F3 — Business gift cards and Pay Links / Quick Pay

**Status: approved; Development/Testing Stub implementation completed. Live merchant settlement remains deferred and server-blocked. See [closeout](f3-gift-cards-pay-links-closeout.md).**

Planning baseline: `feature/squarespace-inspired-v1`, completed F2 commit `824e42c9b43e44717cfab9b086a3a81f6dbe0391`. No new branch, protected-branch changes, schema deployment, or F3 code changes during this review.

Authority: [product direction](../product-direction.md), [architecture](../architecture.md), [Finance boundaries](finance-module-boundaries.md). This explicitly requested feature adds a merchant-specific gift liability ledger; it does not relax the existing prohibition on an Adeni wallet, custody, cross-business balances, or lending.

## 1. Existing Finance/payment capabilities and findings

Repository inspection covered Finance Domain/Application/Infrastructure, `FinanceController`, Booking collection and settlement, Stripe/Paystack/MarketDefault adapters, SQL definitions, Reporting, privacy administration, F1 commerce/profile ports, F2 boundaries, shared contracts, API client, Portal Finance, Discover routes and existing payment tests.

| Area | Reusable implementation | Important limitation |
|---|---|---|
| Payment lifecycle | `IPaymentOrchestrator`, `PaymentIntentRecord`, Pending/Completed/Failed/Cancelled/RefundPending/Refunded, provider signatures, receipts, audit, realtime | Completion uses a status check without SQL rowversion; concurrent webhook delivery needs stronger protection for new monetary effects. |
| Existing links | `POST /api/v1/finance/links`, `CreatePaymentLinkAsync`, Portal `/finance` | Creates an immediate provider checkout. No durable payment-request aggregate, public capability token, expiry/cancellation lifecycle, or creation idempotency. Keep its contract compatible. |
| Providers | `IPaymentProvider`, registry, MarketDefault; Paystack Nigeria, Stripe Canada/US | Actual merchant settlement is missing. Stripe initializes a platform-only Session; Paystack payload has no tenant subaccount/split. Platform fee is metadata, not proof of settlement. |
| Provider retries | Provider references and unique SQL index; optional initialize idempotency key | Key lookup is global and does not compare payload/tenant; provider initialize occurs before DB insert; Stripe sends no provider Idempotency-Key; non-stub checkout URLs are not retained for replay. |
| Webhooks | Signature validation precedes JSON parsing outside relaxed development configurations | Orchestrator does not compare payload amount/currency with intent. Stripe treats `checkout.session.completed` as success without checking paid state; delayed methods require care. Session/payment-intent/refund reference correlation is incomplete. |
| Booking | Deposits, collect-balance after check-in/completion, external till, existing PaymentCompleted handling | Remaining amount is booking lines/fallback price minus Completed intents; no durable reservation against competing checkout/till flows. Do not extrapolate new guest/tip/tax semantics. |
| Refunds | Provider refund interface, refund API and RefundInitiated event | Requested refund is immediately recorded Refunded even when provider returns pending; partial amount is not tracked as remaining principal. Booking refund handler cancels booking. Not a safe gift-card refund design. |
| Documents/reporting | `IBillingDocumentService`, unique receipt per intent, public document tokens; Reporting renderers, safe CSV, ClosedXML, QuestPDF | Existing public document DTO contains identifiers/PII; do not reuse it as the anonymous Pay Link DTO. Gift liabilities must not be counted twice as service revenue. |
| Access | `portal.finance`; Owner/Manager/Accountant roles; tenant middleware and payment-access Application ports | Unknown role normalization becomes Owner. Existing broad payment membership access must not authorize new secret disclosure or monetary mutations. |
| Customer/privacy | F1 `ICustomerCommerceQuery`, profile source permissions and location/history caps; existing admin export/erasure workflow | F1 recorded online payments are booking-linked; standalone links lack trustworthy customer attribution. Recipient email alone never proves account ownership. |
| Notifications/share | Receipt notification dispatch/deduplication; Portal copy/share and `wa.me` link | Never send gift codes through ordinary logged notification templates. No new WhatsApp integration. |
| Reliability | Existing Hangfire and [outbox design](../transactional-outbox-design.md) | SaveChanges publishes events after persistence; event failure can leave committed data without effects. Financial issuance cannot rely only on that handler. |

Physical schema remains `payments`, despite logical Finance ownership. Current money columns are `decimal(12,2)`; both providers use integer hundredths. There are no existing gift-card entities/ledger or merchant settlement-account records. [Sprint 34](sprint-34-payment-settlement.md) and [settlement design](../payment-provider-settlement.md) explicitly describe settlement as future work.

## 2. Proposed module ownership

Finance owns gift-card purchases, cards, ledger, fixed payment requests, settlement and provider integration. Proposed focused Application ports: `IGiftCardService`, `IPayLinkService`, `IFinanceCommerceAccessQuery`, `IFinanceCustomerQuery`, and `ICustomerGiftCardQuery`. Infrastructure implements them within the owner module; Identity/customer modules implement consuming-module access/identity ports. Controllers bind input and map Result/errors only.

Reporting owns serialization/layout, consuming a Finance-owned gift-card operational-report port. Tenancy/F1 consumes Finance metadata ports. No Intake reference or response data flows into Finance. No generic repository, cross-module Infrastructure references, new deployment unit, state framework, or event bus.

**Recommended V1 boundary:** custom fixed charges and gift-card purchase requests only. Defer booking deposits/balances, invoice collection links and booking gift settlement until shared booking-charge reservation/settlement semantics can prevent competing legacy checkouts. This is an explicit scope recommendation for review, not a silent omission.

## 3. Gift-card data and ledger model

Proposed relational entities in `payments`:

- `GiftCard`: random ID, TenantId, purchase-request linkage, original amount, remaining amount, explicit currency, PendingPayment/Active/Exhausted/Suspended/Voided status, purchaser customer reference where authenticated, optional protected recipient name/email, issued timestamp, created/updated timestamps, rowversion, code hash and derivation key version/nonce. Pending cards have zero spendable value.
- `GiftCardTransaction`: ID, TenantId, GiftCardId, per-card sequence, Issue/Redeem/RedemptionReversal/Void discriminator, signed delta, resulting balance, settlement/purchase reference, optional original transaction reference, actor/system identity, occurred timestamp, operation/idempotency reference. Immutable after insert.
- `CommerceSettlement`: tenant/request, tender Provider or GiftCard, amount/currency, source payment intent or gift redemption, settled timestamp. Exactly one successful settlement per request. A gift redemption is not a fabricated successful provider intent.

Implement only transaction kinds with approved operations: Issue, Redeem, and tightly constrained corrective RedemptionReversal. No arbitrary adjustment, top-up, complimentary/manual issue, cash-out, cross-card transfer or unrestricted void endpoint. Voided status is reserved for an explicitly reconciled failed/returned purchase, never automatic balance destruction.

All value changes append a ledger row and update the cached balance in the same transaction. Balance is derivable from deltas and reconciled against the cache. No negative balance, no currency conversion; the ledger is merchant liability/operational history, not a general accounting subsystem. Store no card code/token/PII in ledger descriptions.

## 4. Gift issuance flow

An explicit Owner/Manager creates a fixed gift purchase request; it has a pending gift-card record with no value. Customer signs into the existing customer architecture for purchase attribution and subsequent code access. Normal custom charges remain anonymous-token flows. Do not infer purchaser identity from an email.

Checkout uses the existing Finance orchestrator/providers after settlement-readiness checks. Authoritative **paid** provider completion atomically marks the intent Completed, settles the request, activates the gift card and inserts its single Issue ledger entry. Unique purchase/issue constraints make sequential and concurrent duplicate callbacks harmless.

Implement this invariant within the originating Finance transaction, before external post-save event handlers. Refactor only the required Finance completion seam; retain existing PaymentCompleted events/receipt behavior for legacy paths. A crash/handler failure after commit must not strand paid gift value. Hangfire reconciliation scans durable pending/ambiguous attempts and committed-but-unfulfilled receipts; it is not a substitute for atomic issue. No broad transactional-outbox implementation in F3.

Code disclosure is a separate audited, no-store customer-owned read after activation. Recipient is optional contact metadata; purchaser manually shares the code. No automatic recipient delivery, public purchase catalog, or editable branded gift-card designer in V1.

## 5. Redemption model

Support **gift-card-only settlement of a custom fixed Pay Link** when its immutable `AllowsGiftCard` flag is enabled by the creator. The code must belong to the request tenant, match currency, be Active and cover the entire request. One card may fund several requests over time; a request cannot combine multiple cards or partial gift plus provider funds.

Public authorization is possession of both the link capability and redeemable gift code. Request tenant/amount are resolved server-side; caller cannot supply a different tenant, price or target. Staff-assisted redemption uses the same service and strict Finance access. Gift purchase requests categorically reject gift tender, avoiding circular value issuance.

Atomic operation: claim the unpaid request, conditionally decrement card balance, append Redeem, insert settlement and close request Paid. Any failure rolls all these changes back. Do not use the existing mark-paid-at-till shortcut or masquerade as an online payment. Return balance only to the authorized code holder, never through a general customer aggregate.

## 6. Concurrency and idempotency strategy

Use real SQL transactions and SQL conditional transitions, not Redis as monetary authority. Rowversions plus a consistent lock order protect request, attempt/card and settlement. For redeem, conditional balance update requires correct rowversion, Active status and `RemainingAmount >= amount`; unique settlement and operation constraints defend independently. Re-read the committed idempotent result after conflicts. Retry bounded transient/deadlock failures using the same operation identity; never repeat an external provider call inside a retried DB transaction.

Mandatory bounded Idempotency-Key on new create/checkout/redeem/cancel/reversal mutations, scoped to tenant/request/actor or public capability and operation. Compare a canonical semantic request fingerprint; same key with changed amount/currency/card/target is Conflict. Store no raw code/token in that fingerprint or receipt. Existing public request contracts remain unchanged; new keys are mapped to namespaced internal keys rather than trusting the legacy global lookup.

Persist attempt/provider selection and stable merchant reference **before** calling the provider. Use Stripe's supported provider idempotency header; for Paystack use a stable reference and verify/reconcile ambiguous initialization before retry. Persist the checkout URL securely for the attempt, with expiry and provider reference aliases. A lost provider response must not create a second live attempt. Refunds similarly need stable operation identity and provider-confirmed results.

No SQL transaction spans HTTP. Only one active provider attempt per request; an active/ambiguous attempt excludes gift settlement. A terminal failed/expired attempt permits a new attempt only after authoritative verification that it cannot complete. Verified delayed success for a superseded/closed request goes to a durable exception record and reconciliation, never a second settlement or silently discarded payment.

## 7. Code/token security

Use cryptographic 256-bit output, nonsequential opaque credentials. Keep gift-code validation scoped to TenantId, store a tenant-bound SHA-256 lookup hash and display suffix only. No code in route/query/log/audit, general DTO, CSV/PDF report or notification message.

Recovery decision: because asynchronous paid issuance and lost browser responses need recoverable delivery, propose purpose-separated HMAC-SHA256 derivation from a random 256-bit nonce plus tenant/card identity using a versioned server key held in Key Vault. Store nonce/key version/hash, never plaintext. This is deliberately **recoverable under a server secret**, not a claim of one-way hash-only storage. Only authenticated purchaser-owned explicit reveal regenerates the code; Owner/Manager history reads do not. Keep old key versions while cards remain valid, use separate gift/link purposes, constant-time comparisons where applicable, and document rotation/incident procedures. No new crypto dependency. Recovery/key-lifecycle policy is a review decision.

Link credentials use a separately keyed purpose/nonce and hash lookup, allowing authorized staff to re-copy a URL without storing plaintext. Redact secret-valued headers and type/error representations. Existing IP limiter policies are reusable; add bounded credential-failure controls keyed by non-secret hashes and request/tenant, with no per-process-only financial security claims. Edge controls are needed across instances.

## 8. Expiry recommendation

**No gift-card expiry, dormancy fees or breakage in V1.** Ontario's [consumer guidance](https://www.ontario.ca/page/buying-or-using-gift-cards) says most retail monetary gift cards cannot expire, while identifying limited exceptions. This proposal uses monetary value, not a specific-service certificate.

Nigeria-specific merchant gift-certificate expiry/applicability was not conclusively established. The CBN publishes [stored-value/prepaid-card guidance](https://www.cbn.gov.ng/out/2010/circulars/bod/guidelines%20on%20stored%20value%20prepaid%20card%20issuance%20and%20operations.pdf); do not assume those bank-card rules either automatically govern or exempt this merchant voucher. Confirm classification/terms with local counsel and providers before live Nigeria issuance. No expiry is a conservative product proposal, not legal clearance or a claim about every Canadian province/Nigerian product.

An unpaid **purchase request/link** may expire; paid gift value does not. Suspension for a verified dispute blocks use without deleting the monetary ledger or deeming the value expired.

## 9. Pay Link data model

`PaymentRequest`: ID, TenantId, optional BusinessLocationId, purpose CustomCharge/GiftCardPurchase, immutable amount/currency/customer-visible label, explicit AllowsGiftCard, optional verified customer/roster reference, lifecycle, creator actor, CreatedAt, ExpiresAt, PaidAt/CancelledAt, rowversion, token hash/nonce/key version, creation idempotency receipt. No booking/invoice FK in this V1 scope.

`PaymentRequestAttempt`: tenant/request, sequence, existing PaymentIntentId, persisted provider key/account scope, stable merchant reference, session/payment-intent aliases, bounded checkout URL, attempt expiry/state, initialization/reconciliation timestamps and safe failure code. Keep canonical request amounts unchanged; a changed quote/charge requires a new request. Aliases may require a small child table with provider/account/reference uniqueness.

`CommerceOperation` or equivalent narrow receipt stores scoped mutation identity/fingerprint/result references, allowing safe retries including conflicts. A durable exception/reconciliation state records external money received without an eligible request; never invent a credit wallet as its resolution.

## 10. Pay Link lifecycle

Propose **7 days from creation** by default, with a bounded earlier expiry selected at creation; no arbitrary never-expiring payment request. This is an explicit proposed product policy for review, distinct from gift-card no-expiry.

Public states Active/PaymentPending/Paid/Expired/Cancelled; initialization substate can remain internal. Paid is closed permanently even if later refunded; replacement requires a new request. Failed checkout can retry on the same unexpired link only after the prior attempt is definitively terminal.

Owner/Manager can cancel Active with no active/ambiguous attempt. Cancellation during an initiated checkout returns Conflict/pending resolution; do not pretend local cancellation invalidates a provider session. Link expiry blocks new checkout; an already admitted attempt is allowed to resolve and a later verified payment must be recorded. Expose pending resolution rather than claiming no charge occurred. Worker reconciles timeouts/expiry using existing Hangfire. GET performs no payment creation or settlement.

## 11. Public link security

Propose `/pay#t=<opaque-token>` in Discover, with generic public API paths and credential in a dedicated redacted header. Fragments avoid placing the bearer in origin/edge request paths, server SSR logs and search engines. Provider callback URL construction must correctly preserve fragment/query separation; verify both providers in sandbox. If a provider cannot preserve the return capability, show a generic return page and direct payer to reopen the original link rather than use insecure identifiers.

Resolve capability before selecting tenant context; ignore untrusted X-Tenant-Id for this public surface. Link possession authorizes minimal charge display and checkout only, not customer profile/payment intent/document reads or gift-code reveal. Return business display name, fixed label/amount/currency, safe state and checkout action only. No tenant/customer/payment IDs, provider references, recipient contact, or private notes. Use opaque UI attempt identifiers only if needed.

Private/no-store API and HTML headers; noindex/nofollow, Referrer-Policy no-referrer, no TransferState/service-worker cache/browser persistence of credentials or payment data. No third-party analytics capturing fragments. Audit server/edge/App Insights/Sentry/log serializers for secret/header/callback redaction; generic form errors must not echo input. Validate token format, bound payloads, apply public read/mutation rate policies to actual trusted client IP, and limit checkout attempt creation. Invalid/missing/expired credentials get non-enumerating responses.

## 12. Provider integration and settlement prerequisite

Reuse existing Finance providers, market registry, webhook URL and intent lifecycle. Persist actual selected provider per attempt so tenant market changes cannot reroute an existing transaction/refund. Restrict F3 currencies to explicit supported tenant/provider pairs (initially NGN/Paystack, CAD/Stripe; USD only where a supported tenant market is already configured). Reject excess fractional precision/overflow rather than silently rounding user amounts. No FX or zero-decimal currency extension.

Required F3 safety work: provider initialization idempotency, durable reference/session mapping, exact expected amount/currency matching, success only when paid (including delayed Stripe methods), terminal-state monotonicity and atomic Finance fulfillment. Invalid/missing monetary facts require provider verification or quarantine, never assumed payment. Signature alone is not sufficient reconciliation.

**Live prerequisite:** approved tenant settlement mapping and verified merchant readiness. Existing adapters currently lack Stripe Connect destination and Paystack tenant split. Do not enable F3 production sales with their platform-only fallback, even at zero fee. Recommended delivery separates a settlement prerequisite slice following Sprint 34 from F3 feature slices. It may add tenant provider-account readiness records and an Application settlement port; provider onboarding/fee-refund policy requires explicit review. If that prerequisite is deferred, F3 remains development/sandbox-only with server-enforced live blocking. Do not silently implement an entire onboarding subsystem under this plan or label the feature production ready.

## 13. Gift-card / Pay Link interaction

Approve only the full-custom-charge gift settlement described in section 5. Provider and gift settlement are mutually exclusive through a persisted tender claim; no simultaneous provider checkout plus redeem. Gift-card purchases cannot be paid using another gift card. No partial/mixed tender, multiple cards per charge, booking gift application or gift deposit path.

This yields useful tenant-specific redemption without changing existing booking balance calculations or adding universal customer balances.

## 14. Refund and reversal strategy

Preserve existing legacy Finance refund API/contracts while characterizing their limitations. New F3-origin intents must not pass unchecked through the legacy generic refund path: enforce purpose-aware policy in Finance before external calls, including attempts to use old routes to bypass it.

Custom provider charges: initially full refunds only through the existing provider adapter with durable idempotent refund operation; pending provider result stays RefundPending, confirmed result completes refund. No partial-refund accounting claim. Settlement refund metadata does not reopen the Pay Link. Verify destination/application-fee or split reversal once merchant settlement is supported.

Gift-redemption correction: explicit Owner/Manager may reverse an entire redemption only against its original settlement, once, restoring value to the same card/currency with a positive reversing ledger row. Request remains closed/refunded; no cash or provider refund. Redemptions/transactions are never edited/deleted.

Gift-purchase refunds: no self-service V1 refund of activated/redeemed value. Provide a documented operator/provider reconciliation route for legally required returns/disputes, not an assertion that refunds are legally prohibited. Before enabling live gift sales, approve an unused-card full-refund procedure that atomically freezes redemption, verifies unused balance, performs an idempotent provider refund outside the transaction, and appends the value reversal only on confirmation. Unknown/pending outcomes remain suspended. External refunds/chargebacks suspend remaining value and raise reconciliation; never create negative redeemable balance or cash credit when value was already spent. This operational dependency must not be hidden by merely removing the UI button.

## 15. Permissions

Retain `portal.finance`, tenant JWT/header checks and existing location scope. Add a Finance-owned raw persisted-role access port, implemented by Identity, without calling Normalize for these operations:

| Operation | Proposed business access |
|---|---|
| Create/cancel requests, initiate gift sale, assisted redemption, corrective reversal/refund | Explicit Owner/Manager plus Finance permission |
| View masked cards, ledger, requests and operational exports | Explicit Owner/Manager/Accountant plus Finance permission |
| Gift-code reveal | Authenticated actual purchaser-owned record, never general business membership |
| Public pay/whole-charge redeem | Valid scoped link capability; gift code additionally required for gift tender |

Unknown/legacy roles are denied for F3, without globally changing legacy normalization. Receptionist/practitioner access is deferred until finance permissions are deliberately expanded. Any admin operations use existing MFA/audit conventions, not unscoped staff impersonation.

## 16. Entitlements recommendation

Current JSON has Starter/Professional/Business with 25 catalog keys. Runtime additionally derives compatibility flags; InvoicesReceipts/OnlinePayments/Deposits are universal. No GiftCards/PayLinks keys exist; TenantBillingPolicy/effective SKU and backend `IEntitlementsService` remain authority.

Recommend **initially available across existing plans**, subject to merchant readiness, applicable runtime commerce capability and explicit permissions; no invented quantity caps or new purchased-feature gate. Do not equate the Portal's current `deposits` capability check with every custom charge/gift workflow. Report any new capability fact required separately from permission/entitlement. Professional/Business monetization can be a later catalog/parity/backend-gate decision. Reporting formats and location aggregation retain their existing gates. No entitlement values change before review.

## 17. Audit and privacy

Metadata-only issued/redeemed/reversed, request-created/cancelled, refund/reconciliation and privileged read/export/reveal audit: IDs, actor, outcome, amount/currency where appropriate, correlation and timestamp. No codes, tokens, callbacks containing credentials, provider secrets, payloads or card data. Financial operation receipts/ledger metadata commit with the monetary mutation; existing audit writer mirrors operational events without being the only proof of transaction.

Use existing admin privacy export/erasure orchestration through a Finance Application port. Include authorized financial references and appropriate customer data in privacy exports, never bearer credentials. Detach/redact purchaser/recipient/contact identifiers where permissible while preserving ledger/settlement integrity and explicit legal retention. Do not match/delete solely by email. Determine financial retention/legal holds with policy owners; F2's 24-month intake policy does not apply to Finance. No new contradictory deletion endpoint, no intake answers, no automatic deletion of outstanding merchant value.

## 18. F1 integration

Add a permission-isolated Finance metadata source/subresource for explicitly attributed customer cards and standalone charges. Expose tenant-specific masked card ID, currency, balance/status, and bounded issue/redemption/payment timestamps. Purchaser is not automatically recipient/beneficial owner: label purchased cards distinctly and show no recipient holdings without verified association. Never aggregate as “Adeni balance.”

Keep F1 `recordedOnlinePaymentsByCurrency` semantics intact (existing booking-linked provider principal); gift issuance/redemption is not additional online spending. Use separate fields/kinds with shared-client compatibility tests or a separate additive subresource rather than widening closed enums silently. Preserve Finance permission, customer-route eligibility, history caps, cursor binding and location rules; omit unscopable tenant-wide card data in branch-only views. F2 untouched.

## 19. Proposed additive APIs

Final names follow existing Result/error conventions and `/api/v1` prefix. Existing `/finance/links` response and shared-client method stay compatible.

| Surface | Routes/purpose |
|---|---|
| Business | `/api/v1/tenant/finance/pay-links`: create/list/detail; `/{id}/cancel` |
| Business gift sales | `/api/v1/tenant/finance/gift-cards/purchases`: create purchase request; cards list/detail/transactions, masked only |
| Public capability | `/api/v1/public/pay-links/resolve`, `/checkout`, `/redeem`: token header; no internal target IDs in public DTO |
| Purchaser | `/api/v1/me/gift-cards`, `/{id}/code`: customer ownership, explicit reveal only |
| Corrective operation | Tenant finance settlement `/{id}/reverse-gift-redemption`; purpose-aware provider refund operation |
| Customer metadata | Additive tenant customer commerce/gift metadata route using F1 access orchestration |
| Reporting | Registered tenant gift-card operational CSV export via existing Reporting controller/service |

New shared Zod DTOs, error codes/localized messages and typed finance client methods; no breaking edits to existing initialize, payment receipt, customer or intake contracts. Financial amounts remain existing JSON decimal-compatible numbers within SQL limits; clients format display, backend computes all monetary values.

## 20. Portal and public UI

Portal `/finance`: focused lazy views for fixed requests, masked gift cards/ledger, and create/share actions. Copy URL plus existing wa.me deep link; explicit user-initiated email/SMS only as a later integration unless existing notification permission/entitlement/deduplication can be reused without secret logging. No automatic messages in V1. Customer context may preselect an authorized roster identity, not infer it from search text. No mandatory booking UI changes in this proposed scope.

Discover: responsive `/pay` client-rendered private/noindex page, canonical merchant/amount/label, secure provider redirect, pending/confirmed/error states based on server state. Gift tender entry appears only if allowed; no automatic code prefill/persistent storage. Gift purchase login is required for code delivery, not for ordinary custom-charge payment. An explicit owned gift-card reveal page supports manual recipient sharing. No card details enter Adeni forms; use existing hosted checkout.

Use standalone components, Signals/computed, RxJS/cancellation patterns, existing Carbon/Adeni controls and formatting. Abort stale token/customer/location reads; clean up bounded polling on navigation/destroy; no extra SignalR subscriptions/global store. Leave public Discover SSR/hydration/SEO/market handling unchanged; explicit private-route SSR/header tests are required.

## 21. SQL/DACPAC changes

Add relational tables for cards, transactions, payment requests, attempts/reference aliases, settlements and narrow operation/reconciliation receipts under existing `payments` schema. Add tenant+ID keys and composite tenant FKs; restrict delete of ledger parents, no cascade that destroys financial history. Unique purchase-to-card, card sequence, issue-once, settlement-per-request, active-attempt, reversal-per-original and scoped idempotency indexes. Explicit decimal(12,2), currency/status/positive-amount checks and rowversions; balance bounds do not replace transactional ledger validation.

Add persisted provider identity/aliases and concurrency to existing intent storage where required. Backfill provider identity only from unambiguous references/known configuration; quarantine ambiguity rather than guessing from a changed market. Keep namespace/schema names and old enum numbers stable; new intent purposes need coordinated enum/client mapping review. Prefer separate origin relations where they avoid breaking existing enum consumers.

EF maps SQL authority; no EF migrations. Use partial DbContext configuration following F2. Verify planned filtered indexes/FKs with actual DACPAC publish and EF model parity. Merchant settlement prerequisite has its own explicitly reviewed relational additions; table count is provisional until that dependency is resolved.

## 22. Test strategy, including real SQL Server

- Domain/Application: currency/precision/bounds, ledger arithmetic, lifecycle transitions, no expiry, typed request fingerprint comparison, scope/permission and disclosure policy, no raw secrets in DTO/error/ToString/audit.
- SQL money tests using separate contexts/connections and synchronized competing transactions: two redemptions cannot overspend; same idempotency key creates one ledger delta; duplicate success webhooks issue once; duplicate/competing attempts settle once; provider claim versus gift race; cancel/checkout/expiry/success races; whole reversal once; cross-tenant composite-FK rejection; atomic rollback after forced failures; ledger sum equals cache; unknown provider response followed by reconciliation. InMemory tests do not establish this correctness.
- Provider HTTP contract tests: Stripe idempotency and paid-vs-complete delayed methods; amount/currency/account/reference mismatch; Paystack stable-reference verify/retry; signature rejection; aliased refund events; pending/full-refund behavior; missing merchant destination blocks F3. Sandbox end-to-end for each enabled market including callback fragment handling and merchant settlement/refund.
- API security: JWT/tenant/header mismatch, forged actor/customer/location, Owner/Manager/Accountant/Receptionist/practitioner/unknown roles, public token cannot read customer/payment/documents or reveal gift code, wrong-tenant code, non-enumerating failures, rate limits, no-store/noindex/referrer headers, idempotency conflicts, legacy route cannot bypass new-origin refund restrictions.
- Regression: unchanged booking deposits/balance/till, existing receipt routes, legacy link contract, Finance permissions, entitlements parity/effective SKU, F1 history/location/source scope and totals, F2 unaffected. Architecture tests: thin controllers and dependency direction.
- Privacy/reporting: redaction leaves valid financial ledger, no token/code/contact in ordinary exports, safe centralized CSV, numeric XLSX types if an existing supported format is added; issuance and redemption not double-counted as service sales. Only operational gift report, no accounting claims.
- Frontend/shared: public import compatibility/Zod shapes and exact requests/headers; fixed amount uneditable after create; permission-safe controls; share URL; pending not paid on return marker; retry key reuse; lost response/reopen; no secret persistence; cancellation/destruction; no new SignalR subscriber; SSR shell contains no private data; existing public SSR/SEO tests.

Planned implementation validation (not run by this planning-only change):

```sh
dotnet build Adeni.slnx -c Release
dotnet test Adeni.slnx -c Release
ADENI_SQLSERVER_TESTS=1 dotnet test tests/Adeni.Infrastructure.Tests/Adeni.Infrastructure.Tests.csproj -c Release --filter 'Category=SqlServer'
npm test --workspace @adeni/shared
npm test --workspace @adeni/portal
npm test --workspace @adeni/discover
npm run typecheck
npm run build:portal
npm run build:admin
npm run build:discover
```

Configure `ConnectionStrings__AdeniDb` externally for SQL tests; build/publish the DACPAC to an isolated SQL Server first. Use CI Node version; `npm ci` if restoring dependencies. Report inherited production stylesheet/initial-bundle failures precisely; do not raise budgets to hide them. Provider sandbox tests require configured test merchant accounts, not production credentials.

## 23. Migration and deployment

Additive schema first; no gift-card/backdated-link backfill from historic intents. Existing links/receipts remain valid under existing behavior. Deploy all writers with completion/origin/refund guards before enabling F3 creates. Feature enablement is an operational rollout control, not a new plan entitlement. Old writers must be drained; mixed-version webhook processing cannot safely activate monetary effects. Rollback disables new sales/checkout while keeping receipt access, ledger and reconciliation for paid transactions; never drop issued value/history.

Provision versioned credential keys, approved merchant settlement mappings, canonical HTTPS public base URL/callback origins, proxy-aware edge/rate controls and secret-redaction settings before live enablement. Reconciliation jobs must be observable (oldest unresolved attempt, paid-but-unissued, ledger mismatch, held/disputed value), with an operator runbook. No live rollout until settlement, refund/dispute procedure and market terms are validated. Existing Angular production budgets remain separate deployment blockers.

## 24. Risks and open decisions

1. **Settlement blocker:** approve the Sprint-34 prerequisite slice, or explicitly accept sandbox-only F3. Current code cannot prove customer-to-business routing.
2. **Scope:** approve custom fixed charges first and defer booking/deposit/invoice collection until reservations and all competing collection paths are designed together.
3. **Gift sale/redeem:** approve authenticated purchaser delivery and whole-charge-only gift tender, with no complimentary issue/top-up/manual adjustment.
4. **Recovery:** approve versioned derived recoverable codes and their key custody/rotation; hash-only one-shot delivery is operationally unsafe with asynchronous payment/lost responses.
5. **Refund/disputes:** approve unused-card refund and external refund/chargeback operational handling before live sales. Spent-value losses are a merchant dispute issue, not a new wallet or negative customer balance.
6. **Market terms:** no-expiry/no fees proposed; Nigeria classification and applicable consumer terms require review. No universal legal claim.
7. **Policy:** approve seven-day unpaid-link expiry, initial plan-universal availability and explicit raw-role operation matrix. No product/entitlement decision is implemented by this plan.
8. Existing post-save reliability can still affect receipts/notifications/realtime; financial state is committed atomically and reconciled, but no exactly-once external delivery promise.
9. No recipient identity inferred from contact details; no promise that a purchaser owns a gifted balance. Financial PII/ledger retention is separate from Intake retention.

## 25. Explicitly deferred functionality

Booking/deposit/balance and invoice payment links; booking gift settlement; partial/mixed/multi-card tender; gift top-ups/transfers/complimentary issue/arbitrary adjustments; automatic gift delivery and reminders; generic invoicing/accounting/revenue recognition; a public gift marketplace; installment/BNPL/lending; cross-business/universal wallets/cash-out/crypto; loyalty/memberships/packages; calendar/embed/segments; branded document redesign; WhatsApp API; new notification/reporting/state engines; full outbox rollout. F4/F5 remain untouched.

## 26. Recommended implementation sequence

1. Review this plan's scope, settlement prerequisite, expiry, disclosure/recovery, refunds and authorization/entitlement decisions. Stop here until approved.
2. Characterize existing payment/booking/refund behavior; add required provider/completion/idempotency tests and resolve merchant settlement prerequisite in its own reviewed slice.
3. Add SQL schema/domain/Application contracts and financial invariants; publish isolated DACPAC; implement real concurrency/rollback tests before UI.
4. Implement durable custom requests and attempts, trusted payment confirmation, cancellation/reconciliation and purpose-aware refund guards; keep old contracts compatible.
5. Implement paid gift issuance and purchaser code reveal; then atomic whole-custom-charge redemption and bounded corrective reversal with SQL/security tests.
6. Add typed shared/client contracts, focused Portal management/share and private Discover pay/reveal flows with lifecycle/SSR tests.
7. Add safe F1 metadata, privacy integration and operational report; audit for secrets, double counting and cross-module coupling.
8. Run backend/frontend/real SQL/provider-sandbox validation; document exact results/deployment limits and commit SHAs. Stop for F3 review; do not start F4.

Planning validation: repository/source inspection, branch/HEAD/status checks and public-source policy research only. No application builds/tests or provider transactions ran, and no executable code changed.
