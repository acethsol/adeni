# Spec: Sprint C2 — Consumer portal (dashboard, calendar, shared UI)

> Copied from [`_template.md`](./_template.md). Standing play 1 (spec write) — **planning only; no application code in this change.**

| Field | Value |
|-------|-------|
| **Sprint** | Sprint C2 — Consumer portal |
| **Author** | Aceth (product clarification 2026-10-30) |
| **Status** | Implemented on `dev` |
| **Created** | 2026-10-30 |
| **Updated** | 2026-10-30 |
| **Depends on** | [sprint-c1-consumer-app-experience.md](./sprint-c1-consumer-app-experience.md) (Implemented on `dev`) |
| **Related** | [flutter-consumer-app.md](./flutter-consumer-app.md), [shared-data-grid.md](./shared-data-grid.md), [target-client-architecture.md](../target-client-architecture.md) |

---

## 1. Goal (one sentence)

Make the signed-in consumer surface under `/app/*` feel like a **real application portal** — the customer edition of the Business app — by shipping a proper **Dashboard** and **Calendar**, treating top-bar links as primary menu destinations, and standardizing pages on **`@adeni/ui`** shared chrome (while Discover remains the marketplace exception).

---

## 2. Context

- **Authority:** [product-direction.md](../product-direction.md) — Beauty & Wellness; consumer Flutter is the primary mobile focus; Carbon + `@adeni/ui` for staff/app patterns.
- **Predecessor:** C1 shipped the `/app/*` boundary, auth guard, CSR/`noindex`, config-driven nav, Phase C Home (Up next / messages / benefits / review), Benefits hub, and page moves into `app-surface/`. Home is a **starter composition**, not a portal dashboard; there is **no calendar view**; Benefits still use leftover `_customer-page.scss` list chrome.
- **Product clarification (2026-10-30):**
  1. `/app` is an **application portal** (like business) — same product density and shared components — **except Discover** (marketplace find/book handoff).
  2. Top links are **menu items**; consumers need a real **Dashboard** and a **Calendar**.
  3. **Shared `@adeni/ui` components** must be the default page kit.
- **Shell decision (locked for C2):** **Option A — component parity, not BusinessShell layout.** Keep consumer **top bar + mobile tab bar** (no sidebar rail, no branch switch, no tenant search). Visual/component language matches business pages (`app-portal-page`, tabs, pills, empty states, staff buttons, list toolbar where lists warrant it). Revisit sidebar only if analytics demand desktop density later.
- **Sprint placement:** Client-experience track (C2), parallel to numbered platform sprints; does not displace 32a/33/34/37.x backend work. **No Domain/API/SQL changes** unless a gap appears in §5 (none expected).
- **Modules:** Booking (read `GET /api/v1/me/bookings` / `getMyBookings`), Messaging, Retention/Commerce (existing me endpoints), Discover handoff unchanged.

---

## 3. In scope

- [x] **Nav IA update:** rename `home` → `dashboard`; add `calendar`; keep Discover / Bookings / Messages / Benefits / Profile; update mobile tabs; freeze ids for Flutter.
- [x] **Route aliases:** `/app/home` → `/app/dashboard` (internal `redirectTo` only; no public SEO impact).
- [x] **Dashboard** (`/app/dashboard`): portal-grade overview built from existing APIs (Up next, messages snapshot, benefits snapshot, review prompt, quick actions) using shared page chrome — not the business Zenoti widget editor.
- [x] **Calendar** (`/app/calendar`): consumer appointment calendar (month default + week) driven by `getMyBookings` client-side; day selection → detail list / link to booking detail; empty states via shared empty component.
- [x] **Shared UI sweep:** `/app/*` pages use `PortalPageComponent` (+ tabs/pills/empty/modal); Benefits list chrome moved to `_app-surface.scss`; `_customer-page.scss` deleted. Bookings data-table deferred (list + tabs remain).
- [x] **Shell polish:** top bar primary menu includes Dashboard + Calendar; account menu unchanged; still **not** BusinessShell.
- [x] **i18n EN/FR** for new nav + dashboard + calendar copy; parity tests.
- [x] **Tests:** nav contract, auth-return, dashboard helpers, calendar bucketing.
- [x] **Docs:** flutter stub, `frontend.md`, `sprints.md`; this spec → Implemented.

## 4. Out of scope

