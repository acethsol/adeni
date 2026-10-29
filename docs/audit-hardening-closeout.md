# Audit hardening closeout

**Status: Phase 1–6 code/tests/documentation completed; NOT fully production-build-green.** No merge, branch promotion, production deployment, outbox implementation, schema change, dependency addition or budget increase was performed.

## A. Audit scope

Repository `acethsol/adeni`; branch `chore/audit-hardening-recommendations`; comparison base `dev` at `0cfecfc75417f72257ec89478aa0168c4a594960`. V1 authority is [product-direction.md](product-direction.md): beauty/wellness, Ottawa and Lagos, Angular web, native Flutter mobile direction, SQL Server and one .NET modular monolith. Historical sprint decisions remain labelled as history.

## B. Phase summary

| Phase | Completed work |
|---|---|
| 1 (`731d74a`) | Reject stub production payment/billing providers and logging-only notification fallback; required sender config; remove WhatsApp-to-SMS dispatch; tenant-specific membership check. |
| 2 (`8ed90a9`) | Explicit proxy trust and bounded forwarding before scheme/IP consumers; selected-provider startup checks; Production Local storage guard. |
| 3 (`1aad1b2`) | Shared CSV quoting/formula protection, typed numeric preservation, ClosedXML safety regression coverage; schemas/layout unchanged. |
| 4 (`096e485`) | Six business controllers lose DbContext injection; Finance access and location scope use Application ports. Health connectivity probe retained. |
| 5 (`794d455`) | API transport + 13 internal domains behind compatible root facade; Dashboard request service; shell navigation/search/copy extraction; lifecycle tests. |
| 6 (this commit) | Cross-layer entitlement parity tests, billing boundary/reliability characterization tests, current-versus-archive docs, qa CI triggers and Portal unit-test gate, promotion guidance and deferred outbox design. |

## C. Security improvements

Production-like environments fail before startup when required selected-provider configuration is absent. Forwarded IP/scheme handling is opt-in with explicit trusted hops; no trust-all network default or raw-header rate-limit partitioning. Production local media storage is rejected. CSV text is quoted and formula-prefixed centrally; typed negative numbers remain numeric. Backend membership, permissions, entitlements and tenant filters remain authoritative. WhatsApp remains existing `wa.me` links only.

These checks validate configuration structure, not live credentials, origin access restrictions, database connectivity or provider delivery. Secrets and deployment network controls still require verification.

## D. Architecture and entitlement parity

Finance owns payment access decisions through Application contracts; Identity/Booking supply read evidence. Tenancy exposes existing location rules without moving HTTP error mapping into services. No generic repository, MediatR rollout, cross-module Infrastructure implementation references or new Domain/Application Infrastructure dependencies were introduced.

Frontend public imports stay stable. All 192 API method bodies were compared with their originals, and old/new public types were checked in both directions. Templates, styles, routes, Discover SSR/hydration/TransferState/PendingTasks and realtime connection ownership are unchanged. Signals remain local/computed state; RxJS subscriptions retain lifecycle cleanup.

The catalogue has **25 seed fields and three SKUs** (`starter`, `professional`, `business`). The .NET API adds five deliberate fields. JSON remains the shared catalogue; backend services apply `TenantBillingPolicy` and enforce mutations. Frontend catalogue/marketing values do not establish authorization or purchased access.

| Field | Existing derivation / decision |
|---|---|
| `InvoicesReceipts` | Intentionally universal V1 finance access; no evidence to make it plan-configurable now. |
| `OnlinePayments` | Intentionally universal entitlement; selected market/provider readiness still determines operational availability. |
| `Deposits` | Universal compatibility flag, not computed from a capability. Workflow gating should use the separate business capability (`deposits`), not treat this flag as a purchased tier switch. |
| `AdvancedReminders` | Compatibility OR of configurable reminders and follow-up automation. |
| `MultiLocation` | Capacity compatibility flag from unlimited or >1 maximum locations; distinct from purchased cross-location reporting. |

No entitlement values, billing policy or enforcement behavior changed. Active future trials use trial SKU (default Professional); suspended/canceled tenants fall back to Starter; expired trials and other statuses retain stored SubscriptionSku under the existing policy. New tests cover the exact trial expiry boundary. Any future plan-configurable finance feature needs a coordinated catalogue/API/backend gate change, not a frontend-only toggle.

