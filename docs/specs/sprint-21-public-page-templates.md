# Spec: Public page templates (Phase A)

> Copy of [\_template.md](./_template.md). Do not write code until this spec is reviewed/approved.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 21 — Public page templates (Phase A) |
| **Author** | Agent + product |
| **Status** | Implemented |
| **Created** | 2026-10-05 |

---

## 1. Goal (one sentence)

Let verified businesses pick a public-page **look** and light **branding** in the portal so `/businesses/{slug}` feels like their mini-site—not a generic Adeni listing—without pasting HTML.

---

## 2. Context

- Backlog after Sprint 20 (deploy/AI/observability). Tracked in [sprints.md](../sprints.md) → Sprint 21.
- Strategy: shareable public profile is a core wedge ([product-strategy.md](../product-strategy.md) §2 / public profile); portal is as important as discovery.
- Inspired by Zenoti Webstore V2 (brand colors + storefront config) but **improves** by using curated visual templates + structured toggles instead of raw HTML sidebars.
- Modules: **Tenancy** (config ownership), **Discovery** (public DTO + cache), **Discover web** + **Portal**.
- Builds on the Angular discover mini-site already on `/businesses/:slug`.

**Phases (this spec = A only):**

| Phase | Scope |
|-------|--------|
| **A (this)** | Template picker + brand tokens + section visibility + portal preview + discover render |
| B (later) | Content blocks (hours, gallery, team, promo) + live WYSIWYG-ish editor |
| C (later) | Embeddable book widget, custom domain, sanitized HTML escape hatch (Pro) |

---

## 3. In scope

- [ ] Four curated **page templates** (layout + typography + section order variants)
- [ ] Portal **Branding / Public page** settings: template, accent color, logo (reuse cover media path or logo key), section show/hide
- [ ] Persist config on tenant `BusinessProfile` (or sibling JSON column)
- [ ] Expose config on public profile API; invalidate `tenant:{id}:profile` on write
- [ ] Discover renders template variants from config (SSR-safe)
- [ ] Portal live preview (iframe or in-page mock using same tokens)
- [ ] Shared Zod/types in `@adeni/shared`; api-client methods
- [ ] Sensible defaults for existing businesses (no migration pain)

## 4. Out of scope

- Arbitrary HTML / custom CSS injection (Zenoti-style paste fields)
- Block builder, gallery upload beyond existing cover, team bios, hours editor
- Embed widget, custom domain, favicon upload
- Per-location template overrides (multi-branch later)
- Tier gating of templates (all templates free in Phase A; note for Phase B/C)
- Mobile Flutter business app parity (portal web first)
- Changing Adeni marketplace chrome (global header/footer stay Adeni)

---

## 5. API contract

### Templates (product constants — `@adeni/shared`)

| Id | Intent |
|----|--------|
| `studio` | Default — full-bleed hero, sticky booking rail (current mini-site) |
| `spa` | Softer typography, calmer palette bias, content-first |
| `barber` | High-contrast, compact menu, bold CTAs |
| `luxe` | Editorial serif hero, generous whitespace, minimal chrome |

### Branding payload

```json
{
  "templateId": "studio",
  "accentColor": "#0F766E",
  "logoImageUrl": null,
  "sections": {
    "about": true,
    "services": true,
    "reviews": true,
    "visit": true,
    "book": true
  }
}
```

Validation:

- `templateId` ∈ known set
- `accentColor` optional HEX `#RRGGBB` (null → category gradient / Adeni teal fallback)
- `sections.book` cannot be false for `scheduled_appointment` (always bookable entry); quote businesses keep quote panel under `book`
- At least one of `about` | `services` | `visit` must be true

