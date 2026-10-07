# Spec: Explore gallery carousel

| Field | Value |
|-------|-------|
| **Sprint** | Explore UX (cross-cutting) |
| **Author** | Agent |
| **Status** | Implemented |
| **Created** | 2026-10-06 |

---

## 1. Goal (one sentence)

Let customers browse multiple business photos on Explore cards and map pin previews (home stays single-image).

## 2. Context

- Today each business has one `cover_image_key`; cards show a single image.
- Similar apps (Airbnb-style) use prev/next on listing cards.
- Module(s): Tenancy (media), Discovery, Discover web, Portal.

## 3. In scope

- [x] `GalleryImageKeysJson` on business profile (up to 5 extra photos)
- [x] Tenant upload purpose `gallery` + add/remove gallery endpoints
- [x] Discovery `imageUrls` (cover first, then gallery)
- [x] Explore card + map popup carousel when `imageUrls.length > 1`
- [x] Portal UI to manage gallery photos
- [x] Home page unchanged (no carousel)

## 4. Out of scope

- Profile `/businesses/:slug` hero multi-image (later)
- Flutter discover carousel
- Reordering gallery (append order only for v1)
- CDN transforms / image optimization pipeline

## 5. API contract

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/v1/tenant/media/upload-url` | JWT + tenant | `purpose: "gallery"` |
| POST | `/api/v1/tenant/profile/gallery` | JWT + tenant | Attach uploaded key |
| DELETE | `/api/v1/tenant/profile/gallery` | JWT + tenant | Remove key |
| GET | `/api/v1/discovery` | public | Items include `imageUrls: string[]` |

## 6. Security

- Gallery keys must be under `tenants/{tenantId}/gallery/`
- Tenant isolation via existing media access checks
- No PII in image metadata logs
