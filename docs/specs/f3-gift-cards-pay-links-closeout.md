# F3 — Business gift cards and Pay Links closeout

**Status: Development/Testing Stub implementation complete; awaiting F3 review. Live commerce is blocked. F4 has not started.**

Branch: `feature/squarespace-inspired-v1`. Baseline: completed F2 `824e42c9b43e44717cfab9b086a3a81f6dbe0391`. Scope: [approved plan](f3-gift-cards-pay-links-plan.md). No protected branch, push, merge, cloud deployment or production data change.

## Delivered scope and deliberate live boundary

Tenant/business-specific monetary gift cards, fixed custom-charge requests and gift-purchase requests now have relational state, safe sharing, purchaser-owned credential disclosure and operational history. A gift card can settle an entire custom charge when the creator allows it; the card may retain value for other charges. No mixed tender, automatic booking settlement, universal wallet or cross-business value.

**Live prerequisite remains unresolved:** current Stripe/Paystack adapters do not implement tenant merchant settlement. F3 create/checkout/redeem/refund/reversal therefore require Development/Testing **and** the Stub provider. Completion is also restricted to Stub and a development/test host. There is no configuration switch to permit live fallback. Real Stripe/Paystack test-mode checkout, Connect/split onboarding, provider initialization/refund idempotency, delayed-payment verification/reference correlation and live dispute reconciliation must be completed and tested before changing those guards. This delivery is not a production payment launch.

Existing payment routes, provider adapters, booking/deposit/till flows and legacy contracts remain compatible. New-origin intents cannot use the legacy generic refund endpoint. No entitlement catalog values or new plan gates, external dependencies, API removals, Intake coupling, global state framework or extra SignalR subscription were introduced.

## Monetary behavior and persistence

Six new `payments` tables: `commerce_requests`, `commerce_attempts`, `commerce_operations`, `commerce_settlements`, `gift_cards`, `gift_transactions`. Existing intent storage gains a tenant/ID unique key for tenant-aware foreign keys. SQL/DACPAC remains authoritative; no EF migrations.

- Amounts use existing decimal(12,2), explicit NGN/CAD/USD in the stub environment, positive values and exact hundredths. No FX or client-calculated settlement.
- Published request amount/currency/label are fixed; changed charges require a new request. Proposed/approved unpaid link expiry is seven days by default, with an earlier expiry allowed. Gift value has no expiry or dormancy fee.
- Paid completion atomically commits the intent, request settlement, card activation and single Issue transaction. Receipt/notification/realtime work follows that commit. Receipt repair can run through existing Hangfire; monetary activation does not depend on post-save event delivery.
- Redemption atomically changes request/card state, appends a debit and creates the settlement/operation receipt. SQL rowversions and uniqueness constraints prevent competing redemptions, duplicate issuance, double settlement or a simultaneous provider/gift tender.
- A provider attempt is persisted before initialization. Checkout reloads resume that same stub attempt; they never create another live charge. Purchaser identity must match when resuming a gift purchase. Cancellation cannot discard an admitted payment. Expiry prevents new attempts but does not erase an admitted attempt's eventual result.
- Corrective gift redemption reversal restores value to the same card with a reversing ledger entry, once. The paid request closes Refunded; it never reopens.
- Full stub provider refunds use RefundPending before the external call. Unused gift cards are suspended while pending; only a confirmed matching refund voids their value through a ledger entry. Spent gift value cannot use this refund route. Ambiguous/pending outcomes do not become Refunded by assumption.
- Idempotency compares scoped operation keys and semantic fingerprints, including a hash of a presented code rather than its contents. Changed payloads conflict; repeated committed operations do not repeat monetary deltas. Concurrent losers may receive Conflict and can retry/read the committed result.

No balance-only accounting, ledger mutation, arbitrary adjustment, complimentary issue, top-up, cash withdrawal, cross-card transfer or accounting/revenue-recognition engine. Gift reports count redemption from redemption/reversal entries, so a voided purchase is not mislabeled as redeemed value.

## Access, credentials, audit and privacy

New business mutations require explicit persisted Owner/Manager roles; masked reads/history require Owner/Manager/Accountant. Existing Finance permission and tenant access remain enforced. Unknown/legacy role normalization cannot authorize these services. New controllers contain no DbContext/EF queries. Module communication uses Application contracts; architecture tests pass.

