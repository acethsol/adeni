# Spec: Sprint 33b-4 — Pricing catalog publish workflow

| Field | Value |
|-------|-------|
| **Sprint** | 33b Phase 4 — Catalog publish |
| **Status** | Implemented |
| **Created** | 2026-10-20 |
| **Depends on** | [sprint-33b-admin-commercial-config.md](./sprint-33b-admin-commercial-config.md) (33b-1 read-only catalog) |

---

## 1. Goal (one sentence)

Let operators **draft**, **edit entries**, and **activate** a new global pricing catalog version from Admin, with exactly one active version and cache refresh for `GET /api/v1/pricing`.

---

## 2. In scope

- [x] `POST /api/v1/admin/pricing-catalog/versions` — create **draft** (optional clone from active entries)
- [x] `PATCH /api/v1/admin/pricing-catalog/versions/{code}/entries` — upsert rows on **draft** only
- [x] `POST /api/v1/admin/pricing-catalog/versions/{code}/activate` — set draft → **active**, retire prior active
- [x] Admin **Pricing → Catalog**: versions grid + entries for selected version; create + activate with confirm
- [x] Admin MFA on all routes; audit `pricing_catalog.*` actions
- [x] Unit + integration tests (activate swaps active flag)

## 3. Out of scope

- Inline GMV-only editor (33b-3); entries still carry `platformFeePercent` / `isSelfServeEnabled`
- Tenant commercial overrides / price lock (33b-2)
- Deprecating `country-pricing.json` client fallback

---

## 4. API contract

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/admin/pricing-catalog/versions` | Admin MFA | List versions (existing) |
| GET | `/api/v1/admin/pricing-catalog/versions/{code}` | Admin MFA | Version + entries + diff (existing) |
| POST | `/api/v1/admin/pricing-catalog/versions` | Admin MFA | Create draft version |
| PATCH | `/api/v1/admin/pricing-catalog/versions/{code}/entries` | Admin MFA | Upsert entry rows (draft only) |
| POST | `/api/v1/admin/pricing-catalog/versions/{code}/activate` | Admin MFA | Publish draft → active |

**POST create body**

```json
{
  "code": "2026-q2-01",
  "effectiveFrom": "2026-11-01T00:00:00Z",
  "effectiveTo": null,
  "cloneFromActive": true
}
```

**PATCH entries body**

```json
{
  "entries": [
    {
      "countryCode": "NG",
      "currencyCode": "NGN",
      "subscriptionSku": "professional",
      "monthlyAmountMajor": 60000,
      "platformFeePercent": 0,
      "isSelfServeEnabled": true
    }
  ]
}
```

**Errors:** `validation`, `conflict` (duplicate code), `not_found`, `conflict` (mutate non-draft / activate non-draft).

---

## 5. Files touched

| Layer | Files |
|-------|-------|
| Application | `IPricingCatalogService.cs` — DTOs + mutate methods |
| Infrastructure | `PricingCatalogService.cs` — EF + audit + cache refresh |
| Domain | `AuditActions.cs` |
| Api | `AdminPricingCatalogController.cs` |
| Shared | `packages/shared/src/schemas.ts` |
| api-client | `packages/api-client/src/index.ts` |
| Admin | `pricing-catalog.component.*` |
| Tests | `PricingCatalogServiceTests.cs`, `PricingIntegrationTests.cs` |

---

## 6. Cross-cutting

| Area | Impact |
|------|--------|
| **Auth** | Admin MFA; `sub` for audit actor |
| **Audit** | version created, entries updated, version activated |
| **Cache** | `RefreshCacheAsync` after activate |
| **PII** | None in catalog payloads |