- **BusinessShell sidebar / rail** (Option B) — deferred.
- **In-shell booking** (`/app/businesses/:slug`) — still canonical public `/businesses/:slug` (C1 Q2).
- **Business dashboard widget grid** (Sprint 32 customize/drag/presets) — consumers get a fixed composition, not layout editing.
- **Google/Outlook calendar sync** for consumers (`/api/v1/tenant/calendar/*` is tenant-staff) — not in C2.
- **New booking APIs** (get-by-id, reschedule, cancel-from-calendar beyond existing cancel) — reuse list payload + existing mutations.
- **Wallet / custody**, quotes UI, push, dark theme mandatory.
- **Flutter implementation** — update frozen-nav stub only.
- **SignalR shell lift** — still deferred from C1 R6 unless a small PR lands alongside badge work (nice-to-have, not blocking C2).
- Public SEO / sitemap / SSR marketplace changes.

---

## 5. API contract

No new endpoints. Compose from existing customer-auth APIs:

| Method | Route | Auth | Use in C2 |
|--------|-------|------|-----------|
| GET | `/api/v1/me/bookings` | Customer JWT | Dashboard Up next / review; Calendar events |
| GET | `/api/v1/me/conversations` (existing client: `listCustomerConversations`) | Customer JWT | Dashboard messages card |
| GET | `/api/v1/me/retention/packages` | Customer JWT | Benefits snapshot |
| GET | `/api/v1/me/gift-cards` | Customer JWT | Benefits snapshot |
| POST | `/api/v1/bookings/{id}/review` | Customer JWT | Review prompt (unchanged) |

**Shapes:** existing Zod types in `@adeni/shared` (`CustomerBookingResponse`, `ConversationSummary`, `CustomerPackage`, `BusinessGiftCard`).

**Calendar data rule:** treat bookings as timed events (`startAt`/`endAt`); filter by visible month/week in the client; do **not** call tenant staff calendar or external calendar APIs.

```json
{
  "note": "No new request/response bodies in C2"
}
```

---

## 6. Information architecture

### 6.1 Nav (frozen ids after C2)

Source: `apps/customer/src/app/app-surface/customer-nav.ts` → update [flutter-consumer-app.md](./flutter-consumer-app.md).

| Id | Path | Desktop menu | Mobile tab | Notes |
|----|------|--------------|------------|-------|
| `dashboard` | `/app/dashboard` | Yes | Yes (replaces `home`) | Default post-login / Open app |
| `calendar` | `/app/calendar` | Yes | Yes | New |
| `discover` | `/app/discover` | Yes | Yes | Marketplace exception inside shell |
| `bookings` | `/app/bookings` | Yes | Optional† | List + detail + intake |
| `messages` | `/app/messages` | Yes | Yes | Badge `needsAttention` |
| `benefits` | `/app/benefits` | Yes | No | Hub tabs unchanged |
| `profile` | `/app/profile` | Account menu | Yes | |

† **Mobile tab budget:** keep **5 tabs**. Recommended set: **Dashboard · Calendar · Discover · Messages · Profile**. Bookings reachable from Dashboard/Calendar/desktop menu (same pattern C1 used for Benefits). If product prefers Bookings over Calendar on the tab bar, swap and keep Calendar desktop-only — decide in review (open Q1); default recommendation above.

**Breaking nav id:** `home` → `dashboard`. Analytics/Flutter stub must update in the same PR. `CUSTOMER_APP_HOME` constant → `/app/dashboard`.

### 6.2 Shell (Option A)

```
CustomerAppShell
├── Top bar (≥900px): brand · primary menu · messages badge · account
├── main router-outlet (app-portal-page pages)
└── Tab bar (<900px): 5 destinations per §6.1
```

**Not in shell:** business sidebar groups, command search, pending-bell (tenant ops), entitlements gating.

Discover remains the only surface that may reuse public discovery widgets (`surface="app"`), then leaves the shell for `/businesses/:slug`.

---

## 7. Feature specs

### 7.1 Dashboard (`CustomerDashboardComponent`)

**Successor to** `CustomerHomeComponent` (rename or replace in place; route path changes).

| Card | Source | Failure mode |
|------|--------|--------------|
| Up next + coming up | `getMyBookings` | Card-only retry (existing helpers) |
| Messages snapshot | conversations list | Card-only retry |
| Benefits snapshot | packages + gift cards `allSettled` | Hide if empty success; error card if both fail |
| Leave a review | `canReview` booking | Modal via `AdeniModalComponent` |
| Quick actions | Discover, Book again, Calendar, Benefits | Always available |

