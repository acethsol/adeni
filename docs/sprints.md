# Sprint plan

## Status overview (July 2026)

| Sprint | Name | Status |
|--------|------|--------|
| 0 | Foundation & dev tooling | ✅ Done |
| 1 | Business onboarding | ✅ Done |
| 2 | Discovery API | ✅ Done |
| 3 | Auth0 + client foundation | ✅ Done |
| 3b | Web public shell | ✅ Done |
| 4 | Booking | ✅ Done |
| 5 | Expo mobile loop | ✅ Done |
| 6 | Business portal (web) | ✅ Done |
| 7 | Customer bookings | ✅ Done |
| 8 | MVP gap closure | ✅ Done |
| 9 | Mobile business onboarding | ✅ Done |
| 10 | Design system & caching | ✅ Done |
| 11 | Discovery UX | ✅ Done |
| **12** | **Media & tenant hardening** | ✅ Done |
| **13** | **Reviews & ratings** | ✅ Done |
| **14** | **UX polish & guardrails** | ✅ Done |
| 15 | Architecture foundation + booking v2 + business types | ✅ Done |
| **16** | **Business SaaS & monetization** | ✅ Done |
| **17** | **Commerce orchestration (Paystack)** | ✅ Done |
| **18** | **Messaging & WhatsApp bridge** | Planned |
| **19** | **Trust depth & quote workflows** | Planned |
| 20 | Deployment, AI & observability | Planned |
| — | Sprint 11d LLM agent | → Sprint 20 |
| — | Staging deploy + Auth0 E2E | → Sprint 20 |
| — | App Insights (Obs 1) | → Sprint 20 |
| — | Booking notifications, auto-confirm, payments, waitlist | → Sprint 15 |

