# Spec: Sprint C1 — Consumer app experience (authenticated customer surface in Discover)

> Copied from [`_template.md`](./_template.md). Standing play 1 (spec write) — **planning only; no application code in this change.**

| Field | Value |
|-------|-------|
| **Sprint** | Sprint C1 — Consumer app experience |
| **Author** | Aceth (agent-assisted inspection, 2026-10-29) |
| **Status** | Implemented on `dev` (Phases A–F client closeout; Auth0 real-tenant return-path smoke still required before launch) |
| **Created** | 2026-10-29 |
| **Updated** | 2026-10-30 |
| **Baseline** | `dev` @ `5900b4a` (customer shell `3e806b2`, my-packages/gift-cards polish `5900b4a`, 37.2-A `e124630`; 37.2-B working tree staged, not part of this spec) |

---

## 1. Goal (one sentence)

Make Adeni feel like **one consumer product with two surfaces** — a public, SEO-first **Discover** marketplace and a signed-in **customer app** under `/app/*` that behaves like a consumer edition of the Business Portal — by evolving `apps/customer` (no new Angular app) and shipping an information architecture that maps 1:1 to the future Flutter consumer app.

---

## 2. Context

- **Authority:** [product-direction.md](../product-direction.md) (Beauty & Wellness, Ottawa + Lagos; consumer Flutter is the primary mobile focus; Carbon for patterns, Adeni brand via `@adeni/brand`). Topology: [target-client-architecture.md](../target-client-architecture.md) — Discover Web stays SEO/public; this spec adds an *authenticated application surface* inside it, not a fifth client.
- **Sprint placement:** [sprints.md](../sprints.md) — not a numbered platform sprint; a **client-experience track (C1)** that consumes shipped APIs (Sprint 7 bookings, 36 messaging, F2 intake, F3 gift cards, 37.1 packages; 37.2-D memberships later). Does not displace 33/34/37.x backend work.
- **Related specs:** [f2-intake-consultation-forms-plan.md](./f2-intake-consultation-forms-plan.md) (private route, `RenderMode.Client`, no TransferState), [f3-gift-cards-pay-links-closeout.md](./f3-gift-cards-pay-links-closeout.md), [sprint-37-1-packages-benefit-ledger.md](./sprint-37-1-packages-benefit-ledger.md), [sprint-37-2-memberships.md](./sprint-37-2-memberships.md) (slice D), [sprint-36-messaging-rewrite.md](./sprint-36-messaging-rewrite.md), [sprint-38-wellness-service-quotes.md](./sprint-38-wellness-service-quotes.md) (`/my-quotes` unbuilt), [flutter-business-mobile-skeleton.md](./flutter-business-mobile-skeleton.md) (business only — not the consumer app).
- **Modules touched (client only):** Discover app, `@adeni/ui`, `@adeni/shared` (i18n keys, nav constants), `@adeni/api-client` (additive methods only if a gap in §9 is accepted). **No API/Domain/SQL change in C1 phases A–F** except optional additive fields called out in §9.4.
- **F1–F5 interpretation** (feature tracks in the codebase, from closeout docs): F1 customer profile/activity (**staff-side**, `/tenant/customers/{id}/profile` — not consumer-visible), F2 intake forms (customer fills at `/my-bookings/:id/intake`), F3 gift cards + Pay Links (customer `/me/gift-cards`), F4 calendar + "book with Adeni" (`/book/:slug` redirect, `?service=&practitioner=` preselection), F5 smart segments (**staff-side**). Only F2, F3, F4 touch the consumer surface.
- **Message truncation:** the originating request ended at "UP…". Inferred as **upcoming** appointments + **benefits/packages/gift cards/memberships** (F3, 37.1, 37.2-D); this spec treats both as first-class (Home "Up next", Benefits hub).

---

## 3. Current state assessment

### 3.1 What exists (verified in repo)