**UI kit:** `PortalPageComponent`, `AdeniPillComponent`, `AdeniPortalEmptyStateComponent`, `AdeniCarbonIconComponent`, `AdeniStaffButtonComponent` for primary actions where icon+label matches portal. No `.wash` / kicker.

**Not included:** customizable widget grid, revenue KPIs, layout presets from Sprint 32.

Promote pure helpers (`pickUpcomingBookings`, `summarizeBenefits`, …) to `packages/shared/src/customer-dashboard.ts` **if** Flutter starts before C2 ends; otherwise keep under `app-surface/pages/dashboard/` and note extract in flutter stub.

### 7.2 Calendar (`CustomerCalendarComponent`)

| Concern | Behaviour |
|---------|-----------|
| Views | **Month** (default); **Week** via `AdeniTabs` `button` or `query` (`?view=month\|week`) |
| Data | One `getMyBookings()` load; bucket by local calendar day in the active market/locale |
| Cell | Dot/count for days with appointments; selected day shows list below (service, business, time, status pill) |
| Navigation | Prev/next period; “Today” |
| Actions | Row → `/app/bookings/:id`; optional Book again / intake links reuse existing helpers |
| Empty | Shared empty state + CTA to `/app/discover` |
| A11y | Keyboardable grid; `aria` labels for days with counts; respect `prefers-reduced-motion` |

**UI kit:** `PortalPageComponent`, `AdeniTabs`, `AdeniPill`, empty state, Carbon icons. Prefer a thin consumer calendar presentational component in `apps/customer` first; extract to `@adeni/ui` only if business calendar UI would share it (unlikely in C2 — business calendar is staff schedule).

### 7.3 Shared UI sweep

| Page | Required shared pieces |
|------|------------------------|
| Dashboard | portal-page, pills, empty, modal, staff-btn optional |
| Calendar | portal-page, tabs, pills, empty |
| Bookings list | portal-page, tabs, pills, empty; **prefer** `AdeniListToolbar` + list/table patterns for desktop density ([shared-data-grid.md](./shared-data-grid.md)) without breaking mobile cards |
| Booking detail | portal-page, pills |
| Messages | portal-page or equivalent chrome; keep split pane |
| Benefits hub + children | portal-page + path tabs (hub); **remove** embedded `.customer-page` heads; list panels under `_app-surface.scss` |
| Profile | portal-page |
| Discover-in-app | portal-page wrapper; discovery widgets with `surface="app"` |

**Delete when unused:** `apps/customer/src/app/layout/_customer-page.scss`.

**Naming:** keep `PortalPageComponent` / `adeni-staff-*` names (C1 Q7); no rename sweep in C2.

---

## 8. Files to touch

| Layer | Files |
|-------|-------|
| Domain / Application / Infrastructure / Api | **None** |
| Frontend — customer | `app.routes.ts`, `customer-nav.ts`, `shell/*`, `app-surface/pages/dashboard/*` (from home), `app-surface/pages/calendar/*` (new), bookings/messages/benefits/profile polish, `_app-surface.scss`, delete `_customer-page.scss` |
| `@adeni/ui` | Only if a truly shared primitive appears (e.g. neutral page alias) — default **no** |
| `@adeni/shared` | i18n overlays EN/FR; optional `customer-dashboard.ts` helpers; seo/nav docs untouched |
| `@adeni/api-client` | None |
| Docs | this spec, `sprints.md`, `flutter-consumer-app.md`, `frontend.md` one-liner |
| Tests | nav + redirect specs, dashboard helpers, calendar bucketing, i18n parity |

---

## 9. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | N/A for consumer me-scoped reads; booking detail still tenant-scoped via existing APIs |
| **Auth / roles** | Customer JWT + existing `customerAuthGuard`; default return `/app/dashboard` |
| **Cache** | No new Redis keys; client-only calendar bucketing |
| **Audit** | None (no admin) |
| **PII** | Do not log booking/message bodies; review modal comment stays client-side until POST |
| **Observability** | Preserve `X-Correlation-Id`; no new events required in C2 |
| **SEO** | `/app/**` remains CSR + noindex; sitemap unchanged |
| **SSR** | No public HTML change beyond existing Open app CTA copy if label changes |