Custom-charge payment uses a scoped bearer capability without customer login. Gift purchases require an authenticated existing customer so the code can be delivered and recovered safely. Recipient delivery/contact collection is deferred; purchaser manually shares the code. Purchaser identity is not a claim that they are the eventual gift recipient.

Codes/tokens have 256-bit cryptographic output. The database stores lookup hashes plus versioned HMAC derivation inputs, allowing controlled recovery without plaintext storage. Recovery is **not** one-way hash-only protection: server key custody is required. Gift/link purposes are separate. Only an explicit purchaser-owned reveal returns a full code; staff lists, reports and F1 metadata never do. Sensitive DTO/entity log representations are redacted. New controllers use bounded bodies, safe validation errors and private/no-store headers.

Share URLs use `/pay#t=...`; public requests send the capability in `X-Adeni-Pay-Token`, not a logged route/query string. Public responses expose merchant display name, customer-visible label, amount/currency, allowed action and safe state, without internal tenant/customer/payment identifiers. Existing public read/mutation rate policies apply. Copy and `wa.me` sharing are user initiated; no automatic notification or WhatsApp API integration.

Durable operation receipts and ledger entries contain identifiers, actors, state and timestamps, not credentials or payment payloads. Existing audit infrastructure records operational mutations/privileged reads; reporting uses the existing export audit. Existing admin privacy erasure calls a Finance port that detaches purchaser PII while preserving financial value/history. An admin-MFA-protected commerce export contains safe purchaser-linked metadata. Erasure during pending checkout does not lose subsequent confirmed gift value, and does not resurrect the erased identity. Intake retention/holds do not govern Finance records.

## APIs and UI

Additive surfaces:

- Tenant `/api/v1/tenant/finance/pay-links`: create/list, cancel, full refund and gift-redemption reversal.
- Tenant `/api/v1/tenant/finance/gift-cards`: masked cards and bounded transaction history.
- Public `/api/v1/public/pay-links/resolve`, `/checkout`, `/redeem`: header capability; gift redemption additionally needs the code.
- Customer `/api/v1/me/gift-cards` and explicit `/{id}/reveal` POST.
- Tenant customer `/{id}/purchased-gift-cards`: F1-authorized, Finance-isolated metadata, bounded to 25. Rejects branch-scoped aggregation rather than leaking business-wide value.
- Existing Reporting endpoint gains `gift-cards` CSV. It selects cards issued in the requested range and shows **current** issued/outstanding/net-redemption values, grouped per card/currency. This is not a historical as-of accounting statement. No codes/PII in the export; centralized safe CSV serialization is reused. Existing export and multi-location gates remain authoritative.
- Existing admin customer scope gains privileged `/{id}/commerce-export`.

Portal `/finance/quick-pay` provides simple fixed request creation, gift sales, copy/wa.me, cancellation, correction/refund, masked cards/history and CSV. New page authorization checks Finance permission without assuming that custom charges require the booking deposit capability. Existing navigation/capability behavior remains intact. F1 overview has an explicit purchased-card metadata action only when its Finance source and whole-business scope allow it; normal F1 totals/history are unchanged.

Discover `/pay` and `/my-gift-cards` are client-rendered/no-store/noindex. Token/code state stays in memory and stale requests are aborted. Browser return markers do not confirm payment. A gift purchaser signs in and then reopens the original link; the capability is not persisted through login. The existing development checkout provides a link to purchased cards. No payment details or gift codes enter TransferState. Existing public business/wildcard SSR and SEO remain unchanged.

## Validation results

Environment: .NET SDK 10.0.402; SQL Server 2022 Developer in an isolated local Docker container; SqlPackage 170.5.96; Node 24.19.0; Chromium 154. No dependency manifest/lockfile change; existing restored packages were used. CI's pinned Node 22.22.3 and hosted/provider environments were not executed.

Backend commands used `/workspace/dotnet/dotnet` with `DOTNET_CLI_HOME=/workspace/dotnet-home`, `NUGET_PACKAGES=/workspace/nuget`, `TMPDIR=/workspace/dotnet-tmp`:

| Command | Result |
|---|---|
| `/workspace/dotnet/dotnet build Adeni.slnx -c Release` | Exit 0; zero warnings/errors, including DACPAC. |
| `/workspace/dotnet/dotnet test Adeni.slnx -c Release` | Exit 0; Domain 26, Application 66, Infrastructure 350, API 242: **684 passed**; 16 opt-in SQL cases skipped in this run. |
| `ADENI_SQLSERVER_TESTS=1 /workspace/dotnet/dotnet test tests/Adeni.Infrastructure.Tests/Adeni.Infrastructure.Tests.csproj -c Release --filter 'Category=SqlServer'` | With local `ConnectionStrings__AdeniDb`: **16 passed, none skipped** (eight F3 plus eight inherited). |
| `/workspace/dotnet/dotnet test tests/Adeni.Api.Tests/Adeni.Api.Tests.csproj -c Release --filter FullyQualifiedName~Commerce` | Eight passed, including real HTTP gift purchase/confirm/reveal/redeem, refund-route bypass rejection and Reporting export. |
| `npm test --workspace @adeni/shared` | 83 passed. |
| `CHROME_BIN=/tmp/adeni-chrome npm test --workspace @adeni/business` | 41 passed. Wrapper selects installed Chromium with container sandbox flags. |
| `npm test --workspace @adeni/discover` | Ten passed. |
| `npm run typecheck` | Exit 0; shared/api-client/UI/brand typechecks and Portal/Admin/Discover development builds, including Discover SSR. |
| `npm run build:business` | Exit 1; existing component-style error categories remain. Initial bundle rounds to 3.00 MB, below its error threshold but with very little headroom. |
| `npm run build:admin` | Exit 1; existing style categories and initial bundle error remain. |
| `npm run build:discover` | Exit 1; existing shared stylesheet error remains. |

DACPAC was successfully published to the isolated local database with `BlockOnPossibleDataLoss=True` before SQL tests. SQL connection credentials were supplied only to commands, not committed. SQL cases use independent contexts and synchronized races; they verify overspend prevention, duplicate completion/issuance, competing tenders/cancellation, repeated redemption/reversal, refund-versus-redemption, tenant FKs/balance checks, query translation and full rollback after a relational failure.

An HTTP SSR smoke against the built Discover Node server returned 200 for `/pay` and `/my-gift-cards`, with private/no-store/noindex/no-referrer headers and no payment inputs or credential data in their server-rendered shells. Temporary server and SQL container are local test resources only.

**New tests relative to F2: 59 discovered cases:** six Application access-composition cases, 24 Infrastructure service/credential cases, eight real SQL cases, eight API cases, three shared, seven Portal and three Discover. Unique passing cases across final backend/real SQL/frontend runs total **834**, excluding duplicate targeted reruns. No claim of live Auth0/provider/merchant settlement end-to-end, production accessibility/visual acceptance or hosted CI validation.

## Exact production build blockers

No budget was increased and no styling refactor was included. Error categories existed at F2; shared contract/client additions do increase bundle sizes.

| App | Error threshold | Final reported size |
|---|---|---|
| Portal services stylesheet | 24 kB | 24.50 kB |
| Portal staff stylesheet | 24 kB | 27.41 kB |
| Portal plan stylesheet | 24 kB | 28.33 kB |
| Admin initial bundle | 2 MB | 2.19 MB; 185.37 kB over error budget |
| Discover shared `_data-grid.scss` | 14 kB | 16.90 kB |

Admin's 14 stylesheet errors remain: transactions 25.45, audit-logs 25.39, businesses 25.25, customers 25.75, reviews 25.61, pricing-catalog 26.73, business-subscription-wizard 27.61, markets 25.38, trust-badges 25.25, pending 25.25, pricing-entitlements 25.39, business-commercial-audit 32.83, business-overview 37.71, business-billing 32.74 kB, each against 24 kB.

Portal initial warning is 499.63 kB over 2.5 MB (3.00 MB rounded), below the 3 MB error limit; further eager shared-client/schema growth risks crossing it. Discover initial is 1.46 MB, below its 3 MB error limit. **The branch is not production-build-green.**

## Configuration, rollout and remaining limits

