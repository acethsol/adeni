# Spec: Discover SEO follow-ups

| Field | Value |
|-------|-------|
| **Sprint** | Discover growth (cross-cutting) |
| **Author** | Agent |
| **Status** | Approved |
| **Created** | 2026-10-19 |

---

## 1. Goal (one sentence)

Ship neighborhood landings, richer profile structured data (hours + offers), Search Console readiness, and a working production SSR shell so crawlers see the same HTML as local SSR.

---

## 2. Context

- Follow-up to [discover-seo-landings.md](./discover-seo-landings.md)
- Product direction: category/geo pages, structured data, sitemap

---

## 3. In scope

- [x] Neighborhood hubs: `/{market}/{area}` (+ optional `/{market}/{area}/{category}`)
- [x] Sitemap entries for curated neighborhoods
- [x] Public profile `openingHours` from tenant weekly availability
- [x] LocalBusiness JSON-LD: `openingHoursSpecification` + `hasOfferCatalog` / Offers from public services
- [x] Google Search Console verification meta + submit runbook
- [x] Production SSR host allowlist + Auth0/SSR bootstrap hardening (NG0203)

## 4. Out of scope

- Standalone per-service HTML pages (Offers link to profile `#services` / service anchors)
- Creating the Google Search Console property (ops; runbook only)
- Opening-hours UI on Discover profile page (API + JSON-LD only)
- Neighborhood × every category explosion in sitemap (hubs + optional filtered landings only)

---

## 5. API contract

| Method | Route | Auth | Change |
|--------|-------|------|--------|
| GET | `/api/v1/businesses/{slug}` | Anonymous | Add `openingHours: { dayOfWeek, openTime, closeTime }[]` |

Discover URLs:

| Path | Purpose |
|------|---------|
| `/{market}/{area}` | Neighborhood hub (`q` = area phrase) |
| `/{market}/{area}/{category}` | Neighborhood + category |
| Meta `google-site-verification` | When env `googleSiteVerification` set |

---

## 6. Files to touch

Shared seo neighborhoods + paths; Discovery `PublicBusinessProfile` + load/cache invalidate; discover seo-market routes/page, seo-jsonld, server sitemap, SeoService verification, environments, angular.json hosts, Auth0 SSR cache; docs runbook + sprints note; tests.

---

## 7. Cross-cutting

| Area | Impact |
|------|--------|
| **Cache** | Invalidate `location:{slug}:profile` when weekly hours change |
| **PII** | Hours are non-PII; phone rules unchanged |
| **Tenant** | Hours from tenant weekly rules only |

---

## 8. Acceptance criteria

- [x] `/lagos/lekki` and `/ottawa/kanata` SSR with meta + listings (`q` area)
- [x] Profile JSON-LD includes hours when set; Offers when services exist; no masked phone
- [x] `robots.txt` + runbook document GSC submit
- [x] `node dist/discover/server/server.mjs` renders `/lagos` without NG0203 / host 400