| Area | Today | Notes |
|------|-------|-------|
| **Apps** | `apps/customer` (Angular SSR, :5190), `apps/business`, `apps/admin`, `apps/mobile` (business Flutter skeleton). No consumer Flutter yet. | Consumer Flutter is "planned after web GA" (ADR-012) but product-direction says primary mobile focus. |
| **Routes** (`app.routes.ts`) | `CustomerShellComponent` group: `my-bookings`, `my-bookings/:id/intake[/:assignmentId]`, `my-packages`, `my-gift-cards`, `my-messages` (all `customerAuthGuard`). `PublicShellComponent` group: `/`, `/discover`, `/businesses/:slug`, `/documents/:token`, `/q/:slug`, `/privacy`, `/terms`, `/plan`, SEO market routes (`:marketId[/…]` via `seoMarketCanMatch`). Standalone: `/pay`, `/checkout/stub/:ref`. Wildcard `** → ""`. | Shell split already exists (commit `3e806b2`) — C1 builds on it rather than replacing it. |
| **Customer shell** | `layout/customer-shell.component.*` — sticky top header, brand lockup, text-link nav (My bookings, My packages, Gift cards, Messages), "Explore" tertiary button, Log out; `main` max-width 72rem; **mobile = nav wraps below header as horizontal scroller**. No icons, no tab bar, no account menu, no unread badge, no locale/theme control. | Uses `--cds-*` and brand vars; no `@adeni/ui` staff chrome. |
| **Page styling** | `layout/_customer-page.scss` (gradient "wash" page head, list cards, illustrated empty state) used by my-packages and gift cards; my-bookings and my-messages have **their own** 8–12 kB SCSS with similar-but-diverging rules. | Reads as marketplace/consumer marketing polish, not portal-like. |
| **Auth** | `customerAuthGuard` (Auth0 or dev sub `devCustomerAuth0Sub`), `CustomerApiService.withAuthorizedClient` (bearer or `X-Dev-Auth0-Sub`). Guard return-path allowlist is hard-coded to `/my-bookings/**/intake`, `/my-gift-cards`, else `/my-bookings`. Login passes `appState: { target }`. | **`appState.target` is never consumed** — repo-wide search finds no `appState$` / `handleRedirectCallback` usage; redirect URI is `window.location.origin`. Dev bypass hides this locally. Must be verified on a real Auth0 tenant (Phase A spike). |
| **SSR render modes** (`app.routes.server.ts`) | `Client` + `no-store, private` + `noindex` for `/pay`, `/my-packages`, `/my-gift-cards`, `/my-bookings/:id/intake/**`. **`/my-bookings` and `/my-messages` fall into the `**` → `Server` rule** (SSR'd shell, guard cannot succeed on server). | Inconsistent; `robots.txt` disallows only `/my-bookings`, `/my-messages` (not packages/gift-cards). |
| **Public shell** | Header nav: Explore, **My bookings, Messages**, For businesses; Sign in / Get started / Log out; scroll-pinned compact search on home; kiosk layout for `/q/…?kiosk=1`; marketing footer (with `/my-bookings` link). | Mixes public marketing nav with app links — the boundary C1 must separate. |
| **Customer features shipped** | **Bookings** (list upcoming/past, cancel, intake link, business link; status tones). **Intake** (statuses + submit, `IntakeFieldsComponent` from `@adeni/ui`). **Packages** (read-only balances + meter). **Gift cards** (purchased; masked code suffix; on-demand reveal; no wallet notice). **Messages** (conversation list + thread, SignalR via `AdeniMessagingRealtimeService`, attachments preview, mobile list/thread toggle at 720px). Booking panel (`shared/booking-panel`), package storefront (`business-packages`), quote request panel. | |
| **Portal patterns worth mirroring** | `PortalShellComponent`: sidebar (grouped nav, collapsible rail, badges via `badgeCount(item)`), top bar (breadcrumb, search, messages shortcut, pending bell, theme toggle, settings menu with appearance + language, account menu with identity + sign-out), `portal-nav.ts` config-driven nav (`PORTAL_NAV_GROUPS`, `permission`, `capability`, `entitlement`, `tabs`), `PortalThemeService`, 900px breakpoint → overlay sidebar. | Nav-as-data is the pattern to copy; sidebar/rail is not (consumer wants top nav + bottom tabs). |
| **`@adeni/ui`** | `PortalPageComponent` (`app-portal-page`), `AdeniTabsComponent` (`path`/`query`/`button`), `AdeniPortalEmptyStateComponent`, `AdeniPillComponent`, `AdeniStaffButtonComponent`, `AdeniCarbonIconComponent` (**no** home / chat / gift / settings / menu glyphs — list is a curated subset), `AdeniBrandLockupComponent`/`AdeniBrandLogoComponent`, `AdeniStaffAppContextComponent` (`"portal"`\|`"admin"` only), feedback hosts (toast/confirm/global loading), `AdeniModalComponent`, locale + i18n helpers, realtime services. Data-grid kit (not needed for consumer lists). | `app-portal-page` and `adeni-staff-*` names are staff-flavoured but not staff-locked. |
| **`@adeni/shared`** | `CustomerBookingResponse`, `CustomerPackage`, `BusinessGiftCard`, `ConversationSummary`/`needsAttention`, intake schemas, `formatSlotTime`, `formatPrice`, `bookingPreselectionQuery`, i18n catalogs EN/FR (+ parity tests), `AuthSession`. | No consumer nav constants yet. |
| **Budgets** | `anyComponentStyle` error 14 kB; `npm run build:customer` **already fails** on shared `_data-grid.scss` 16.9 kB (F1 closeout). | New SCSS must live in a global partial, not per-component copies (§6.3). |
| **Tests** | Discover has `app.spec`, `intake`, `pay`, `booking-preselection`, `seo-jsonld`, `booking-link-handler` specs. Portal has route-compat specs (`customer-route-compatibility.spec.ts`) — precedent for route contract tests. | No shell, guard, or redirect tests in Discover. |

### 3.2 Gaps (what the consumer cannot do / see today)

1. **No concept of "the app"** — signed-in pages are separate top-level URLs (`/my-*`) sharing the public host with no path-level boundary (affects robots, cache headers, analytics, deep links, Flutter mapping).
2. **No Home** — post-login lands on `/` (public home) or `/my-bookings`; no "Up next", no cross-feature summary.
3. **Nav inconsistency** — public shell and customer shell each hard-code overlapping links; Packages and Gift cards are peers of Bookings instead of grouped under Benefits.
4. **No mobile-native navigation** — header links wrap; no tab bar, no safe-area handling, no unread badge.
5. **No profile/account page** — only Log out. `GET /auth/me` returns `userId, roles, tenantId, hasMfa` (no name/email); there is no customer profile or notification-preference API (tenant notification prefs exist only under `/tenant/…`).
6. **Dead login return path** (§3.1) and hard-coded guard allowlist.
7. **Reviews:** API (`createBookingReview`) and fields (`canReview`, `hasReview`, `reviewRating`) exist; **no Discover UI**.
8. **Customer quotes:** `listCustomerQuotes` / `acceptQuote` / `declineQuote` exist in api-client; **no Discover `/my-quotes`** (planned in Sprint 38 spec, unbuilt).
9. **No reschedule** (no API) and **no "get booking by id"** for customers (list only).
10. **Messages realtime lifecycle** is page-scoped (`ngOnDestroy → realtime.stop()`), so a shell-level unread badge needs a lifted service.
11. **Styling drift** — four customer pages, three SCSS dialects; `_customer-page.scss` gradient washes diverge from portal's flat Carbon surfaces.
12. **Packages/gift-card rows lack `businessSlug`** → cannot deep-link "Book / Use credits" (`CustomerPackage.businessName` only; `BusinessGiftCard.businessName` only).
13. **No memberships on the consumer side** (37.2-D draft: `/api/v1/me/retention/memberships` not built).

---

## 4. In scope

- [ ] **Authenticated application surface** under **`/app/*`** inside `apps/customer`, with its own shell, route tree, SCSS boundary, SSR policy and robots policy.
- [ ] **Public surface unchanged:** all existing public URLs, canonical tags, sitemap, structured data and SSR behaviour (`/`, `/discover`, `/businesses/:slug`, market SEO routes, `/privacy`, `/terms`, `/plan`, `/documents/:token`, `/q/:slug`, `/pay`, `/checkout/stub/*`, `/book/:slug`).
- [ ] **Shell:** desktop portal-like top bar + primary nav; mobile bottom tab bar (Home · Discover · Bookings · Messages · Profile); Benefits reachable from Home and Profile (and desktop primary nav).
- [ ] **Customer Home** built only from APIs that exist today (§8).
- [ ] **Benefits hub** (`/app/benefits`): packages (37.1) and purchased gift cards (F3) now; memberships tab appears only when 37.2-D ships (§9).
- [ ] **Pre-launch route cutover:** delete legacy `/my-*` customer URLs (no parallel route trees, **no** Express/Angular redirects). Authenticated customer pages live only under `/app/*`; internal links use `/app/*` exclusively (§7.2).
- [ ] **Profile/Account** page (read-only identity from Auth0 claims, language, sign-out, legal links) — no new API.
- [ ] **Discover-in-app** tab (`/app/discover`): search + featured entry that hands off to the **canonical public** business page (§6.4).
- [ ] **Auth return-path fix** (honor `appState.target` with same-origin allowlist).
- [ ] **Styling consolidation:** replace per-page customer SCSS with an `app-surface` partial built from Carbon tokens + `@adeni/ui` components.
- [ ] **Tests** (route contract, guard, shell, Home composition, EN/FR parity) per §13.
- [ ] **Flutter parity doc** — IA ↔ screens/API table (§11) kept in this spec; no Flutter code.
- [ ] **Docs:** `frontend.md` + `target-client-architecture.md` one-paragraph updates in Phase F; `sprints.md` C1 row (this change).

## 5. Out of scope (do not build in C1)

- A **separate Angular consumer app** or new deployable (re-evaluate only if §12 risk R1 materialises).
- Flutter consumer app implementation (only parity mapping).
- **Duplicating public pages under `/app`** (no `/app/businesses/:slug`; booking stays on the canonical public URL) — see §6.4.
- Marketplace redesign of public pages; portal-ifying `/`, `/discover`, `/businesses/*`.
- New backend modules, tables, endpoints (except the optional additive fields in §9.4, each requiring its own approval).
- Reschedule, favorites/saved businesses, loyalty points (37.3+), customer notification preferences, profile editing, avatar upload, payment-methods management, wallet/balance (**never** — AGENTS out-of-scope).
- `/app/quotes` build-out (slot reserved; Sprint 38 owns it).
- Dark theme for the consumer app (token-ready; optional in F).
- Push notifications / FCM (Flutter track).
- Production deploy / observability (Sprint 20).

---

## 6. Architecture

### 6.1 Principle: two surfaces, one product

```
apps/customer (one Angular SSR deployable, one origin)
├── PUBLIC SURFACE      PublicShell  — SSR, SEO, marketplace look, footer, market context
│     /  /discover  /businesses/:slug  /:market…  /privacy /terms /plan  /documents/:t  /q/:slug
└── APP SURFACE         CustomerAppShell — CSR only, noindex, portal-like chrome, no footer
      /app/home  /app/discover  /app/bookings  /app/messages  /app/benefits  /app/profile
```

The boundary is enforced at four layers: **route tree** (separate `path: "app"` parent with own shell and guard), **render mode** (`app/**` = `RenderMode.Client`), **styles** (`.app-surface` scope, §6.3) and **robots** (`Disallow: /app/`).

### 6.2 Shell architecture — components to create vs reuse

| Component | Where | New / reuse | Responsibility |
|-----------|-------|-------------|----------------|
| `CustomerAppShellComponent` | `apps/customer/src/app/app-surface/shell/` | **New** (successor to `CustomerShellComponent`, which is deleted in Phase F) | Layout grid: top bar + `<router-outlet>` + mobile tab bar; hosts toast/confirm/loading hosts; sets `noindex`. |
| `customer-nav.ts` | `apps/customer/src/app/app-surface/customer-nav.ts` (promote constants to `@adeni/shared` only when Flutter parity tooling needs them) | **New** (mirror of `portal-nav.ts`) | Config-driven: `{ path, labelKey, icon, tab:boolean, desktop:boolean, badge?: "messages"\|"benefits", visibleWhen?: capability }`. Single source for desktop nav, tab bar, and search-jump. |
| `AppTopBar` (template part of shell) | same | **New**, styled like portal topbar | Brand lockup (`AdeniBrandLockupComponent`), desktop primary nav (Home, Discover, Bookings, Messages, Benefits), messages shortcut (badge), account menu (identity from claims, Profile, language, Sign out). |
| `AppTabBar` | same | **New** | 5 tabs: Home, Discover, Bookings, Messages, Profile. `<nav aria-label>`, `routerLinkActive`, `aria-current="page"`, badge on Messages. Hidden ≥ 900px. |
| `CustomerNavIcon` | **`@adeni/ui`** (extend `carbon-icon.ts` + `AdeniCarbonIconName`) | **Extend** | Add Carbon 20/16 glyphs: `home`, `search`, `chat`, `gift`, `user--avatar`(exists), `menu`, `settings`, `logout`, `notification`. Prefer extending the shared icon map over copying portal's local `PortalNavIconComponent`. |
| `CustomerAppSession` service | `app-surface/services/` | **New** | Signal store: signed-in state, claims (name/email/picture from Auth0 `user$`, in-memory only), `unreadMessages` signal, locale. Replaces duplicated `signedIn` logic in two shells. |
| `AuthReturnService` | `core/services/` | **New** | Subscribes to `AuthService.appState$` (browser only, app initializer); validates `target` against `^/(?!/)[A-Za-z0-9/_\-.~?=&%#]*$` + prefix allowlist (`/app/`, `/businesses/`, `/pay`, `/discover`); `router.navigateByUrl`. Replaces `customerAuthGuard`'s page-specific allowlist. |
| `CustomerMessagingBadgeService` | `app-surface/services/` | **New** | Starts SignalR once at shell scope (`AdeniMessagingRealtimeService`), exposes `needsAttentionCount`; **lifts** realtime start/stop out of `MyMessagesComponent` so leaving `/app/messages` doesn't kill the badge. |
| Page chrome | **Reuse** `PortalPageComponent` (`app-portal-page`: title, description, `[pageActions]` slot) | Reuse as-is; add `AdeniPageComponent` alias export only if naming review demands (non-blocking) | Replaces gradient `.page-head` + `.wash` on every page. |
| Tabs | **Reuse** `AdeniTabsComponent` (`linkMode="path"`) | Reuse | Bookings (Upcoming / Past), Benefits (Packages / Gift cards / Memberships). **Not** `cds-content-switcher`. |
| Empty states | **Reuse** `AdeniPortalEmptyStateComponent` | Reuse | Replace per-page `.empty` blocks. |
| Status | **Reuse** `AdeniPillComponent` | Reuse | Replace per-page `.status` chips (maps pending→warning, confirmed/active→success, rejected/cancelled/voided→neutral/danger). |
| Buttons | Carbon `.cds-btn` (existing) and/or `AdeniStaffButtonComponent` | Reuse | Keep existing consumer `.cds-btn` usage; adopt `AdeniStaffButtonComponent` only where icon+label combos duplicate. |
| Brand | `AdeniBrandLockupComponent` (full lockup ≥ 600px) / `AdeniBrandLogoComponent variant="mark"` (compact) | Reuse | Never CSS-recreate (AGENTS brand rule). |
| Context chip | `AdeniStaffAppContextComponent` | **Optional extend**: `app` input `"portal"\|"admin"\|"customer"` | Only if a "which app am I in" chip is wanted; not required. |

**Not reused from portal:** sidebar rail, branch switch, tenant search, permission/entitlement nav filtering (customers have no roles), `PortalShellComponent` itself (tenant-coupled).

### 6.3 Public vs app styling boundary

- **Scope class:** shell root carries `.adeni-app` (`<div class="shell adeni-app">`). All app-surface rules live in **one global partial** `apps/customer/src/styles/_app-surface.scss`, imported from `styles.scss`, selectors prefixed `.adeni-app …`. This avoids per-component duplication and the existing **14 kB `anyComponentStyle` error budget** (global styles count toward `initial`, not component styles).
- **Tokens (shared):** Carbon + Adeni tokens from `@adeni/ui/styles/_carbon-adeni-tokens.scss` (`--surface`, `--bg-subtle`, `--border`, `--interactive`, `--text`, `--text-muted`, `--cds-spacing-*`, `--cds-radius-none`). Both surfaces share tokens; they differ in **composition**:

| Concern | Public (marketplace) | App (consumer portal) |
|---------|----------------------|------------------------|
| Chrome | Marketing header with gradient accent, scroll-pinned search, footer, market tone | Flat top bar (1 px border, no gradient), no footer, bottom tabs on mobile |
| Page head | Imagery, hero, gradient washes | `app-portal-page` title + description + actions; **no wash/kicker** |
| Cards | Photo cards, shadows, hover lift | Flat `--surface` panels, 1 px `--border`, 0 radius, status via `adeni-pill`; left accent bar allowed for status only |
| Density | Spacious | Portal-like compact rhythm (`--cds-spacing-04/05`), 2.5 rem hit targets (≥ 44 px on touch) |
| Motion | Marketing transitions | Minimal; respect `prefers-reduced-motion` |
| Max width | Per page | `main` 72 rem; list pages 46–56 rem; messages full-bleed split |

- **Rules:** (1) app components must not import `public-shell` / `hero-*` / `discovery-*` SCSS; (2) shared presentational pieces (e.g. `DiscoveryBusinessCardComponent` reused inside `/app/discover`) are styled via inputs or a `surface="app"` variant, never by app CSS overriding public CSS; (3) `_customer-page.scss` is **frozen** at Phase A and deleted in Phase F once all pages use `app-portal-page` + `_app-surface.scss`; (4) new global partial reviewed against `build:customer` budget in every phase.
- **Promotion rule (AGENTS Shared packages):** extract to `@adeni/ui` only when the portal or admin would also use it; the tab bar and consumer nav stay in Discover until the Flutter/web parity story proves reuse.

### 6.4 Discover-in-app and public handoff

- `/app/discover` = search + categories + featured cards (reusing `DiscoverySearchComponent`, `DiscoveryBusinessCardComponent`, `MarketContextService`) inside the app shell. **Business cards link to the canonical public URL `/businesses/:slug`** (full navigation leaves the app shell). Rationale: single canonical, SSR/SEO booking surface; avoids duplicate-content, JSON-LD and booking-panel forks; keeps `?service=&practitioner=#book` and `/book/:slug` working unchanged.
- **Return affordance:** when `signedIn`, the **public shell header** replaces "My bookings / Messages" with an **Open app** button → `/app/home` (and shows account initials). The business page gets a "Back to app" breadcrumb **only** when navigated from `/app/*` (router state `{ fromApp: true }`; never affects SSR HTML).
- `/app/discover` is `noindex`; public `/discover` remains the SEO page. If product later wants in-shell booking, add `/app/businesses/:slug` with `rel=canonical → /businesses/:slug` as a separate spec (open question Q2).

### 6.5 Mobile nav and breakpoints

| Breakpoint | Behaviour |
|------------|-----------|
| **≥ 900 px** (matches portal's 900 px rail break) | Top bar with full primary nav (Home, Discover, Bookings, Messages, Benefits), account menu, no tab bar. |
| **600–899 px** | Top bar compact (mark logo + messages + account); **bottom tab bar visible**. |
| **< 600 px** | Same as above; page padding `--cds-spacing-04`; lists single column; messages shows list **or** thread (existing 720 px behaviour moves to 899 px to align with tab bar). |
| Tab bar | 5 equal tabs, 56 px + `env(safe-area-inset-bottom)`; label always visible (icon + text); active = `--interactive` + top 2 px indicator; badge dot/count on Messages; `main` gets `padding-bottom` = bar height. Hidden while the on-screen keyboard is open (detected via `visualViewport` resize; Phase E detail). |
| Benefits on mobile | Not a 6th tab: entry points = Home "Benefits" card + Profile list row + `/app/benefits` deep link; desktop nav shows it. |
| Intake/thread detail routes | Tab bar stays; `back` button in page chrome returns to parent route (mirrors Flutter back stack). |

---

## 7. Routing & redirect strategy

### 7.1 New route tree (`app.routes.ts`)

```
path: "app"                 component: CustomerAppShellComponent   canActivate: [customerAuthGuard]   data: { surface: "app" }
 ├─ ""                      redirectTo: "home"  (pathMatch: full)
 ├─ "home"                  → CustomerHomeComponent                (lazy)
 ├─ "discover"              → AppDiscoverComponent                 (lazy)
 ├─ "bookings"              → BookingsComponent                    (lazy; AdeniTabs path: upcoming|past via ?tab= query for deep link simplicity)
 │   └─ ":bookingId/intake[/:assignmentId]" → IntakeComponent      (existing component, lazy)
 ├─ "messages"              → MessagesComponent                    (lazy)
 │   └─ ":conversationId"   (same component; selection bound to route param → deep-linkable)
 ├─ "benefits"              → BenefitsHubComponent                 (lazy; AdeniTabs linkMode="path")
 │   ├─ ""                  redirectTo: "packages"
 │   ├─ "packages"          → existing MyPackagesComponent body
 │   ├─ "gift-cards"        → existing PurchasedGiftsComponent body
 │   └─ "memberships"       → (added in 37.2-D integration; route absent until then)
 ├─ "profile"               → CustomerProfileComponent             (lazy)
 └─ "**"                    redirectTo: "home"
(reserved, not built in C1: "quotes")
```

`path: "app"` must be declared **before** the market-SEO catch-alls; `"app"` is not a valid `isSeoMarketId` (markets are `lagos`, `ottawa`) so there is no collision, but a unit test pins that (`isSeoMarketId("app") === false`) so adding a market named `app` is impossible silently.

### 7.2 Pre-launch legacy URL removal (no redirect window)

**Policy (pre-launch):** Adeni has not shipped consumer `/my-*` URLs to production users. C1 **deletes** the legacy customer route tree and does **not** ship Express 301s, Angular `redirectTo` aliases, or a deprecated-alias maintenance window. Bookmarks or drafts pointing at `/my-*` will hit the public wildcard (`** → ""`) until updated.

**Canonical customer URLs (only supported paths):**

| Removed (404 / public home) | Canonical URL |
|-----------------------------|---------------|
| `/my-bookings` | `/app/bookings` |
| `/my-bookings/:bookingId/intake[/:assignmentId]` | `/app/bookings/:bookingId/intake[/:assignmentId]` |
| `/my-packages` | `/app/benefits/packages` |
| `/my-gift-cards` | `/app/benefits/gift-cards` |
| `/my-messages` | `/app/messages` |

| Topic | Behaviour |
|-------|-----------|
| `/my-quotes` (never shipped) | Sprint 38 builds **`/app/quotes` only**. |
| `/` (signed-in) | Public landing; **Open app** CTA → `/app/home` (or `/app/bookings` until Home ships). |
| Auth0 callback → `/` | `AuthReturnService` → stored `appState.target`; default when none: `/app/home`. |

- **Internal links (Phase A):** all Discover `routerLink`, auth return paths, and pay/checkout targets use `/app/*` only (`booking-panel`, `checkout-stub`, `pay`, `public-footer`, `public-shell`, intake back-link, messages/packages/gift-cards heads).
- **Notifications / email templates:** audit 38c before launch; any draft `/my-*` links must be updated to `/app/*` (no server-side redirect safety net).
- **`**` wildcard:** public `** → ""` remains; `/app/**` has its own wildcard inside the `app` parent.

### 7.3 Auth guard changes

- `customerAuthGuard` moves to the **`app` parent route only** (children inherit) — removes per-route repetition and the page-specific `returnPath` regex; the target is the **requested `state.url` path** validated by `AuthReturnService.isSafeTarget`.
- Unauthenticated + Auth0 configured → `login(returnPath)`; unauthenticated + not configured → `/discover` (unchanged). Dev bypass unchanged (`isDiscoverCustomerDevMode`) — **Development/Testing only**, never prod (AGENTS).
- `canMatch`/`canActivate` run in browser only because `app/**` is client-rendered; no SSR guard path remains.

---

## 8. Customer Home data contract (APIs available **today**)

Home is a **composition** screen (`CustomerHomeComponent`) fed by a `CustomerHomeStore` (signals; loads in parallel with `Promise.allSettled` so one failing card never blanks the page; each card has own skeleton/error/retry; `AbortController` on destroy — match intake/gift-card patterns).

| Home section | Source (existing) | Contract | Behaviour / empty state |
|--------------|-------------------|----------|--------------------------|
| **Up next** (hero card) | `GET /api/v1/bookings` → `getMyBookings()` | `CustomerBookingResponse[]` — filter `startAt ≥ now`, `status ∈ {0 pending, 1 confirmed}`, sort asc, first item. Fields used: `serviceName`, `businessName`, `businessSlug`, `startAt/endAt`, `staffDisplayName`, `guestCount`, `lines`, `totalPriceAmount/currency`, `status`. | Date block + status pill + actions: **View business** (`/businesses/:slug`), **Forms** (if pending assignments), **Message** (§below), **Cancel** (existing `canCancel` rule + confirm dialog). Empty: `AdeniPortalEmptyState` "No upcoming appointments" + **Find a business** (`/app/discover`). |
| **Pending forms** badge | `GET /api/v1/bookings/{id}/intake` → `intake.customerStatuses(bookingId)` for **next ≤ 3 bookings only** (avoid N+1) | `IntakeStatus[]` — count assignments `canSubmit`/not submitted | Shown on card as pill "N forms to complete" → `/app/bookings/:id/intake`. |
| **Upcoming list** | same bookings payload | next 3 after hero | "See all" → `/app/bookings`. |
| **Quick actions** (row of 4) | Navigation only (no API) | **Find a business** → `/app/discover`; **Book again** → last completed/past booking: `/businesses/{businessSlug}` + `bookingPreselectionQuery({service: serviceOfferingId, practitioner: staffMemberId})` + `#book` (F4 preselection, shared helper); **Messages** → `/app/messages` (+ unread badge); **Benefits** → `/app/benefits`. | "Book again" hidden when no past booking. No "Reschedule" (no API). |
| **Messages** card | `GET /api/v1/messages/conversations` → `listCustomerConversations()` | `ConversationSummary[]` — `needsAttention`, `preview`, `lastMessageAt`, business name | Top 2 threads + unread count; row → `/app/messages/:id`. Badge shared with shell via `CustomerMessagingBadgeService`. Empty: "Start a conversation from a booking." |
| **Benefits snapshot** | `GET /api/v1/me/retention/packages` → `retention.myPackages()`; `GET /api/v1/me/gift-cards` → `commerce.purchasedCards()` | `CustomerPackage[]` (status, `balances[].remaining`, `expiresAt`); `BusinessGiftCard[]` (`remainingAmount`, `currency`, `state`) | "N active packages · M credits left (soonest expiry <date>)"; gift cards "outstanding ₦X / $Y" **per currency, never summed across currencies** (`formatPrice`); CTA → `/app/benefits`. Memberships line appears only when 37.2-D API exists. Hidden entirely if both lists empty **and** call succeeded (show discovery hint instead). Credits are sessions, never money (copy rule). |
| **Leave a review** | bookings payload (`canReview && !hasReview`) + `POST /api/v1/bookings/{id}/review` (`createBookingReview`) | existing | One-card prompt for most recent eligible booking → opens inline review modal (`AdeniModalComponent`). *Phase C* (API & i18n exist; UI new). |
| **Header greeting** | Auth0 ID-token claims (`name`) in `CustomerAppSession` | client-only, in-memory | Falls back to "Welcome back". **Never** log/persist email/name beyond the session; no PII in analytics events. |

**Explicitly not on Home (no data today):** loyalty points, favorites, recommendations, payment methods, notification feed, receipts/invoices list, quote offers (API exists, UI deferred), membership state, "check-in now", directions/address, business phone (masked on public profile only), reschedule.

**Request budget:** 3–5 calls on first paint (`bookings`, `conversations`, `packages`, `gift-cards`, + ≤3 intake statuses lazily after bookings). All are `GET`s already authorized by customer JWT; no tenant header (customer-scoped). Add `Cache-Control: no-store` expectations already enforced server-side for `/me/*`; client does not cache across sessions.

---

## 9. Benefits hub

### 9.1 Structure (`/app/benefits`)

- Hub header via `app-portal-page` ("Benefits"), summary strip (3 stat tiles: active packages, credits left, gift-card balance(s)), then `AdeniTabsComponent linkMode="path"`:
  1. **Packages** (default) — existing `MyPackagesComponent` content re-skinned: card per `CustomerPackage` with `adeni-pill` status, per-service meter (`granted/used/remaining`), expiry. Honest empty state with link to `/app/discover`.
  2. **Gift cards** — existing `PurchasedGiftsComponent` behaviour preserved verbatim: list `BusinessGiftCard`, `••••suffix`, **on-demand reveal**, auto-hide on destroy, abort controller, **"no wallet" notice kept** (Adeni holds no funds — AGENTS). No redemption UI (redemption is public Pay Link flow).
  3. **Memberships** — appears when `retention.myMemberships()` exists in api-client **and** returns 2xx (37.2-D). Until then the tab is not rendered (no "coming soon" stub). Contract (from 37.2 spec): `/api/v1/me/retention/memberships`; statuses Pending/Active/PastDue/Frozen/CancelPending/Cancelled/Expired; cycle benefits; customer **requests** freeze/cancel (owner approves — per slice D). Live online billing is stub-gated until Sprint 34/37.2-C, so tab visibility also respects `Retention` stub/live flags returned by API (no client env flag).
- Shared `CustomerBenefitsStore` (signals) feeds hub, tabs and Home snapshot so `packages`/`gift-cards` are fetched once per session visit (refresh on pull/retry).

### 9.2 Data & gaps

| Need | Today | Action |
|------|-------|--------|
| Package list | `myPackages()` ✓ | — |
| Gift cards purchased | `purchasedCards()` + `reveal()` ✓ | — |
| Deep link "Use at / Book" | rows lack `businessSlug` | **Optional additive contract field** `businessSlug` on `CustomerPackage` and `BusinessGiftCard` (API + Zod optional) — separate small PR; until then CTA = "Find business" or none. |
| "Use credits at booking" | Booking panel already calls `myRedeemableBenefits(slug, …)` ✓ | Package card CTA (when slug available) → `/businesses/:slug#book`. |
| Memberships | not built | Gate on 37.2-D. |
| Received gifts / wallet | none, out of scope | Do not build. |

### 9.3 Copy & compliance

Credits = sessions; gift card = business-specific value, not wallet; keep `commerce.noWallet` notice; no balances aggregated across businesses or currencies. EN/FR keys under `discover.benefits.*` in `@adeni/shared` i18n (parity test must pass).

### 9.4 Optional additive API changes (each needs its own approval; none block A–F)

1. `businessSlug` on `/me/retention/packages` and `/me/gift-cards` items.
2. `GET /api/v1/bookings/{id}` customer detail (deep links from messages/notifications without list fetch).
3. Customer notification preferences (`/api/v1/me/notification-preferences`) — Notifications module (38) owns.
4. Customer profile display name/email via `/auth/me` (today claims-only).

---

## 10. Migration of existing pages

| Page | Move | Restyle | Behaviour changes |
|------|------|---------|-------------------|
| `my-bookings` | → `/app/bookings`; Upcoming/Past via `AdeniTabs` (`button`/`query`) replacing `cds-content-switcher` | `app-portal-page` head; list rows as flat panels with `adeni-pill`; drop `.wash`/kicker; SCSS 8 kB → delete (global partial) | Add booking **detail expansion** (accordion) using list data (no GET-by-id); add **Review** CTA (Phase C); add **Book again**; keep cancel/confirm/intake. `data-status` tones preserved. |
| `intake` | → `/app/bookings/:bookingId/intake[/:assignmentId]` | page chrome swap only | Back link → `/app/bookings`; keep `RenderMode.Client`, `no-store`, abort/stale guards, **no TransferState**, `IntakeFieldsComponent` unchanged. |
| `my-packages` | → `/app/benefits/packages` | via hub | Remove duplicated header buttons (`My bookings`, `Discover`) — nav owns navigation. |
| `my-gift-cards` (`PurchasedGiftsComponent`) | → `/app/benefits/gift-cards` | via hub | Preserve reveal/abort/destroy hygiene exactly (F3 security review). |
| `my-messages` | → `/app/messages` (+ `/:conversationId`) | keep split layout; align mobile toggle breakpoint with 899 px; replace `.empty` with shared empty state | Lift realtime lifecycle to shell service; selection from route param; preserve attachment preview, mark-read, `needsAttention`. |
| Customer shell | `CustomerShellComponent` → superseded by `CustomerAppShellComponent` | — | Delete legacy shell once `/app/*` is live (Phase A/F). |
| Public shell links | — | — | Replace "My bookings/Messages" with **Open app** when signed in; footer "My bookings" → `/app/bookings` (kept as a plain link for signed-out users who then sign in and return). |

Rollout safety: pages are **moved**, not rewritten — component class logic stays; only templates/styles/route wiring change in Phase B, minimising regression risk. Pre-launch cutover removes `/my-*` in the same change that adds `/app/*` (no parallel trees).

---

## 11. Flutter consumer app parity

The web IA is deliberately the Flutter IA. `go_router`/`NavigationBar` tab structure = web tab bar; detail routes = nested paths.

| Web route | Flutter screen / route | Tab | API (shared by both) | Notes |
|-----------|------------------------|-----|----------------------|-------|
| `/app/home` | `HomeScreen` `/home` | **Home** | bookings, conversations, me/packages, me/gift-cards | Same composition store logic specified in §8 → port as Dart repository + provider. |
| `/app/discover` | `DiscoverScreen` `/discover` | **Discover** | `GET /api/v1/discovery`, markets | In Flutter, business detail + booking are **in-app** screens (`/discover/business/:slug`) — differs from web handoff (§6.4) because native has no SEO constraint. |
| `/businesses/:slug#book` | `BusinessScreen` + `BookingFlow` | (Discover stack) | businesses, availability, `POST /bookings` (+ `Idempotency-Key`) | Web booking panel stays canonical; Flutter reimplements UI on same API. |
| `/app/bookings` | `BookingsScreen` | **Bookings** | `GET /bookings`, cancel | Tab filter Upcoming/Past ↔ segmented control. |
| `/app/bookings/:id/intake[/:aid]` | `IntakeScreen` | (Bookings stack) | `/bookings/{id}/intake…` | Zod schemas → OpenAPI-generated Dart models. |
| `/app/messages[/:id]` | `MessagesScreen` / `ThreadScreen` | **Messages** | `/messages/conversations…` + SignalR hub (`signalr_netcore`) | Badge from `needsAttention`; FCM later (product-direction notes FCM with Flutter consumer). |
| `/app/benefits/*` | `BenefitsScreen` (3 tabs) | via Home/Profile | `/me/retention/*`, `/me/gift-cards` | Not a bottom tab (parity with web mobile). |
| `/app/profile` | `ProfileScreen` | **Profile** | `/auth/me` + OIDC claims | Native auth = Auth0 PKCE (`auth0_flutter`); no new endpoints. |
| `/pay`, `/checkout/*`, `/documents/:t`, `/q/:slug` | Deep-link handlers (custom tabs / in-app browser) | — | public pay-link APIs | Keep as web utilities. |
| Auth return | Universal/App Links `https://adeni.com/app/**` | — | — | **`/app/*` path prefix becomes the universal-link namespace** — another reason to separate now. Add `apple-app-site-association` / `assetlinks.json` in the Flutter sprint; exclude `/businesses/**`? (decision: allow, enables "open business in app"). |

Contract policy: API stays the single source of truth; `@adeni/shared` Zod types remain TS-only — Flutter uses OpenAPI codegen (existing plan). `customer-nav.ts` entries intentionally map 1:1 to Flutter `NavigationDestination`s; keep ids (`home|discover|bookings|messages|profile|benefits`) identical.

---

## 12. SEO / SSR

| Route family | Render mode | Headers | Indexing |
|--------------|-------------|---------|----------|
| `/`, `/discover`, `/businesses/**`, market SEO routes, `/privacy`, `/terms`, `/plan` | **Server** (unchanged) | existing | Indexed; sitemap unchanged |
| `/documents/**`, `/q/**`, `/pay`, `/checkout/**`, `/book/:slug` | unchanged (existing rules/handlers) | existing | noindex as today |
| **`/app/**`** | **`RenderMode.Client`** (`{ path: "app/**" }`, declared **before** `**`) | `Cache-Control: no-store, private`; `X-Robots-Tag: noindex, nofollow`; `Referrer-Policy: no-referrer` | Never indexed; `<meta name="robots" content="noindex,nofollow">` set in shell via `SeoService`; **not** in sitemap |
| Legacy `/my-*` | **Removed** — no routes, no redirects | — | Not in sitemap; requests fall through to public wildcard |

- `robots.txt`: `Disallow: /app/` only (legacy `/my-*` disallow entries removed with the routes).
- **Why client-only (not SSR):** no sensitive hydration payloads; no TransferState for customer data (F2 constraint); guard needs browser Auth0 SDK (`loadAuthService` returns `null` on server); eliminates today's inconsistency where `/my-bookings` and `/my-messages` are server-rendered shells. Matches the intake route pattern.
- **Hydration:** `provideClientHydration()` stays global; `RenderMode.Client` routes bypass hydration. Verify no flash from the public `index.html` template (add a neutral `app-loading` skeleton in `index.html` only if flash appears — measure in Phase A).
- **SSR safety:** shell and stores guard browser-only APIs with `isPlatformBrowser`; no `window`/`matchMedia` in constructors (existing `MyMessages.isNarrow()` already guards).
- **Core Web Vitals:** app chunks are lazy (`loadComponent`); nothing from `/app` enters public initial bundle except the route stub. Track `build:customer` initial size per phase (currently 1.42 MB).

---

## 13. Auth, tenant, PII, security

| Area | C1 impact |
|------|-----------|
| **Tenant isolation** | App surface is **customer-scoped** (JWT sub → platform customer); no `X-Tenant-Id`, no tenant filters touched. `/app/discover` links leave to public business pages. No cross-tenant aggregation is introduced beyond what `/me/*` already returns per customer. |
| **Auth / roles** | Customer role only; `customerAuthGuard` on `app` parent; dev bypass remains Development/Testing only. Business/admin users who open `/app` get the same customer experience (no staff features). |
| **Return-path safety** | `AuthReturnService` accepts only same-origin relative paths from an allowlist (prefix + charset); rejects `//`, schemes, `\`. Never trust query-supplied redirect params (matches F2 note "a URL containing IDs is a navigation aid, never authorization"). |
| **Cache** | Client-only, `no-store`; no new Redis keys; no API caching changes. Optionally in-memory store dedupe per session. |
| **Audit** | No admin mutations; no new audit events. |
| **PII** | Name/email/picture read from Auth0 claims in memory only; not stored in localStorage by app code (SDK cache excepted), not sent to analytics, not logged. Gift-card codes remain on-demand and cleared on destroy; booking notes not logged. Public profile phone masking unaffected. |
| **Observability** | Preserve `X-Correlation-Id` (api-client). Add PostHog **navigation events** only after Sprint 20 policy: `app_tab_viewed {tab}`, `home_card_clicked {card}` — no PII. |
| **Webhook/payments** | Untouched. `/pay`, checkout stub unchanged. |
| **API contract** | No endpoint changes in A–F. If §9.4 fields are added: additive optional, `/api/v1/` only, Zod optional, no deprecation needed. |
| **Rate limiting** | `/me/*` and `/bookings` unchanged; Home's 3–5 calls per load stay within existing read policies (verify in Phase C against staging rate limit). |

---

## 14. Phased implementation (A–F)

Each phase is shippable, keeps public behaviour identical, and ends with `npm run typecheck` + discover tests green. Files are under `apps/customer/src/app/` unless noted.

### Phase A — Surface boundary, shell skeleton, pre-launch cutover (foundation)

**Goal:** `/app/*` exists with the new shell; legacy `/my-*` routes deleted (no redirects); SSR/robots/auth-return correct.

| Change | Files |
|--------|-------|
| `app` route tree + guard on parent (no legacy `/my-*` siblings) | `app.routes.ts`, `core/guards/customer-auth.guard.ts` |
| Render mode + headers for `app/**` | `app.routes.server.ts` |
| `robots.txt` | `public/robots.txt` |
| Shell: top bar, tab bar, messages badge service, account menu, noindex | `app-surface/shell/*`, `app-surface/customer-nav.ts`, `app-surface/services/customer-app-session.ts`, `customer-messaging-badge.service.ts` |
| `AuthReturnService` + app initializer; replace guard allowlist; **verify on real Auth0** | `core/services/auth-return.service.ts`, `app.config.ts` |
| Icons: add glyphs to `@adeni/ui` | `packages/ui/src/carbon-icon.ts` (+ export type) |
| i18n keys (`discover.app.nav.*`) EN+FR | `packages/shared/src/i18n/messages/overlay-en.ts`, `overlay-fr.ts` (parity test) |
| Global app partial skeleton `.adeni-app` | `src/styles/_app-surface.scss`, `src/styles.scss` |
| Existing pages temporarily mounted under shell **unmodified** (templates unchanged) | route `loadComponent` only |
| Internal link rewrites (§7.2) | `booking-panel`, `checkout-stub`, `pay`, `public-footer`, `public-shell` |
| Public shell "Open app" CTA when signed in | `layout/public-shell.component.*` |
| Tests | route-contract spec, guard spec, `isSeoMarketId("app")`, shell spec |

### Phase B — Page migration & chrome unification

**Goal:** Bookings, Messages, Intake look like the same product as portal.

- Bookings → `app-portal-page` + `AdeniTabs` + `AdeniPill` + `AdeniPortalEmptyState`; delete `my-bookings.component.scss`; add Book again, expansion detail.
- Messages → route-param selection (`/app/messages/:id`), realtime lifted to shell service, breakpoint 899 px, shared empty states.
- Intake → chrome swap, back link, same logic/tests (`intake.component.spec.ts`).
- Move component folders `pages/my-*` → `app-surface/pages/*` (git mv; keep selectors/class names unless renamed with spec updates).
- Files: `app-surface/pages/bookings/*`, `messages/*`, `intake/*`, `_app-surface.scss`; delete page-level duplicate SCSS.
- Tests: bookings tab/filter, cancel flow, messages route-param deep link, realtime badge lifecycle.

### Phase C — Customer Home

**Goal:** Dashboard of Up next + quick actions + messages + benefits snapshot (+ review prompt).

- `CustomerHomeComponent`, `CustomerHomeStore`, `UpNextCardComponent`, `QuickActionsComponent`, `HomeMessagesCardComponent`, `HomeBenefitsSnapshotComponent`, `ReviewPromptComponent` (modal using `AdeniModalComponent`, `createBookingReview`).
- Pure helpers (`pickNextBooking`, `bookAgainUrl`, `summarizeBenefits`) → unit-tested; evaluate promotion to `@adeni/shared` (pure TS, Flutter-mirrorable) — **recommended**: `packages/shared/src/customer-home.ts` + tests so web and Dart ports share fixtures.
- i18n `discover.home.*` EN/FR.
- Tests: store composition with partial failures, empty/first-time state, currency separation, "book again" URL uses `bookingPreselectionQuery`, intake N≤3 cap.

### Phase D — Benefits hub

- `BenefitsHubComponent` (summary tiles + `AdeniTabs path`), re-skin packages and gift-card components (move into `app-surface/pages/benefits/`), shared `CustomerBenefitsStore`.
- Optional PR: `businessSlug` additive field (§9.4-1) + CTAs.
- Memberships tab **contract placeholder**: route + component added in the PR that lands api-client `myMemberships` (37.2-D), not before.
- Tests: reveal hide-on-destroy preserved, abort on destroy, per-currency totals, tab routing, 37.2-D visibility gate.

### Phase E — Profile, Discover-in-app, mobile polish

- `CustomerProfileComponent` (identity from claims, language switch (`AdeniLocaleService`), Benefits/Help/Legal/For-businesses links, Sign out; account menu parity).
- `AppDiscoverComponent` (`DiscoverySearchComponent`, `HeroCategoryStage`-free simplified categories, featured via `MarketContextService`, `surface="app"` card variant).
- Public-shell ↔ app handoff polish (`fromApp` back chip).
- Mobile: safe-area, keyboard handling, 44 px targets, focus order, `prefers-reduced-motion`, optional theme toggle (reuse `PortalThemeService` pattern → promote to `@adeni/ui` only if portal agrees; otherwise defer).
- Tests: tab bar a11y (roles, `aria-current`), breakpoints (matchMedia stubs), profile sign-out, discover handoff URLs.

### Phase F — Hardening, cleanup, docs

- Delete `CustomerShellComponent`, `_customer-page.scss`, old `pages/my-*` leftovers; confirm no references (`rg "my-bookings|my-packages|my-gift-cards|my-messages"`).
- Bundle/budget check (`build:customer`, no new regressions beyond the known `_data-grid.scss` failure), Lighthouse on `/` & `/businesses/:slug` unchanged, a11y audit (axe) on `/app/*`.
- Update `docs/frontend.md`, `docs/target-client-architecture.md` (Discover = public SSR **+ authenticated `/app`**), `docs/angular-migration-checklist.md` paths, spec status → Implemented, `sprints.md` row.
- Flutter parity: freeze `customer-nav.ts` ids; produce `docs/specs/flutter-consumer-app.md` stub (separate spec; not part of C1).
- Confirm no `legacy-redirects` or `/my-*` route entries remain (`rg "my-bookings|my-packages|my-gift-cards|my-messages"` in Discover).
- Security audit (play 4) on return-path handling, headers, no PII; EN/FR full parity.

---

## 15. Files to touch (summary)

| Layer | Files |
|-------|-------|
| Domain / Application / Infrastructure / Api | **None in A–F** (optional §9.4 additive fields would touch `Retention` + `Finance` `Me*` controllers, Application DTOs, tests) |
| Frontend — Discover | `app.routes.ts`, `app.routes.server.ts`, `app.config.ts`, `public/robots.txt`, `styles.scss`, `styles/_app-surface.scss`, `layout/public-shell.*`, `core/guards/customer-auth.guard.ts`, `core/services/auth-return.service.ts`, new `app-surface/**` (shell, nav, session, pages: home, bookings, messages, intake, benefits, profile, discover), removal of `layout/customer-shell.*`, `layout/_customer-page.scss`, `pages/my-*` after move; link updates in `shared/booking-panel`, `pages/checkout-stub`, `pages/pay`, `shared/public-footer` |
| `@adeni/ui` | `carbon-icon.ts` (new glyphs); optional alias export for `PortalPageComponent`; optional `AdeniStaffAppContext` `"customer"` |
| `@adeni/shared` | i18n overlays EN/FR (`discover.app.*`, `discover.home.*`, `discover.benefits.*`), optional `customer-home.ts` pure helpers + tests |
| `@adeni/api-client` | none in A–F; `retention.myMemberships()` arrives with 37.2-D |
| Docs | this spec, `sprints.md` row, Phase F updates to `frontend.md` / `target-client-architecture.md` |
| Tests | Discover specs (routes, guard, shell, home store, benefits, bookings, messages, auth-return), shared i18n parity |

---

## 16. Testing strategy

- **Route contract tests** (pattern: portal `customer-route-compatibility.spec.ts`): `/app` parent has `customerAuthGuard`, no legacy `/my-*` routes, `app` declared before market catch-alls, `isSeoMarketId("app")` false.
- **Server routes test:** `serverRoutes` entry for `app/**` is `Client` with required headers and precedes `**`.
- **Guard/AuthReturn tests:** safe/unsafe targets (`//evil.com`, `https://…`, `javascript:`, `\`), default target, unauth login redirect with return path, dev-mode bypass.
- **Component tests:** shell nav active state/`aria-current`, tab bar visibility by breakpoint (matchMedia stub), badge from service, Home store with `Promise.allSettled` partial failure, empty/first-time/returning variants, per-currency benefits summary, gift-card reveal cleanup, bookings cancel + tab logic.
- **Pure helper tests** in `@adeni/shared` (`pickNextBooking`, `bookAgainUrl`, `summarizeBenefits`) with shared JSON fixtures reusable by Flutter.
- **i18n parity:** `parity.test.ts` EN/FR for new keys.
- **E2E smoke (manual script until Playwright exists):** login → `/app/home`; deep link `/app/bookings/:id/intake` unauthenticated → login → returns; legacy `/my-*` URLs are not registered; public page HTML source unchanged (diff SSR output of `/` and `/businesses/:slug` before/after Phase A for regressions); `/app/*` response headers.
- **Non-functional:** a11y (axe on 6 routes), bundle size delta, Lighthouse for public routes unchanged, keyboard-only pass on tab bar and account menu, 320 px / 390 px / 768 px / 1280 px visual checks.
- Commands: `npm test --workspace @adeni/customer`, `npm test --workspace @adeni/shared`, `npm run typecheck`; backend `dotnet test Adeni.slnx -c Release` only if §9.4 API changes land.

---

## 17. Risks & non-goals

| # | Risk | Mitigation |
|---|------|------------|
| R1 | **Single deployable growth** — consumer app features bloat Discover's bundle/complexity, hurting SEO CWV | Lazy `/app` chunks; budgets per phase; split-app trigger criteria: app routes > ~35% of source or independent release cadence required (re-evaluate at Phase F). |
| R2 | **Auth return path currently unreliable** (dead `appState`) — new deep links break login UX | Phase A spike on real Auth0 tenant before any redirect ships; keep dev-mode tests from masking it. |
| R3 | **Legacy URL breakage** (draft bookmarks, emails) | Pre-launch: no redirects — audit notification templates (38c) and internal links before launch; `/my-*` is unsupported. |
| R4 | **Public shell regressions** (SSR output/SEO) | Don't touch public SSR components beyond header links; diff SSR HTML for `/`, `/discover`, `/businesses/:slug`, market pages pre/post; sitemap unchanged. |
| R5 | **SCSS budget** (existing `build:customer` failure; 14 kB component cap) | Global `.adeni-app` partial; delete per-page SCSS as pages migrate; do not worsen `_data-grid.scss`. |
| R6 | **Realtime lifecycle bug** when lifting SignalR to shell (leaks, double joins) | Single owner service, explicit start on shell init/stop on destroy/logout, tests with fake hub. |
| R7 | **Home N+1 / rate limits** | ≤ 5 initial calls; intake statuses capped to next 3; `allSettled`; retry per card. |
| R8 | **Feature honesty** — showing UI for unbuilt memberships/quotes/reschedule | Gate on API availability; no placeholder tabs. |
| R9 | **Market SEO collision** if a market id equals an app path | Reserved-prefix unit test; `/app` documented as reserved top-level segment (also reserve `me`, `account` for future). |
| R10 | **Mobile keyboard/safe-area quirks** with fixed tab bar | `env(safe-area-inset-*)`, `visualViewport` handling, device matrix in Phase E. |
| R11 | **Naming drift** (`app-portal-page`, `adeni-staff-*` in consumer app) | Accept for C1; optional neutral aliases in `@adeni/ui`, no rename sweep. |

**Non-goals (restated):** separate Angular app, Flutter code, public-page restyle, new backend modules, wallet/custody, reschedule/favorites/loyalty, quotes UI, push, dark theme (optional), SEO changes to public pages.

---

## 18. Recommended order and estimate (sprints)

| Phase | Effort | Depends on | Release |
|-------|--------|------------|---------|
| A Boundary + shell + redirects + auth-return | **1 sprint** (includes Auth0 spike) | — | Ship with pages unmodified inside new shell |
| B Page migration & unification | **1 sprint** | A | **MVP of the new surface** (A+B ≈ 2 sprints) |
| C Customer Home (+ review prompt) | **1 sprint** | A (B recommended for Bookings reuse) | |
| D Benefits hub | **0.5–1 sprint** | B; 37.2-D optional later | Memberships tab = follow-up PR when 37.2-D lands |
| E Profile, Discover-in-app, mobile polish | **0.5–1 sprint** | A–C | |
| F Hardening, cleanup, docs | **0.5 sprint** | all | |
| **Total** | **~4–5 sprints for one engineer; ~3 with two parallel tracks (A→B→D ‖ C after A)** | | |

**Placement:** run C1 **in parallel with** (not ahead of) 33-closeout staging E2E / Sprint 34; it is client-only and does not block backend module order ([roadmap-modules.md](../roadmap-modules.md)). Do **not** start Phase D memberships integration before 37.2-D exists. Do not start Sprint 19+ trust/LLM scope.

---

## 19. Acceptance criteria

- [x] `/app/home|discover|bookings|messages|benefits|profile` render inside one shell; unauthenticated access redirects to Auth0 and returns to the requested `/app/*` path (**real Auth0 tenant smoke still required** — see [auth0-setup.md](../auth0-setup.md) customer §5).
- [x] Legacy `/my-*` URLs are **not** registered (no parallel trees, no 301/302 aliases); canonical customer URLs are `/app/*` only.
- [x] Public routes' SSR HTML, canonical tags, sitemap and structured data are unchanged apart from header / Open app affordances; sitemap contract excludes `/app`.
- [x] `/app/**` is client-rendered with `no-store, private` + `noindex, nofollow`; `robots.txt` updated; not in sitemap.
- [x] Desktop top nav and mobile 5-tab bar match §6.5; Messages badge reflects `needsAttention` (SignalR shell lift deferred — badge updates on messages/home load).
- [x] Home shows only data from existing APIs; any single failing call degrades one card only (Up next, messages, benefits, review prompt).
- [x] Benefits hub preserves gift-card reveal hygiene and "no wallet" copy; memberships tab present (37.2-D API landed).
- [x] `CustomerShellComponent` deleted; pages live under `app-surface/pages/*`; `_customer-page.scss` retained only for Benefits list chrome.
- [x] EN/FR home/nav keys shipped; a11y / Lighthouse budget checks remain manual before launch.
- [ ] Flutter parity table (§11) reviewed by mobile owner; `customer-nav.ts` ids frozen.

---

## 20. Open questions

| Question | Recommendation / Decision |
|----------|---------------------------|
| **Q1.** Auth0 `redirect_uri`: keep `origin` + `appState` return (no Auth0 tenant change) or add `/app/callback`? | **Keep origin + `AuthReturnService`** (no allowed-callback change); revisit only if the spike shows SDK limitations. |
| **Q2.** Should booking happen inside the app shell (`/app/businesses/:slug`)? | **No for C1.** Canonical public page; revisit with a canonical-link spec if conversion data demands it. |
| **Q3.** Is "Benefits" a bottom tab? | **No** — 5 tabs as mandated; Benefits via Home/Profile (desktop nav has it). Revisit if analytics shows Benefits is a top-3 destination. |
| **Q4.** Should signed-in `/` redirect to `/app/home`? | **No** (SEO/bot safety, shareable); show **Open app** CTA. |
| **Q5.** Add `businessSlug` to `/me/*` payloads (§9.4)? | **Yes**, small additive PR in Phase D; unblocks "Book/Use credits" CTAs. |
| **Q6.** Reviews in Home/Bookings in C1? | **Yes (Phase C)** — API and i18n exist; UI is the only gap. |
| **Q7.** Promote `PortalPageComponent` rename to neutral name? | **Defer**; alias only if review demands. |
| **Q8.** `/app/quotes` timing? | Owned by Sprint 38 follow-up; slot reserved, tab appears only for `quote_request` capability. |
| **Q9.** Dark theme for app? | Optional in Phase E/F; reuse portal theme pattern. |

---

## 21. Repo rename: Discover app → Customer app

**Planning spec:** [discover-to-customer-rename.md](./discover-to-customer-rename.md).

| Layer | C1 spec language after rename |
|-------|-------------------------------|
| Deployable | `apps/customer` (`@adeni/customer`, `npm run dev:customer`, `build:customer`, `:5190`) — no `dev:discover` / `build:discover` aliases. |
| Public marketplace | Still **Discover**: routes `/discover`, `/businesses/:slug`, in-app tab `/app/discover` — SEO and product copy unchanged. |
| Signed-in surface | **Customer app** under `/app/*` (this spec); not a second Angular app. |

Execute the folder/npm rename **after** Phase A–F code is committed or in a dedicated PR **before** parallel C1 work continues on old paths. Do not confuse npm package rename with URL or `DiscoveryController` / `discovery-api` domain names.
