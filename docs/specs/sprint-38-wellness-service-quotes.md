# Spec: Sprint 38 — Wellness service quotes (custom estimates)

> **Option B** from Sprint 37 follow-up: per-offering “price on request” for **`scheduled_appointment`** wellness tenants, without adopting the whole-business **`quote_request`** (home-services) operating model.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 38 — Wellness service quotes |
| **Author** | Agent + product |
| **Status** | Draft |
| **Created** | 2026-10-19 |
| **Authority** | [product-direction.md](../product-direction.md) (Beauty & Wellness V1); supersedes treating `quote_request` as the wellness default |

---

## 1. Goal (one sentence)

Let **wellness businesses that mostly take appointments** offer **selected services as “custom estimate”** (bridal, packages, complex treatments) with the same **quote inbox → offer → customer accept → pending booking** pipeline staff already have, without flipping the tenant to **`quote_request`** or showing a home-services-style “describe your job” profile.

---

## 2. Context

- **Today:** Sprint **19d** shipped full quote lifecycle (`IQuoteRequestService`, portal **Quotes**, `QuotePipelineChanged`). Discovery shows **`quote-request-panel`** only when `shouldShowQuoteFlow()` — i.e. tenant **`quote_request`**, `discoveryCta: get_quote`, or legacy **`quotes`** capability (bundled with home-services in `category-workflows.json`).
- **Wellness defaults:** All wellness category slugs in `category-workflows.json` use **`scheduled_appointment`** + fixed menu booking. **`ValidatePricingTypeAsync`** blocks **`pricingType: quote_request`** on offerings unless tenant **`businessType === quote_request`**.
- **Gap:** A spa/salon cannot mark “Bridal makeup — price on request” on an otherwise normal calendar business; customers have **no Discover “My quotes”** to accept offers; profile CTA is all-or-nothing book vs quote.
- **Related:** [sprint-19-trust-quotes.md](./sprint-19-trust-quotes.md), [sprint-37-realtime-expansion.md](./sprint-37-realtime-expansion.md) (staff push wired; customer push not).
- **Modules:** Booking (quotes + catalog), Tenancy (capabilities on profile), Discovery + Portal clients, optional Messaging (`ConversationContextType.Quote`).

---

## 3. In scope

- [ ] **Capability model:** Introduce **`service_quotes`** in `@adeni/shared` (`capabilities.ts`, category-workflows where product opts categories in — e.g. makeup-brows, skincare-aesthetics; **not** plumbers/electricians as V1 wellness).
- [ ] **Tenant stays** `scheduled_appointment`; **`quote_request` business type** unchanged for non–wellness-V1 demos/backlog only.
- [ ] **Catalog rule change:** Allow offering **`pricingType: quote_request`** (API name unchanged) when tenant has **`service_quotes`**, even if `businessType !== quote_request`. Fixed/hourly rules unchanged.
- [ ] **Create quote API:** Extend **`CreateQuoteRequestRequest`** with optional **`serviceOfferingId`**; validate offering belongs to tenant, is quote-priced, tenant has capability. **`serviceAddress`** required only if tenant has **`on_site_address`** (wellness usually optional).
- [ ] **Discovery profile UX:** If tenant has **`service_quotes`** and ≥1 quote-priced offering: show **booking panel for fixed services** and **“Request estimate”** entry points (per service row + optional rail CTA). Do **not** replace entire rail with home-services quote form when fixed services exist.
- [ ] **Discover customer:** **`/my-quotes`** — list, detail, accept/decline (wire existing `listCustomerQuotes`, `acceptQuote`, `declineQuote` in `@adeni/api-client`).
- [ ] **Portal:** Show **Quotes** nav + dashboard widget when **`service_quotes`** OR existing **`quotes`** capability (backward compatible). Optional **nav badge** on new submission (REST count or reuse `QuotePipelineChanged`).
- [ ] **Copy / i18n:** Wellness-oriented strings (“Request estimate”, “Custom package”) vs “Get a quote” / job language; keys in `packages/shared` locales.
- [ ] **Realtime:** Reuse existing **`QuotePipelineChanged`** (no new hub event). Customer “quote ready” = **phase 2** (email/push or messaging) — see §4.
- [ ] **Tests:** Catalog validation matrix; create with `serviceOfferingId`; cross-tenant denial; customer accept → pending booking + pending bookings push (already implemented on accept).