### Routes

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/profile` | Business JWT + `X-Tenant-Id` | Extend response with `publicPage` |
| `PATCH` | `/api/v1/tenant/public-page` | Business JWT + `X-Tenant-Id` | Upsert branding/template/sections |
| `GET` | `/api/v1/businesses/{slug}` | Anonymous | Extend `PublicBusinessProfile` with `publicPage` |

**Error codes** (stable dotted codes + i18n):

- `tenancy.public_page.invalid_template`
- `tenancy.public_page.invalid_accent`
- `tenancy.public_page.sections_required`

**Cache:** on `PATCH`, invalidate `tenant:{id}:profile` (and any slug-keyed profile cache if present).

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `BusinessProfile` (+ `PublicPageJson` or typed columns); optional value object |
| Application | DTOs on `IBusinessOnboardingService` / small `IPublicPageService`; extend `PublicBusinessProfile` |
| Infrastructure | EF config + migration; profile load/update; discovery mapping; cache invalidation |
| Api | `TenantPublicPageController` or action on tenant controller; OpenAPI |
| Tests | Unit validation; integration GET public + PATCH tenant; cross-tenant 403 |
| `packages/shared` | `public-page-templates.ts`, Zod schemas, i18n labels |
| `packages/api-client` | `getTenantProfile` shape + `updatePublicPage` |
| `apps/portal` | Branding / Public page settings UI + preview |
| `apps/discover` | Template host on business page (CSS variants / section flags) |

Follow [architecture.md](../architecture.md) — Tenancy owns write; Discovery reads via existing public profile path (no cross-Infrastructure imports).

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | PATCH tenant-scoped; query filters + `X-Tenant-Id` claim match |
| **Auth / roles** | Business owner/staff with profile edit; public GET anonymous |
| **Cache** | Invalidate `tenant:{id}:profile` on PATCH |
| **Audit** | Not admin; optional tenant activity log later — skip Phase A |
| **PII** | No new PII; logo/cover URLs only; never log phone |
| **Observability** | Existing correlation ID; no new App Insights events required |
| **Security** | No raw HTML; accent HEX only; logo must use existing media key prefix rules if uploaded |
| **SSR** | Discover TransferState must include `publicPage` |

---

## 8. Tests

- [ ] Unit: template/accent/sections validation (`invalid_template`, `sections_required`)
- [ ] Integration: `PATCH /api/v1/tenant/public-page` then `GET /api/v1/businesses/{slug}` reflects config
- [ ] Tenant cross-access denial on PATCH
- [ ] Default `publicPage` for legacy rows (null → `studio` + all sections on)
- [ ] Discover/portal typecheck against shared Zod

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [ ] Portal shows template picker with 4 named previews; save persists
- [ ] Accent color + optional logo appear on public page
- [ ] Section toggles hide/show About / Services / Reviews / Visit without breaking booking
- [ ] Public page SSR renders correct template (no flash of wrong layout)
- [ ] Existing businesses unchanged until they save (`studio` default)
- [ ] No HTML/CSS injection surfaces in portal
- [ ] Spec status → Implemented; sprints.md Sprint 21 tasks checked when done

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| Logo: reuse cover upload pipeline vs separate `logoImageKey`? | Prefer **separate `logoImageKey`** (square/mark); cover stays hero. If media work slips, Phase A ships accent + template only and logo follows immediately after. |
| Entitlement gating in A? | **No** — all 4 templates free; gate gallery/custom HTML in B/C. |
| Where in portal nav? | New **Public page** item under business settings, or section on Profile — prefer dedicated page for preview space. |
| Quote-only businesses? | Same templates; `book` section hosts quote panel; CTA label from existing `discoveryCta`. |

---

## 11. UX notes (Phase A)

- Portal: one composition — template grid + brand controls + phone-width preview; not a settings dashboard wall.
- Discover: keep Adeni shell; **inside** the page, business brand dominates (name, accent, template).
- Templates differ by hero treatment, type scale, density, and default section order — not only a CSS filter on the same DOM.
- Mobile: booking rail stacks above content (current pattern); template must not break that.

---

## 12. Success metric (soft)

Pilot businesses share `/businesses/{slug}` more often (share-kit copies) and complete branding setup within first week of verification — qualitative for Phase A; instrument later in Sprint 20 observability if events exist.
