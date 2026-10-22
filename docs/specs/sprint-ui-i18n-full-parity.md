# Spec: Full UI language-switch parity (portal, discover, admin, `@adeni/ui`)

> Copied from [`_template.md`](./_template.md). Standing play 1 in [AGENTS.md](../../AGENTS.md).

| Field | Value |
|-------|-------|
| **Sprint** | UI i18n — full parity (cross-cutting, no new sprint number) |
| **Author** | Cursor agent |
| **Status** | In progress (Phase 0 + mechanical sweep done; manual tail tracked in §9) |
| **Created** | 2026-10-22 |

---

## 1. Goal (one sentence)

Switching language (EN ⇄ FR) updates **every** user-visible string on every page of portal, discover, admin and shared `@adeni/ui` — including API error messages and toasts — with no hardcoded English left in TS/HTML except dev-gated diagnostics.

---

## 2. Context

- Prior audit: agent chat `3836b4e1` (AdeniLocaleService + `t()` exist; API client defaulted to `en`; `error.set("…")` / `feedback.*("…")` hardcoded everywhere).
- Existing pieces reused: `AdeniLocaleService` (`@adeni/ui`), `t()` / `localizeErrorResponse` (`@adeni/shared`), discover `label()` pattern, portal/admin shell `CHROME` maps (left as-is — already bilingual).
- Constraints: keys live in `@adeni/shared` ([AGENTS.md shared packages](../../AGENTS.md)); no `ngx-translate`; minimal per-file diffs.

---

## 3. In scope

- [x] **Phase 0 plumbing**
  - `AdeniApiClientOptions.locale` accepts `string | (() => string)`; `BusinessApiService`, `CustomerApiService`, `AdminApiService` pass `() => AdeniLocaleService.locale()` so language changes apply to already-created clients.
  - `AdeniApiError.params` (RFC 7807 `params`) alongside `code`.
  - `resolveApiErrorMessage(locale, err, fallbackKey, vars?)` in `@adeni/shared` (`api-errors.ts`) + unit tests: API `code`/`params` → `errors.*`; anything else → localized fallback key (server English text is never surfaced).
  - `localizeErrorResponse` last-resort string is now `toast.somethingWrongShort` (localized).
  - `AdeniFeedbackService` injects `AdeniLocaleService` (`optional`) — default toast titles / loading label+detail are keys (`toast.*`) resolved reactively; `AdeniToastHostComponent` dismiss `aria-label` localized.
  - `tenantReportExportErrorMessage("en", …)` call sites (bookings, payments ×2) replaced by `apiErr(err, key)`.
  - Complete `errors.*` for every `ErrorCodes.cs` constant in **en and fr** (queue, messaging, entitlements, reporting were missing in fr); guarded by `parity.test.ts`.
- [x] **Component helpers** (`@adeni/ui`): `injectAdeniTranslate()` → `protected readonly tr = …` and `injectAdeniApiError()` → `protected readonly apiErr = …`. Templates use `{{ tr('portal.page.key') }}` / `[title]="tr('…')"`; catch blocks use `this.apiErr(err, 'key')`.
- [x] **Mechanical sweep** (AST-light codemod, reviewed by typecheck + build):
  - static template text nodes, `title|description|label|placeholder|aria-label|…` attributes, uppercase string literals inside `{{ }}` / `[binding]`
  - `error.set(…)`, `*Message.set(…)`, `feedback.success|error|info(…)` literals in components/services
  - ~1,270 unique strings → `common.*`, `portal.<page>.*`, `admin.<page>.*`, `discover.<page>.*`, `ui.*` in `messages/catalog-en.ts` + `catalog-fr.ts`
- [ ] **Manual tail** (see §9): interpolated sentences, class-field arrays (columns/tabs) that need `computed()`, route titles, formatters, `new Error("…")` messages, a few multi-line `error.set(` calls.

## 4. Out of scope

- Server-side English `title`/`detail` text (API remains English; clients map `code` → `errors.*`).
- Third-party locales beyond `en` / `fr`.
- Developer-only diagnostics (kept English, documented below): dev Auth0-sub hints, `admin-load-error` diagnostics, `console.*`, thrown `Error` messages that are never shown (replaced by `apiErr` fallbacks when displayed).
- Content translation of user-generated content (existing `content-translation.ts` feature).

---

## 5. API contract

No HTTP contract change. Client-side contract:

| Piece | Shape |
|-------|-------|
| `AdeniApiClientOptions.locale` | `string \| (() => string)` (default `"en"`) |
| `AdeniApiError` | `new AdeniApiError(message, statusCode?, code?, params?)` |
| `resolveApiErrorMessage` | `(locale, error: unknown, fallbackKey = "toast.somethingWrong", vars?) => string` |

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| `@adeni/shared` | `api-errors.ts`, `i18n/types.ts` (`mergeMessageTrees`), `i18n/errors.ts`, `i18n/messages/{en,fr,extras-en,extras-fr,catalog-en,catalog-fr}.ts`, tests |
| `@adeni/api-client` | `index.ts` |
| `@adeni/ui` | `locale/adeni-i18n.ts`, `feedback/*`, list pagination/toolbar/modal/data-table, `index.ts` |
| Portal / Admin / Discover | `core/services/*-api.service.ts`, every page/layout/shared component with static copy |
| Tests | `api-errors.test.ts`, `i18n/messages/parity.test.ts` (en/fr key parity, `{placeholder}` parity, every `ErrorCodes.cs` code has en+fr) |

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation / auth / cache / audit** | None |
| **PII** | None — messages never include request payloads |
| **Observability** | `AdeniApiError.code` / `params` available for logging |

---

## 8. Tests

- [x] Unit: `resolveApiErrorMessage`, `mergeMessageTrees`, en/fr parity, `ErrorCodes.cs` ↔ `errors.*` coverage (`packages/shared`: `npx vitest run`)
- [x] Build/typecheck: `ng build --configuration development` for portal, admin, discover; `tsc --noEmit` for `packages/ui`
- [ ] Manual smoke: switch to FR on portal dashboard, bookings, profile; admin dashboard, pricing; discover home, business, my-bookings

## 9. Acceptance criteria / remaining gaps

- [x] API client uses the active locale (reactive getter).
- [x] fr parity for all `ErrorCodes.cs` codes and for every new key (enforced by test).
- [ ] No user-facing English hardcoded in TS/HTML except dev-gated diagnostics — **see status report in the PR/commit notes for the remaining manual tail**; run
  `rg -n "error\.set\(\s*[\"'\`]|feedback\.(success|error|info)\(\s*[\"'\`]"` and the template scanners used in this PR to re-check.

Dev-only English (intentional): portal staff-invite dev hint, `accept-invite` dev Auth0-sub field label, admin `setup` page (Auth0 configuration instructions for engineers), `Local dev session` labels.

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| Per-page key files vs one catalog? | Generated `catalog-{en,fr}.ts` (grouped by app/page) + hand-written `extras-*.ts`; `mergeMessageTrees` throws on duplicate keys. |
| Pipe vs method in templates? | Method (`tr()`) — no extra `imports` entry per component; reads the locale signal so it is reactive. |