---

## 4. Out of scope

- Whole-business **`quote_request`** onboarding for wellness categories (no default flip in category JSON for hair/spa/yoga).
- **Home services** expansion (plumbers, electricians) — remove or hide from wellness onboarding paths; not part of this sprint’s UX.
- **Mixed hourly + quote** on same tenant beyond existing hourly rules.
- **LLM** quote drafting, automated pricing, CRM pipelines.
- **Customer SignalR** for quote status (use My quotes + optional notification sprint later).
- **Pay-to-quote**, deposits on quote-before-offer (deposit after accept keeps current booking/deposit flow).
- **Mobile Flutter** full parity in Sprint 38 unless explicitly added as task 38m (web Discover + Portal required).

---

## 5. Product rules (wellness)

| Rule | Detail |
|------|--------|
| **Who gets `service_quotes`** | API-driven **`capabilities`** on tenant profile (from category template + entitlement/settings). Default **off** for all wellness categories until template or portal toggle enables it. |
| **Profile CTA** | Primary CTA remains **`book_now`** when any fixed-price bookable service exists. Secondary: **Request estimate** when quote-priced services exist. |
| **Quote form** | Short description + optional photos; **pick service** (required when multiple quote offerings). No mandatory job-site address unless capability **`on_site_address`**. |
| **Staff offer** | Same as today: amount, currency, proposed slot, link **`serviceOfferingId`** (prefill from request when present). |
| **Accept** | Creates **pending booking** (existing); customer can complete deposit via existing payment loop. |
| **vs `quote_request` type** | **`quote_request`** tenant = entire profile is job-quote flow (legacy 19 + demo categories). **`service_quotes`** = appointment business with **some** custom-priced menu items. |

---

## 6. API contract

### Changes (v1 additive)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/v1/quotes/businesses/{slug}` | Customer JWT | Create quote — **add optional `serviceOfferingId`** in body |
| GET | `/api/v1/quotes/me` | Customer JWT | List customer quotes (existing) |
| POST | `/api/v1/quotes/{id}/accept` | Customer JWT | Accept (existing) |
| POST | `/api/v1/quotes/{id}/decline` | Customer JWT | Decline (existing) |
| GET | `/api/v1/tenant/quotes` | Business JWT + tenant | List (existing) |
| POST | `/api/v1/tenant/quotes/{id}/offer` | Business JWT + tenant | Submit offer (existing) |

**`CreateQuoteRequestRequest` (shared Zod):**

```json
{
  "description": "string, min 10",
  "serviceAddress": "string | optional",
  "photoKeys": ["string"] ,
  "serviceOfferingId": "uuid | optional"
}
```

**Validation (new / updated stable codes):**

| Code | When |
|------|------|
| `quote.not_enabled` | Tenant lacks `service_quotes` and is not `quote_request` / legacy `quotes` |
| `quote.service_required` | Multiple quote offerings and no `serviceOfferingId` |
| `quote.invalid_service` | Offering not found, wrong tenant, or not quote-priced |
| Existing | `booking.closed`, auth, not found |

**Public profile:** Expose `capabilities` including `service_quotes`; discovery may expose `hasQuotePricedServices: boolean` (computed) to avoid N+1 on client.

---

## 7. UI surfaces

| Surface | Behavior |
|---------|----------|
| **Discover business profile** | Branch on `hasCapability(service_quotes)` + quote offerings; hybrid book + estimate |
| **Discover `/my-quotes`** | Tabs: open (submitted/quoted) / closed; accept with slot summary |
| **Portal services** | Allow “Price on request” pricing type when `service_quotes` |
| **Portal quotes** | Unchanged; visible if `service_quotes` \|\| `quotes` |
| **Portal settings (optional 38b)** | Toggle “Accept custom estimate requests” if not only category-driven |

**`shouldShowQuoteFlow` (shared):** Deprecate as sole gate for full-page quote rail; replace with:

- `shouldShowServiceQuotes(profile, services)` — service-scoped
- Keep `shouldShowQuoteFlow` for **whole-business** `quote_request` tenants only

---

## 8. Files to touch

| Layer | Files |
|-------|-------|
| Domain | Optional: quote source enum (`WholeBusiness` vs `ServiceOffering`) — only if needed for analytics |
| Application | `IQuoteRequestService` / DTOs; catalog validation port |
| Infrastructure | `QuoteRequestService.CreateBySlugAsync`, `ServiceCatalogService.ValidatePricingTypeAsync`, capability resolution on profile |
| Api | Quotes controllers (customer create body); discovery/profile DTO if adding `hasQuotePricedServices` |
| Tests | Infrastructure quote + catalog tests; Api integration customer create with offering id |
| `packages/shared` | `capabilities.ts`, `category-workflows.ts` + JSON, schemas, i18n, `shouldShowServiceQuotes` |
| `packages/api-client` | Create quote body; my-quotes helpers if missing |
| `apps/discover` | `business.component`, `booking-panel` or service list, **`my-quotes`**, routes, shell nav |
| `apps/portal` | Nav capability gate (`service_quotes`), services UI pricing type, optional badge |
| `apps/mobile` | Out of scope unless 38m |

---

## 9. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Quote records already tenant-scoped; offering id must match tenant |
| **Auth** | Customer mutations JWT; business routes tenant header + permissions **`portal.quotes`** |
| **Cache** | Invalidate `tenant:{id}:profile` on capability/settings change |
| **Audit** | No new admin routes |
| **PII** | Description/address in quotes; no new logging of bodies |
| **Messaging (optional stretch)** | On quote create, idempotent `CreateConversation` with `ContextType.Quote`, `contextId = quoteRequestId` |

---

## 10. Tests

- [ ] `ValidatePricingTypeAsync`: `scheduled_appointment` + `service_quotes` → allow quote-priced offering
- [ ] `ValidatePricingTypeAsync`: no capability → reject quote-priced offering
- [ ] Create quote with valid/invalid `serviceOfferingId`
- [ ] Discover gate: profile with fixed + quote services shows both CTAs (component or shared helper tests)
- [ ] Customer accept → booking pending + realtime publish (regression)
- [ ] Cross-tenant offering id → 404/validation

Run: `dotnet test Adeni.slnx -c Release`

---

## 11. Acceptance criteria

- [ ] Wellness tenant (`scheduled_appointment`, e.g. makeup-brows) with **`service_quotes`** can save a **price-on-request** service in portal.
- [ ] Discover profile shows **Book** for fixed services and **Request estimate** for quote-priced services on the same page.
- [ ] Customer submits estimate tied to a **service offering**; staff sees it in **Quotes**; **`QuotePipelineChanged`** updates dashboard without poll.
- [ ] Customer accepts offer on **`/my-quotes`** → pending booking appears in **My bookings**.
- [ ] Tenants **without** `service_quotes` behave as today (no quote-priced offerings, no estimate UI).
- [ ] **`quote_request`** whole-business tenants still work unchanged.

---

## 12. Open questions

| Question | Decision |
|----------|----------|
| Capability name `service_quotes` vs reusing `quotes` for wellness? | **`service_quotes`** for appointment businesses; keep **`quotes`** on `quote_request` business type bundle for backward compatibility |
| Which wellness categories get template flag day one? | **Product:** start with **none** (portal/entitlement toggle only) or **makeup-brows + skincare-aesthetics** — decide before implementation |
| Settings toggle vs capability-only? | **38a:** capability from category/entitlement; **38b (optional):** portal setting mirrors capability |
| Customer notify when quoted? | **Out of 38** — My quotes + email backlog |
| Rename API `pricingType: quote_request` to `custom_price`? | **No** in 38 (avoid breaking clients); document as “price on request” in UI |

---

## 13. Sprint backlog entry (for `docs/sprints.md`)

Add when approved:

**Sprint 38 — Wellness service quotes** — `service_quotes` capability, per-offering estimates on `scheduled_appointment` tenants, hybrid Discover UX, customer **My quotes**. Spec: this file.
