# Spec: Discover SEO landings + sitemap

| Field | Value |
|-------|-------|
| **Sprint** | Discover growth (cross-cutting; not Sprint 32/33) |
| **Author** | Agent |
| **Status** | Approved |
| **Created** | 2026-10-19 |

---

## 1. Goal (one sentence)

Make Adeni Discover crawlable for city/category demand and list every verified business in a sitemap so SEO and share links bring customers to bookable profiles.

---

## 2. Context

- Product direction: public URLs toward `/ottawa`, `/lagos`, category/geo pages, structured data, sitemap ([product-direction.md](../product-direction.md) § SEO)
- Checklist gap: SSR crawl spot-check / sitemap ([angular-migration-checklist.md](../angular-migration-checklist.md) §6)
- Module: Discovery (read-only public); Discover Angular SSR

---

## 3. In scope

- [x] Static `robots.txt` allowing crawl + sitemap pointer
- [x] Dynamic `sitemap.xml` (home, city hubs, category landings, verified business slugs via discovery search pagination)
- [x] SSR city hubs: `/lagos`, `/ottawa` (all V1 catalog markets, not only `isLive`)
- [x] SSR category landings: `/{marketId}/{categorySlug}` for enabled wellness categories
- [x] Unique title/description/canonical per hub/landing; set market context for listings
- [x] Richer `LocalBusiness` JSON-LD (subtype, areaServed, multi-image; omit masked telephone)

## 4. Out of scope

- Neighborhood hubs (Lekki/Kanata programmatic pages) — follow-up
- Google Search Console account setup (ops)
- Opening hours / Offer schema until public API exposes hours
- Changing Explore UX (`/discover?…`) beyond links from hubs
- New API endpoints (reuse discovery search + categories)

---

## 5. API contract

No new API routes. Sitemap server uses existing:

| Method | Route | Auth |
|--------|-------|------|
| GET | `/api/v1/discovery?market=&lat=&lng=&page=&pageSize=&sort=featured` | Anonymous |
| GET | `/api/v1/categories?market=&wellness=true` | Anonymous (hubs may use client-side / shared catalog) |

**URL contract (Discover host):**

| Path | Purpose |
|------|---------|
| `/robots.txt` | Allow `/`, disallow private areas; `Sitemap:` line |
| `/sitemap.xml` | urlset of public SEO URLs |
| `/{marketId}` | City hub |
| `/{marketId}/{categorySlug}` | Category landing |

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Docs | `docs/specs/discover-seo-landings.md`, note in `docs/sprints.md` or checklist |
| Shared | optional helpers for SEO market/category path validation; JSON-LD builder inputs |
| Discover | `public/robots.txt`, `src/server.ts` sitemap, routes, new market-hub / category-landing pages, `seo-jsonld.ts` |
| Tests | unit tests for path helpers / JSON-LD; optional sitemap smoke |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | N/A (public verified only) |
| **Auth** | Anonymous |
| **Cache** | Sitemap may cache in-memory briefly (e.g. 15 min) to protect API |
| **PII** | Do not put masked phone in JSON-LD `telephone` |
| **SEO** | Canonicals must not conflict with `/discover?market=` |

---

## 8. Acceptance criteria

- [x] `GET /robots.txt` and `GET /sitemap.xml` work in SSR/prod server
- [x] `/lagos` and `/ottawa` SSR with market-specific meta + featured businesses
- [x] `/lagos/hair-grooming` (etc.) SSR with filtered list + meta
- [x] Unknown market/category → navigate away / 404-style redirect to home or discover
- [x] Profile JSON-LD uses a more specific `@type` when mappable; no `[REDACTED]` telephone
- [x] Existing routes (`/discover`, `/businesses/…`, `/privacy`, …) unchanged

---

## 9. Rollout / config

- `publicAppUrl` used for absolute sitemap URLs and canonicals
- Sitemap fetches `apiBaseUrl` from env / build-time config on the Node server
