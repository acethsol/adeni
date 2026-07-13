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
| **14** | **UX polish & guardrails** | **Planned** |
| 15 | Booking experience v2 | Planned |
| 16–19 | — (unscheduled) | — |
| 20 | Deployment, AI & observability | Planned |
| — | Sprint 11d LLM agent | → Sprint 20 |
| — | Staging deploy + Auth0 E2E | → Sprint 20 |
| — | App Insights (Obs 1) | → Sprint 20 |
| — | Booking notifications, auto-confirm, payments, waitlist | → Sprint 15 |

**Next up:** Sprint 14 (error page, toasts, form validation, confirm dialogs, empty states, discovery filters).

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

## Sprint 14 — UX polish & guardrails (planned)

Cross-cutting user-experience gaps identified outside the booking flow: feedback, guardrails, consistency, and discovery.

| Task | Status |
|------|--------|
| **14a** Global feedback layer — web `error.tsx` / `not-found.tsx`, toast/snackbar system for save/copy confirmations | Planned |
| **14b** Field-level form validation — wire existing `ui/input.tsx` error state into register, verification, locations, profile forms | Planned |
| **14c** Confirmation dialogs — shared `Dialog`/`AlertDialog` component; apply to reject booking, remove location, deactivate service, approve business, toggle market live, cancel booking (web + mobile) | Planned |
| **14d** Empty-state consistency — replace plain "No X yet" text with `EmptyState` in locations, services, booking inbox, admin queue, mobile my-bookings | Planned |
| **14e** Discovery filters — expose sort (distance/featured), add price/rating filters, "clear all filters" affordance (web + mobile) | Planned |
| **14f** Accessibility pass — skip-to-content link, `focus-visible` rings on `Button`/`BackLink`, `aria-label` on icon-only controls (stretch) | Planned |

**Out of scope for 14:** staging deploy, LLM agent, App Insights → Sprint 20. Booking notifications/auto-confirm/payments/waitlist → Sprint 15.

Confluence: [Sprint 14 — UX polish & guardrails](https://aceth.atlassian.net/wiki/spaces/SD/pages/30769154)

## Sprint 15 — Booking experience v2 (planned)

Close the gaps between MVP booking (request → manual accept) and a production-ready customer/business loop.

| Task | Status |
|------|--------|
| **15a** Booking notifications — email and/or push when status changes (pending → confirmed/rejected, cancellation) | Planned |
| **15b** Auto-confirm — tenant setting to skip manual accept for trusted businesses | Planned |
| **15c** Slot freshness — exclude past slots in API + client; friendly error if slot expires before submit | Planned |
| **15d** Payments at booking — deposit or pay-in-full via payment provider (design + API + web/mobile checkout step) | Planned |
| **15e** Waitlist — customer joins waitlist when no slots; notify when slot opens | Planned |
| **15f** Recurring bookings — repeat weekly/biweekly option on confirm step (stretch) | Planned |

Confluence: [Sprint 15 — Booking experience v2](https://aceth.atlassian.net/wiki/spaces/SD/pages/30801921)

## Sprint 20 — Deployment, AI & observability (planned)

Infrastructure and platform work deferred from the original Sprint 14 plan.

| Task | Status |
|------|--------|
| Staging deploy + Auth0 E2E (was 12d) | Planned |
| Sprint 11d — LLM Ask Adeni agent | Planned |
| Obs 1 — App Insights on API | Planned |

See [observability.md](./observability.md) for Obs 2–4 follow-ons.

Confluence: [Sprint 20 — Deployment, AI & observability](https://aceth.atlassian.net/wiki/spaces/SD/pages/30834689)

## Next up

1. **Sprint 14** — UX polish & guardrails (error page, toasts, form validation, confirm dialogs, empty states, discovery filters)
2. **Sprint 15** — booking notifications, auto-confirm, slot UX, payments, waitlist
3. **Sprint 20** — staging, LLM agent, App Insights