**Next up:** Sprint 18 (**Messaging & WhatsApp bridge**). See [product-strategy.md §7.4](./product-strategy.md#74-nigeria-specific-engineering-notes).

---
## Sprint 0 — Foundation & dev tooling ✅

- [x] DDD scaffold, SOC 2 Sprint 0 controls
- [x] Docker Postgres + Redis, OpenAPI/Scalar, CI, dev UIs

## Sprint 1 — Business onboarding ✅

Register → submit docs → admin approves → tenant `Verified`.

## Sprint 2 — Discovery ✅

- `GET /api/v1/discovery`, `GET /api/v1/businesses/{slug}`
- Redis keys: `discovery:*`, `tenant:{id}:profile`

## Sprint 3 — Auth0 + client foundation ✅

**Backend (done):**

- [x] `GET /api/v1/auth/me`
- [x] CORS for web clients
- [x] `appsettings.Staging.json`

**Frontend pivot (July 2026 — ADR-010):**

- [x] Confluence updated ([Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065))
- [x] Flutter archived → `mobile/_archive/adeni_app_flutter`
- [x] Monorepo: `apps/web` (Next.js), `apps/mobile` (Expo), `packages/*`

## Sprint 3b — Web public shell ✅

**Goal:** SEO-ready discovery landing wired to existing API; market-aware, industry-neutral shell.

| Task | Status |
|------|--------|
| Public landing | ✅ `/` — categories from API, runtime market context |
| Business profile SSR | ✅ `/businesses/[slug]` |
| Discover page | ✅ `/discover` → discovery API + category filters |
| Dynamic market/categories | ✅ `packages/shared` market config; generic category API |
| Auth0 Next.js SDK | ✅ Login + role-gated `/business` and `/admin` |

## Sprint 4 — Booking ✅

Services CRUD, availability, `POST /api/v1/bookings`, Redis slot locks, geo markets, multi-branch locations, web booking UI.

## Sprint 5 — Expo mobile loop ✅

Unified app: browse, book, business booking management.

| Task | Status |
|------|--------|
| API client + market/geo context | ✅ |
| Home tab (categories, market copy) | ✅ |
| Discover tab (list + category filters) | ✅ |
| Business profile screen | ✅ |
| Customer booking flow | ✅ |
| Auth0 Native login | ✅ |
| Business booking management | ✅ |

## Sprint 6 — Business portal (web) ✅

Bring the web business portal to parity with mobile — profile, services, availability, booking inbox.

| Task | Status |
|------|--------|
| Portal shell + dev business auth | ✅ |
| Dashboard overview | ✅ |
| Booking inbox (accept/reject) | ✅ |
| Profile view + edit | ✅ |
| Services list | ✅ |
| Services CRUD UI | ✅ |
| Weekly availability UI | ✅ |
| Verification submission | ✅ |
| Multi-location management | ✅ |

## Sprint 7 — Customer bookings ✅

Let customers view booking history on web and mobile.

| Task | Status |
|------|--------|
| `GET /api/v1/bookings` (customer list) | ✅ |
| Shared schema + API client | ✅ |
| Web `/my-bookings` page | ✅ |
| Mobile my bookings screen | ✅ |

## Sprint 8 — MVP gap closure ✅

Close remaining product gaps before staging deploy.

| Task | Status |
|------|--------|
| Admin approve/reject UI | ✅ |
| Business self-signup web flow | ✅ |
| Customer cancel booking | ✅ |
| SOC2-09 customer export/delete admin | ✅ |

## Sprint 9 — Mobile business onboarding ✅

Business owners can register and manage verification from the Expo app.

| Task | Status |
|------|--------|
| Business tenant resolution via `getBusinessContext` | ✅ |
| Register business screen | ✅ |
| Profile edit + verification submit | ✅ |
| Account tab business navigation | ✅ |
| Booking inbox uses resolved tenant | ✅ |

## Sprint 10 — Design system, polish & client caching ✅

Shared tokens, UI primitives, empty/loading states, and TanStack Query on web + mobile.

| Task | Status |
|------|--------|
| `@adeni/shared` design tokens + query keys | ✅ |
| Web `components/ui/*` + Tailwind theme | ✅ |
| Mobile `components/ui/*` + shared theme | ✅ |
| Public shells & key pages refactored | ✅ |
| TanStack Query (categories, discovery, bookings) | ✅ |
| Docs: [design-system.md](./design-system.md) | ✅ |

## Sprint 11 — Discovery UX ✅

Global search, visual discovery cards, fused Ask Adeni search, and profile heroes.

| Task | Status |
|------|--------|
| API `GET /discovery?q=` keyword filter | ✅ |
| Fused DiscoverySearch (web header + mobile home/discover) | ✅ |
| Category visuals + image business cards | ✅ |
| Ask Adeni rule-based intent parser | ✅ |
| Business profile hero image (category fallback) | ✅ |
| Business cover upload + blob storage | Deferred → [media-storage.md](./media-storage.md) |
| LLM agent (11d) | → Sprint 20 |

Confluence: [Sprint 11 — Discovery UX](https://aceth.atlassian.net/wiki/spaces/SD/pages/28540929)

## Sprint 12 — Media & tenant hardening ✅

| Task | Status |
|------|--------|
| **12a** `IFileStorage` port (Local + Azure Blob) | ✅ |
| **12b** `cover_image_key` + presigned upload URL API | ✅ |
| **12c** Business portal cover photo upload UI (web + mobile) | ✅ |
| **12e** Tenant isolation hardening (see below) | ✅ |

**Deferred to Sprint 20:** staging deploy, Auth0 E2E, LLM agent (11d), App Insights.

### 12e — Tenant isolation hardening

Foundation exists (middleware + EF global filters + audit). Sprint 12 closes gaps before staging.

| Task | Status |
|------|--------|
| Integration tests: tenant A cannot access tenant B bookings/services | ✅ |
| Integration tests: EF filter blocks cross-tenant reads when filter enabled | ✅ |
| Architecture test: every `ITenantEntity` has a global query filter | ✅ |
| Expand `AdeniDbContextTenantFilterTests` (bookings, services, locations) | ✅ |
| Document cross-tenant vs intentionally public routes | ✅ `docs/tenant-isolation.md` |
| Cache key convention: tenant-private keys must include `{tenantId}` | ✅ `docs/tenant-isolation.md` |

**Current model (no change):** `/api/v1/tenant/*` requires `X-Tenant-Id` → `TenantAccessMiddleware` → EF filter ON. Public discovery/customer routes intentionally cross-tenant. Admin routes role-gated, filter OFF.

See [media-storage.md](./media-storage.md).

Confluence: [Sprint 12 — Media & tenant hardening](https://aceth.atlassian.net/wiki/spaces/SD/pages/28540956)

## Sprint 13 — Reviews & ratings ✅

| Task | Status |
|------|--------|
| `reviews` schema + one-review-per-booking constraint | ✅ |
| `POST /bookings/{id}/review` + public list API | ✅ |
| `ratingAvg` / `reviewCount` on discovery + profile DTOs | ✅ |
| Customer review flow (web + mobile my-bookings) | ✅ |
| Star ratings on discovery cards + profile section | ✅ |
| Admin soft-hide + audit (`review.hidden`) | ✅ |
| Live E2E + integration test (completed booking → review → public ratings) | ✅ |

Confluence: [Sprint 13 — Reviews & ratings](https://aceth.atlassian.net/wiki/spaces/SD/pages/28672001)

## Sprint 14 — UX polish & guardrails ✅

Cross-cutting user-experience gaps identified outside the booking flow: feedback, guardrails, consistency, and discovery.

| Task | Status |
|------|--------|
| **14a** Global feedback layer — web `error.tsx` / `not-found.tsx`, toast/snackbar system for save/copy confirmations | ✅ |
| **14b** Field-level form validation — wire existing `ui/input.tsx` error state into register, verification, locations, profile forms | ✅ |
| **14c** Confirmation dialogs — shared `Dialog`/`AlertDialog` component; apply to reject booking, remove location, deactivate service, approve business, toggle market live, cancel booking (web + mobile) | ✅ |
| **14d** Empty-state consistency — replace plain "No X yet" text with `EmptyState` in locations, services, booking inbox, admin queue, mobile my-bookings | ✅ |
| **14e** Discovery filters — expose sort (distance/featured), add price/rating filters, "clear all filters" affordance (web + mobile) | ✅ |
| **14f** Accessibility pass — skip-to-content link, `focus-visible` rings on `Button`/`BackLink`, `aria-label` on icon-only controls (stretch) | ✅ |

**Also delivered (portal polish):** collapsible sidebar, top bar with notifications/user menu, availability multi-block calendar, profile verification stepper, business reviews panel, overview metrics/charts, unsaved-changes navigation guard, dirty-form save guards, profile lock when verified.

**Out of scope for 14:** staging deploy, LLM agent, App Insights → Sprint 20. Booking notifications/auto-confirm/payments/waitlist → Sprint 15.

Confluence: [Sprint 14 — UX polish & guardrails](https://aceth.atlassian.net/wiki/spaces/SD/pages/30769154)

## Sprint 15 — Architecture foundation + booking v2 + business types ✅ Done

**Lead with architecture** — modular monolith hardening (moved from Sprint 20b) so Notifications, Payments, and Messaging modules land on enforced boundaries, not ad-hoc coupling. Then close booking gaps and lay the **business-type workflow foundation** — see [architecture.md](./architecture.md), [product-strategy.md §3.5](./product-strategy.md#35-business-types--workflow-capabilities).

| Task | Status |
|------|--------|
| **15a** Modular monolith hardening — `IDomainEvent` + in-process dispatcher; per-module `AddXxxModule()` DI; NetArchTest (or equivalent) forbidding cross-module Infrastructure references; refactor existing Booking/Tenancy/Discovery/Reviews into registered modules | ✅ Done |
| **15b** Booking notifications — email and/or push when status changes (pending → confirmed/rejected, cancellation); **Notifications module** via domain events | ✅ Done (logging stub) |
| **15c** Auto-confirm — tenant setting to skip manual accept for trusted businesses | ✅ Done |
| **15d** Slot freshness — exclude past slots in API + client; friendly error if slot expires before submit | ✅ Done |
| **15e** Payments at booking — deposit or pay-in-full via payment provider (design + API + web/mobile checkout step) | ✅ Done (stub provider + checkout placeholder) |
| **15f** Waitlist — customer joins waitlist when no slots; notify when slot opens | ✅ Done (schema + API + notification stub) |
| **15g** Recurring bookings — repeat weekly/biweekly option on confirm step (stretch) | Deferred — backlog (Sprint 15 stretch) |
| **15h** `BusinessType` on tenant — `scheduled_appointment` \| `quote_request`; category → default mapping in catalog config | ✅ Done |
| **15i** Capabilities API — tenant DTO returns `businessType` + `capabilities[]`; shared config in `@adeni/shared` | ✅ Done |
| **15j** Type-aware onboarding + portal — calendar-first for appointment types; quote-oriented setup for quote types; hide irrelevant nav | ✅ Done (portal nav + profile settings) |
| **15k** Customer journey branches — “Book now” for appointment types; “Get a quote” stub (describe job form, no full quote engine yet) for quote types; discovery card CTA by type | ✅ Done |

**v1 business types:** `scheduled_appointment` (Lagos beauty wedge) + `quote_request` (home services). Walk-in queue, recurring, dispatch → backlog.

Confluence: [Sprint 15 — Booking experience v2](https://aceth.atlassian.net/wiki/spaces/SD/pages/30801921)

## Sprint 16 — Business SaaS & monetization ✅ Done

Turn the business portal from a free tool into a **monetizable operating system**. Foundation for MRR/ARR. See [product-strategy.md §4.1](./product-strategy.md#41-saas-subscriptions--foundation-mrrarr).

| Task | Status |
|------|--------|
| **16a** Subscription tiers schema — `Free` / `Pro` / `Business` entitlements on tenant; feature flags in API | ✅ Done |
| **16b** Entitlements middleware — gate bookings/month, messaging, analytics, multi-location by tier | ✅ Done |
| **16c** Portal upgrade UX — plan comparison, upgrade prompts, usage meters (bookings used/limit) | ✅ Done |
| **16d** Business analytics v2 — revenue trends, booking funnel, repeat-customer rate, service mix (build on overview charts) | ✅ Done |
| **16e** Share kit — copy booking link, QR code, Instagram/WhatsApp share templates for public profile | ✅ Done |
| **16f** Billing provider design — Paystack Subscriptions or Stripe Billing integration spec; webhook handling (implement stub) | ✅ Done (stub + [subscription-billing.md](./subscription-billing.md)) |
| **16g** Admin tier override — manually set tenant plan for pilot businesses | ✅ Done |

**Revenue unlocked:** SaaS MRR. **Metric:** paying businesses × ARPU.

Confluence: [Sprint 16 — Business SaaS & monetization](https://aceth.atlassian.net/wiki/spaces/SD/pages/41222145)

## Sprint 17 — Commerce orchestration ✅

**Non-custodial fintech** — Adeni orchestrates payments; licensed providers move money. Highest ROI: payment links usable on WhatsApp without marketplace traffic. See [product-strategy.md §4.3](./product-strategy.md#43-fintech--orchestration-not-custody) and [payments.md](./payments.md).

| Task | Status |
|------|--------|
| **17a** `IPaymentProvider` port + `IPaymentOrchestrator` + `PaymentIntent` domain — **Payments module**; provider-agnostic; no wallet/balance tables | ✅ Done |
| **17b** Paystack integration (NG) — initialize transaction, webhook confirm, idempotency | ✅ Done |
| **17c** Payment links & invoices — business creates “Pay ₦X” link; share via WhatsApp/SMS; receipt on success | ✅ Done |
| **17d** Booking deposit flow — optional deposit % at confirm; tie to Sprint 15 payments step | ✅ Done |
| **17e** Transaction ledger — business-facing payment history, reconciliation view in portal | ✅ Done |
| **17f** Platform fee config — Adeni take-rate (0% during Lagos pilot; configurable per market) | ✅ Done |
| **17g** Refund orchestration — initiate refund via provider; update booking/payment status | ✅ Done |

**Revenue unlocked:** GMV × take rate + payment rev-share. **Metric:** GMV per active business.

Confluence: [Sprint 17 — Commerce orchestration (Paystack)](https://aceth.atlassian.net/wiki/spaces/SD/pages/41287681)

## Sprint 18 — Messaging & WhatsApp bridge (planned)

Close the fragmentation gap — businesses live on WhatsApp; Adeni must meet them there. See [product-strategy.md §7.4](./product-strategy.md#74-nigeria-specific-engineering-notes).

| Task | Status |
|------|--------|
| **18a** In-app messaging — **Messaging module**; customer ↔ business threads tied to booking or business profile | Planned |
| **18b** WhatsApp deep links — “Message on WhatsApp” with pre-filled booking context; “Book on Adeni” link in bio templates | Planned |
| **18c** Notification preferences — email, push (FCM), SMS/WhatsApp reminder channel per tenant | Planned |
| **18d** Message templates — business quick-replies (hours, pricing, location) | Planned |
| **18e** Unread badge + inbox in business portal topbar (extend bookings bell pattern) | Planned |
| **18f** Basic FAQ auto-responder — rule-based replies for price/hours/availability (precursor to business AI agent) | Planned |

**Revenue unlocked:** Pro tier messaging entitlement (Sprint 16). **Retention:** businesses stay because customers reach them where they already are.

Confluence: [Sprint 18 — Messaging & WhatsApp bridge](https://aceth.atlassian.net/wiki/spaces/SD/pages/41320449)

## Sprint 19 — Trust depth & quote workflows (complete)

Differentiate on trust and **service-type flexibility** — builds on the business-type framework from Sprint 15. See [product-strategy.md §3.5](./product-strategy.md#35-business-types--workflow-capabilities). Spec: [sprint-19-trust-quotes.md](./specs/sprint-19-trust-quotes.md).

| Task | Status |
|------|--------|
| **19a** Tiered verification badges — phone, CAC, address, license; display on profile + discovery cards; admin grant workflow | Done |
| **19b** Verification upgrade UX — business requests advanced checks; admin review queue shows documents | Done |
| **19c** Service pricing types — `fixed` vs `quote_request` vs `hourly` on offerings; constrained by tenant `businessType` | Done |
| **19d** Full quote request flow — customer describes job; business submits quote; customer accepts → books | Done |
| **19e** Business review responses — owner can reply publicly to customer reviews | Done |
| **19f** Discovery trust signals — badge stack, “Verified since”, completion rate (when data exists) | Done |
| **19g** CAC verification integration research — research doc (stretch) | Done |
| **19h** Category-specific verification requirements — license badge required for `plumbers`/`electricians` in discovery | Done |

**Revenue unlocked:** premium verification checks (transparent, optional). **Differentiator:** instant quotes for variable-price services (photographers, plumbers, caterers).

Confluence: [Sprint 19 — Trust depth & quote workflows](https://aceth.atlassian.net/wiki/spaces/SD/pages/41189385)

## Sprint 20 — Deployment, AI & observability (planned)

Infrastructure and platform work deferred from the original Sprint 14 plan. **Modular monolith hardening moved to Sprint 15a** — see [architecture.md](./architecture.md).

| Task | Status |
|------|--------|
| Staging deploy + Auth0 E2E (was 12d) | Planned |
| Sprint 11d — LLM Ask Adeni agent (tool-calling on discovery/booking APIs) | Planned |
| **20c** Business AI agent MVP — auto-reply to FAQs using tenant services/hours/business type (paid Pro add-on); **tools call module APIs only** (depends on Sprint 15a module boundaries) | Planned |
| Obs 1 — App Insights on API | Planned |
| **20e** GMV / MRR / active-business dashboards — ops metrics aligned with [product-strategy.md §2](./product-strategy.md#2-go-to-market-wedge-nigeria-first-global-architecture) | Planned |

See [observability.md](./observability.md) for Obs 2–4 follow-ons.

Confluence: [Sprint 20 — Deployment, AI, observability & architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/30834689)

## Next up

1. **Sprint 18** — Messaging & WhatsApp bridge
2. **Sprint 18** — messaging + WhatsApp bridge
3. **Sprint 19** — tiered verification, **full quote workflows** (on Sprint 15 framework)
4. **Sprint 20** — staging, LLM agent, observability

Strategy reference: [product-strategy.md](./product-strategy.md) — [§3.5 Business types](./product-strategy.md#35-business-types--workflow-capabilities), [architecture.md](./architecture.md) (modular monolith)