Parity checks anchor TypeScript seed keys, supported SKU types, Zod API field types and .NET serialized names/types/values to the same JSON. Negative tests reject missing fields/rows, renamed fields, wrong types, unsupported SKUs and invalid limits; nullable limits are supported. The unchecked TS catalogue cast became an equivalent public type annotation, with no runtime change.

## E. Validation and final comparison

| Check | Final local result |
|---|---|
| `.NET Release build` | Passed; 0 warnings, 0 errors, including Database Project. |
| `.NET full suite` | **561 passed, 2 skipped** (opt-in real SQL Server tests). |
| Workspace `npm run typecheck` | Passed: shared, api-client, UI/brand and Portal/Admin/Discover development builds, including Discover server bundle. |
| Shared Vitest | **73 passed**, 15 files. |
| Portal Jasmine/Chrome | **15 passed**, including API-client transport/public imports, data loading, gates, Signals and realtime cleanup. |
| Portal production | Failed: 3 component stylesheet budgets. |
| Admin production | Failed: 14 component stylesheet budgets **and initial JS bundle budget**. |
| Discover production | Failed: shared data-grid component stylesheet budget. |

Commands (repository root; backend uses the installed `/workspace/dotnet/dotnet` with workspace CLI home/NuGet/temp settings):

```sh
dotnet build Adeni.slnx -c Release
dotnet test Adeni.slnx -c Release
npm run typecheck
npm test --workspace @adeni/shared
CHROME_BIN=/tmp/adeni-chrome npm test --workspace @adeni/business
npm run build:business
npm run build:admin
npm run build:discover
```

`/tmp/adeni-chrome` runs system Chromium with `--no-sandbox` for this execution environment only; no application/browser deployment setting changed. Phase 5 ran `npm ci --cache /workspace/npm-cache`; dependency manifests/lockfile remain unchanged. Validation used Node 24.19.0; hosted CI is configured for Node 22.22.3. UI/api-client have typecheck scripts rather than separate test runners; their public compatibility is exercised by Portal tests. The frontend CI job now runs the existing Portal unit-test script as well as shared tests. Hosted SQL Server tests and NuGet/npm vulnerability gates still require green CI before promotion.

Final branch comparison: **95 files changed; 25 test files new/updated; 185 net additional test cases** (164 xUnit, 14 Portal, 7 shared). xUnit attribute counts match discovery totals: dev 399 versus branch 563, each including the same two SQL skips. Final local executed totals are 649 passed plus 2 skipped across backend/shared/Portal. Most Phase 5 churn is moved API code, not new endpoints. No dependency manifest, SQL/migration, API route or permission-attribute changes were found in the audit diff.

## F. Known production blockers: build budgets

All fatal stylesheet sizes below reproduce identically on the untouched frontend at Phase 4 (`096e485`), which predates the frontend refactor. No stylesheet or budget configuration changed in this branch. The Admin **initial bundle is also 2.16 MB against a 2.00 MB error limit** (Phase 4 baseline 2.54 MB); the Phase 5 summary omitted this additional blocker. Portal initial bundle is now 2.96 MB, below its 3 MB error threshold but above its 2.5 MB warning threshold. Discover initial bundle is 1.42 MB, above its 1.2 MB warning threshold but below its 3 MB error threshold.

| App | Fatal stylesheet (relative to its app unless shared) | Error budget | Actual compiled size |
|---|---|---|---|
| Admin | `src/app/pages/audit-logs/audit-logs.component.scss` | 24.00 kB | 25.39 kB |
| Admin | `src/app/pages/businesses/business-billing.component.scss` | 24.00 kB | 32.74 kB |
| Admin | `src/app/pages/businesses/business-commercial-audit.component.scss` | 24.00 kB | 32.83 kB |
| Admin | `src/app/pages/businesses/business-overview.component.scss` | 24.00 kB | 37.71 kB |
| Admin | `src/app/pages/businesses/business-subscription-wizard.component.scss` | 24.00 kB | 27.61 kB |
| Admin | `src/app/pages/businesses/businesses.component.scss` | 24.00 kB | 25.25 kB |
| Admin | `src/app/pages/customers/customers.component.scss` | 24.00 kB | 25.75 kB |
| Admin | `src/app/pages/markets/markets.component.scss` | 24.00 kB | 25.38 kB |
| Admin | `src/app/pages/pending/pending.component.scss` | 24.00 kB | 25.25 kB |
| Admin | `src/app/pages/pricing/pricing-catalog.component.scss` | 24.00 kB | 26.73 kB |
| Admin | `src/app/pages/pricing/pricing-entitlements.component.scss` | 24.00 kB | 25.39 kB |
| Admin | `src/app/pages/reviews/reviews.component.scss` | 24.00 kB | 25.61 kB |
| Admin | `src/app/pages/transactions/transactions.component.scss` | 24.00 kB | 25.45 kB |
| Admin | `src/app/pages/trust-badges/trust-badges.component.scss` | 24.00 kB | 25.25 kB |
| Discover | `../../packages/ui/src/styles/_data-grid.scss` | 14.00 kB | 16.90 kB |
| Portal | `src/app/pages/plan/plan.component.scss` | 24.00 kB | 28.33 kB |
| Portal | `src/app/pages/services/services.component.scss` | 24.00 kB | 24.50 kB |
| Portal | `src/app/pages/staff/staff.component.scss` | 24.00 kB | 27.41 kB |