- Development/Testing uses the existing Stub provider. `Finance:Commerce:PublicBaseUrl` defaults to `http://localhost:5190`; set it to the actual Discover origin for share URLs. No public callback override is accepted.
- `Finance:Commerce:ActiveKeyVersion` and `CredentialKeys:<version>` select base64 keys of at least 32 bytes. Development/Testing has a documented development-only fallback key; it is never used outside those environments. Preserve old configured versions for every retained card/token, back up keys appropriately and define incident recovery. New live enablement requires Key Vault-managed keys and proper HTTPS origins.
- Publish schema before all writers and retain it on rollback. Stop old writers before enabling monetary operations; old code must not process F3 intents/refunds. No historical card or link backfill. Disabling new sales must not delete financial history or outstanding merchant liability.
- Existing Hangfire registers a five-minute stub reconciliation/receipt-repair job. It resumes persisted initialization and repairs missing receipts in bounded batches; logs counts only. It does not implement live provider reconciliation, automatic card delivery or an outbox. Pending/refund/dispute states need operator visibility and stable retry procedures.
- Post-save failure can still affect notifications/receipts/realtime; atomic monetary records plus receipt repair reduce selected failure windows. Do not claim exactly-once external delivery. Transactional outbox remains separate work.
- Real provider settlement/onboarding, provider idempotency/verification/reference/refund hardening and market legal/financial terms are **live-launch blockers**, alongside production build budgets. Nigeria merchant-voucher classification was not conclusively established; no-expiry is a product default, not legal clearance. Unused-card stub refunds do not settle provider split/application-fee policy.
- No recipient PII/delivery workflow, gift-card purchase catalog, attachments, card design, arbitrary adjustments, manual free issuance, reloads, cash refunds, partial/mixed tender, booking/deposit/invoice links, gift booking settlement, Flutter implementation or F4/F5 functionality.
- Existing legacy payment limitations identified in the plan remain outside this guarded F3 slice. Removing a sandbox guard alone is not an acceptable live rollout.

## Reviewable commits and next step

- `4abf0a1`: approved plan.
- `8bf328a`: backend, SQL, privacy/reporting integration and monetary/security tests.
- `f24c27d`: compatible shared/client contracts, Portal and private Discover pages/tests.
- Final documentation commit: this closeout, approved-plan status and Finance guide; use branch HEAD for the complete F3 SHA.

Review the guarded F3 implementation before authorizing further work. Merchant settlement should be a separate reviewed prerequisite; no F4 implementation or branch promotion was performed.

## Files changed relative to F2

72 files, grouped here as an exact inventory:

- [apps/discover/src/app/app.routes.server.ts](../../apps/discover/src/app/app.routes.server.ts)
- [apps/discover/src/app/app.routes.ts](../../apps/discover/src/app/app.routes.ts)
- [apps/discover/src/app/core/guards/customer-auth.guard.ts](../../apps/discover/src/app/core/guards/customer-auth.guard.ts)
- [apps/discover/src/app/pages/checkout-stub/checkout-stub.component.html](../../apps/discover/src/app/pages/checkout-stub/checkout-stub.component.html)
- [apps/discover/src/app/pages/pay/pay.component.spec.ts](../../apps/discover/src/app/pages/pay/pay.component.spec.ts)
- [apps/discover/src/app/pages/pay/pay.component.ts](../../apps/discover/src/app/pages/pay/pay.component.ts)
- [apps/discover/src/app/pages/pay/purchased-gifts.component.ts](../../apps/discover/src/app/pages/pay/purchased-gifts.component.ts)
- [apps/business/src/app/app.routes.ts](../../apps/business/src/app/app.routes.ts)
- [apps/business/src/app/core/commerce-api.spec.ts](../../apps/business/src/app/core/commerce-api.spec.ts)
- [apps/business/src/app/pages/commerce/commerce-permission.guard.ts](../../apps/business/src/app/pages/commerce/commerce-permission.guard.ts)
- [apps/business/src/app/pages/commerce/commerce.component.html](../../apps/business/src/app/pages/commerce/commerce.component.html)
- [apps/business/src/app/pages/commerce/commerce.component.spec.ts](../../apps/business/src/app/pages/commerce/commerce.component.spec.ts)
- [apps/business/src/app/pages/commerce/commerce.component.ts](../../apps/business/src/app/pages/commerce/commerce.component.ts)
- [apps/business/src/app/pages/customers/customer-profile-data.service.spec.ts](../../apps/business/src/app/pages/customers/customer-profile-data.service.spec.ts)
- [apps/business/src/app/pages/customers/customer-profile-data.service.ts](../../apps/business/src/app/pages/customers/customer-profile-data.service.ts)
- [apps/business/src/app/pages/customers/customer-profile-panel.component.html](../../apps/business/src/app/pages/customers/customer-profile-panel.component.html)
- [apps/business/src/app/pages/payments/payments.component.html](../../apps/business/src/app/pages/payments/payments.component.html)
- [apps/business/src/app/pages/payments/payments.component.ts](../../apps/business/src/app/pages/payments/payments.component.ts)
- [db/Adeni.Database/payments/Tables/commerce_attempts.sql](../../db/Adeni.Database/payments/Tables/commerce_attempts.sql)
- [db/Adeni.Database/payments/Tables/commerce_operations.sql](../../db/Adeni.Database/payments/Tables/commerce_operations.sql)
- [db/Adeni.Database/payments/Tables/commerce_requests.sql](../../db/Adeni.Database/payments/Tables/commerce_requests.sql)
- [db/Adeni.Database/payments/Tables/commerce_settlements.sql](../../db/Adeni.Database/payments/Tables/commerce_settlements.sql)
- [db/Adeni.Database/payments/Tables/gift_cards.sql](../../db/Adeni.Database/payments/Tables/gift_cards.sql)
- [db/Adeni.Database/payments/Tables/gift_transactions.sql](../../db/Adeni.Database/payments/Tables/gift_transactions.sql)
- [db/Adeni.Database/payments/Tables/payment_intents.sql](../../db/Adeni.Database/payments/Tables/payment_intents.sql)
- [docs/finance.md](../../docs/finance.md)
- [docs/specs/f3-gift-cards-pay-links-closeout.md](../../docs/specs/f3-gift-cards-pay-links-closeout.md)
- [docs/specs/f3-gift-cards-pay-links-plan.md](../../docs/specs/f3-gift-cards-pay-links-plan.md)
- [packages/api-client/src/finance/commerce-api.ts](../../packages/api-client/src/finance/commerce-api.ts)
- [packages/api-client/src/index.ts](../../packages/api-client/src/index.ts)
- [packages/shared/src/commerce.test.ts](../../packages/shared/src/commerce.test.ts)
- [packages/shared/src/commerce.ts](../../packages/shared/src/commerce.ts)
- [packages/shared/src/i18n/errors.ts](../../packages/shared/src/i18n/errors.ts)
- [packages/shared/src/i18n/messages/overlay-en.ts](../../packages/shared/src/i18n/messages/overlay-en.ts)
- [packages/shared/src/i18n/messages/overlay-fr.ts](../../packages/shared/src/i18n/messages/overlay-fr.ts)
- [packages/shared/src/index.ts](../../packages/shared/src/index.ts)
- [packages/shared/src/report-keys.ts](../../packages/shared/src/report-keys.ts)
- [src/Adeni.Api/Controllers/CommerceControllers.cs](../../src/Adeni.Api/Controllers/CommerceControllers.cs)
- [src/Adeni.Api/Errors/ApiErrorResponseMapper.cs](../../src/Adeni.Api/Errors/ApiErrorResponseMapper.cs)
- [src/Adeni.Api/Extensions/HangfireExtensions.cs](../../src/Adeni.Api/Extensions/HangfireExtensions.cs)
- [src/Adeni.Application/Finance/ICommerceService.cs](../../src/Adeni.Application/Finance/ICommerceService.cs)
- [src/Adeni.Application/Finance/IGiftCardReportQuery.cs](../../src/Adeni.Application/Finance/IGiftCardReportQuery.cs)
- [src/Adeni.Application/Reporting/ReportDefinitionRegistry.cs](../../src/Adeni.Application/Reporting/ReportDefinitionRegistry.cs)
- [src/Adeni.Application/Reporting/ReportKeys.cs](../../src/Adeni.Application/Reporting/ReportKeys.cs)
- [src/Adeni.Application/Tenancy/CustomerGiftCardsQueryService.cs](../../src/Adeni.Application/Tenancy/CustomerGiftCardsQueryService.cs)
- [src/Adeni.Domain/Common/ErrorCodes.cs](../../src/Adeni.Domain/Common/ErrorCodes.cs)
- [src/Adeni.Domain/Finance/CommerceModels.cs](../../src/Adeni.Domain/Finance/CommerceModels.cs)
- [src/Adeni.Infrastructure/Admin/AdminCustomerService.cs](../../src/Adeni.Infrastructure/Admin/AdminCustomerService.cs)
- [src/Adeni.Infrastructure/Finance/CommerceCredentials.cs](../../src/Adeni.Infrastructure/Finance/CommerceCredentials.cs)
- [src/Adeni.Infrastructure/Finance/CommercePaymentCompletion.cs](../../src/Adeni.Infrastructure/Finance/CommercePaymentCompletion.cs)
- [src/Adeni.Infrastructure/Finance/CommercePaymentLifecycle.cs](../../src/Adeni.Infrastructure/Finance/CommercePaymentLifecycle.cs)
- [src/Adeni.Infrastructure/Finance/CommercePrivacyService.cs](../../src/Adeni.Infrastructure/Finance/CommercePrivacyService.cs)
- [src/Adeni.Infrastructure/Finance/CommerceReconciliationJob.cs](../../src/Adeni.Infrastructure/Finance/CommerceReconciliationJob.cs)
- [src/Adeni.Infrastructure/Finance/CommerceRefundService.cs](../../src/Adeni.Infrastructure/Finance/CommerceRefundService.cs)
- [src/Adeni.Infrastructure/Finance/CommerceService.cs](../../src/Adeni.Infrastructure/Finance/CommerceService.cs)
- [src/Adeni.Infrastructure/Finance/CustomerGiftCardQuery.cs](../../src/Adeni.Infrastructure/Finance/CustomerGiftCardQuery.cs)
- [src/Adeni.Infrastructure/Finance/FinanceServiceCollectionExtensions.cs](../../src/Adeni.Infrastructure/Finance/FinanceServiceCollectionExtensions.cs)
- [src/Adeni.Infrastructure/Finance/GiftCardReportQuery.cs](../../src/Adeni.Infrastructure/Finance/GiftCardReportQuery.cs)
- [src/Adeni.Infrastructure/Finance/PaymentOrchestrator.cs](../../src/Adeni.Infrastructure/Finance/PaymentOrchestrator.cs)
- [src/Adeni.Infrastructure/Identity/FinanceCommerceAccessQuery.cs](../../src/Adeni.Infrastructure/Identity/FinanceCommerceAccessQuery.cs)
- [src/Adeni.Infrastructure/Identity/IdentityServiceCollectionExtensions.cs](../../src/Adeni.Infrastructure/Identity/IdentityServiceCollectionExtensions.cs)
- [src/Adeni.Infrastructure/Logging/PiiDestructuringPolicy.cs](../../src/Adeni.Infrastructure/Logging/PiiDestructuringPolicy.cs)
- [src/Adeni.Infrastructure/Persistence/AdeniDbContext.cs](../../src/Adeni.Infrastructure/Persistence/AdeniDbContext.cs)
- [src/Adeni.Infrastructure/Persistence/CommerceModelConfiguration.cs](../../src/Adeni.Infrastructure/Persistence/CommerceModelConfiguration.cs)
- [src/Adeni.Infrastructure/Reporting/ReportExportService.cs](../../src/Adeni.Infrastructure/Reporting/ReportExportService.cs)
- [src/Adeni.Infrastructure/Tenancy/TenancyServiceCollectionExtensions.cs](../../src/Adeni.Infrastructure/Tenancy/TenancyServiceCollectionExtensions.cs)
- [tests/Adeni.Api.Tests/Integration/CommerceIntegrationTests.cs](../../tests/Adeni.Api.Tests/Integration/CommerceIntegrationTests.cs)
- [tests/Adeni.Application.Tests/Tenancy/CustomerGiftCardsQueryServiceTests.cs](../../tests/Adeni.Application.Tests/Tenancy/CustomerGiftCardsQueryServiceTests.cs)
- [tests/Adeni.Infrastructure.Tests/Finance/CommerceCredentialTests.cs](../../tests/Adeni.Infrastructure.Tests/Finance/CommerceCredentialTests.cs)
- [tests/Adeni.Infrastructure.Tests/Finance/CommerceServiceTests.cs](../../tests/Adeni.Infrastructure.Tests/Finance/CommerceServiceTests.cs)
- [tests/Adeni.Infrastructure.Tests/Finance/CommerceSqlServerTests.cs](../../tests/Adeni.Infrastructure.Tests/Finance/CommerceSqlServerTests.cs)
- [tests/Adeni.Infrastructure.Tests/Finance/CommerceTestSupport.cs](../../tests/Adeni.Infrastructure.Tests/Finance/CommerceTestSupport.cs)