---

## 10. Phased delivery

| Phase | Goal | Exit |
|-------|------|------|
| **A — IA + shell** | Nav ids/paths, redirects `/app/home`→`dashboard`, `CUSTOMER_APP_HOME`, Open app target, tab set per Q1 | Route contract tests green |
| **B — Dashboard** | Rename/upgrade Home → Dashboard; shared chrome; helpers tests | Manual smoke dashboard cards |
| **C — Calendar** | Month/week UI + day list + deep links | Helper + component tests |
| **D — Shared UI sweep** | Bookings/Benefits/Messages/Profile on shared kit; delete `_customer-page.scss` | Visual parity checklist; build budget OK |
| **E — Docs + freeze** | Flutter stub, frontend note, spec → Implemented | PR ready |

Phases B and C may parallelize after A.

---

## 11. Tests

- [ ] Unit: dashboard helpers (upcoming, review, benefits currency split) — extend existing home helpers specs
- [ ] Unit: calendar `bookingsInMonth` / `bookingsOnDay` / week range (locale-safe)
- [ ] Unit: `customer-nav` ids include `dashboard` + `calendar`; no `home`
- [ ] Route: `/app` default → dashboard; `/app/home` redirects; `isSeoMarketId` still excludes `app`
- [ ] Auth return allowlist accepts `/app/dashboard` and `/app/calendar`
- [ ] i18n EN/FR parity for new keys
- [ ] No backend suite required

Commands: `npm test --workspace @adeni/customer`, `npm test --workspace @adeni/shared`.

---

## 12. Acceptance criteria

- [ ] Desktop top bar presents Dashboard, Calendar, Discover, Bookings, Messages, Benefits as primary menu; Profile via account menu.
- [ ] Mobile tab bar matches locked 5-tab set (Q1); active `aria-current`.
- [ ] `/app/dashboard` is the default landing; `/app/home` redirects; Open app → dashboard.
- [ ] Dashboard uses only existing APIs; single failing call degrades one card.
- [ ] Calendar shows the customer’s appointments by month/week without tenant calendar APIs.
- [ ] All `/app/*` pages use `app-portal-page` (or documented exception for messages split); no `.wash` / `.customer-page` chrome remains.
- [ ] Discover remains the marketplace exception (search → public business URL).
- [ ] EN/FR parity; customer unit tests pass; flutter stub nav table updated.

---

## 13. Risks & non-goals

| # | Risk | Mitigation |
|---|------|------------|
| R1 | Tab bar overcrowding if Bookings + Calendar both tabs | Lock 5 tabs in Q1; Bookings via menu/dashboard |
| R2 | Calendar reinventing staff schedule UI | Consumer-only month/week; no extract to ui until proven |
| R3 | Nav id break for Flutter/analytics | Same PR updates flutter stub + constant rename |
| R4 | Scope creep into Sprint 32 widget system | Explicit out of scope; fixed dashboard cards |
| R5 | Shared data-grid SCSS budget | Prefer toolbar + compact lists; avoid pulling full grid SCSS into customer if budget regresses |
| R6 | Users expect Google Calendar sync | Copy clarifies “Your Adeni appointments”; sync is future |

**Non-goals:** Option B sidebar, wallet, in-app booking page, new APIs, Flutter screens.

---

## 14. Open questions

| Question | Recommendation |
|----------|----------------|
| **Q1.** Mobile 5-tab set: Dashboard · Calendar · Discover · Messages · Profile (Bookings desktop/menu)? | **Yes** — default for C2 |
| **Q2.** Keep `/app/home` redirect forever or remove after one release? | **Keep redirect** (cheap; bookmarks) |
| **Q3.** Week view in C2 or month-only MVP? | **Month + week** if week is cheap; else month-only with week as follow-up |
| **Q4.** Bookings desktop: card list vs `AdeniDataTable`? | **Toolbar + responsive list/cards first**; table if desktop density still weak after D |
| **Q5.** Promote dashboard helpers to `@adeni/shared` in C2? | **Only if Flutter work starts**; else leave in app-surface |

---

## 15. Recommended order

1. Review/approve this spec (lock Q1 tab set).  
2. Phase A (IA) → B (Dashboard) → C (Calendar) → D (shared UI) → E (docs).  
3. Do **not** start Option B shell or in-shell booking without a new spec.