Portal/Admin component warning thresholds are 14 kB; Discover's is 6 kB. These are emitted CSS sizes, not SCSS source bytes. Shared `_data-grid.scss` is installed as `styleUrl` on ten UI components. Page styles also `@use` emitting `staff-layout`, `data-grid`, `admin-list-page` and dashboard/detail helpers. Sass deduplication is within a compilation unit, not across Angular component scopes. The evidence points to shared/global style ownership plus component growth against configured thresholds, **not a verified Carbon vendor defect**. Carbon components are also loaded globally. Admin's initial bundle needs a separate dependency/route-size profile.

No isolated dead selector was proven safe to remove. Proposed separate work: split component-owned grid styles from shared foundations, review emitted selector scope and duplicate page imports, then compare visual snapshots and production sizes across all three apps. Avoid blindly hoisting scoped rules or raising budgets; both can mask regressions.

## G. Accepted audit-scope risks and deferred debt

- **Accepted only for this audit's scope; release acceptance still required:** existing post-save in-process delivery can fail after persistence, lose work on a crash, or partially deliver. The new characterization test makes the limitation explicit. [Transactional outbox design](transactional-outbox-design.md) covers a SQL-backed future implementation; no outbox ships here.
- **Deferred:** HealthController's diagnostic service resolution, local upload signature adapter and verification enum mapping still reference Infrastructure. No business controller injects DbContext after Phase 4.
- **Deferred:** Staff/calendar container decomposition, remaining Dashboard presentation/layout complexity, and declarative shared schemas. No new global state architecture is needed.
- **Deployment assumption:** IP rate limits remain per-process; trusted proxy topology and aggregate edge protection must be configured for multi-instance deployments.
- **Compatibility assumption:** CSV consumers must retain formula-protection prefixes before spreadsheet import. XLSX timestamps intentionally remain ISO text to preserve offsets.
- **Deferred:** stale deferred-feature notes in the admin plan matrix are UI copy, not entitlement authority; changing that product copy is outside this closeout.

## H. Production-readiness checklist

- [ ] Resolve all three apps' production budget failures and rerun required CI; do not hide them with larger limits.
- [ ] Run the SQL Server CI job plus NuGet/npm dependency/security gates and final Node 22.22.3 CI checks.
- [ ] Configure reviewed `dev` → `qa` → `main` PR promotion and branch/environment protections; verify, rather than assume, the settings exist.
- [ ] Implement/review Azure artifact deployment and rollback workflow; current CI validates but does not deploy.
- [ ] Verify SQL/Redis/Translator/Key Vault and selected payment/billing, Resend/Twilio and Blob configuration; smoke-test provider webhooks, delivery and public media access.
- [ ] Verify actual trusted proxy hops, origin restrictions, HTTPS, CORS, Auth0 audience/MFA and spoofed-header denial in staging.
- [ ] Reconcile persisted payment/booking outcomes after post-save failures; obtain explicit release-owner acceptance or prioritize durable outbox migration before relying on automated recovery.

## I. Recommended engineering priorities and promotion

1. Separate reviewed build-size work: shared/component CSS ownership plus Admin bundle profiling; retain visual behavior and budget enforcement.
2. Green hosted CI and real-provider staging smoke tests; configure protections and immutable artifact promotion/rollback.
3. DB-backed outbox for critical commerce and booking-triggered notifications, with idempotent consumers and recovery observability, following the design note.
4. Only then resume planned product work and selectively decompose remaining hotspots.

Review the audit PR against `dev`; resolve blockers in separately reviewed changes, integrate and rerun the complete checks before merge. Promote by PR from `dev` to `qa` for staging and from validated `qa` to `main` for production. No direct production commits, force-push workaround, frontend-check waiver or merge/promotion is part of this audit.
