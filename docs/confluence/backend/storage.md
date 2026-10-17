# Backend module — Media Storage

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Cover images, gallery, staff avatars, customer media** — presigned upload URLs, Azure Blob (or local file storage in dev), CDN public URLs in staging/prod.

---

## Layer map

| Layer | Path |
|-------|------|
| Application | `Adeni.Application/Storage/` — `IFileStorage`, `ITenantMediaService`, `ICustomerMediaService`, gallery key helpers |
| Infrastructure | `Adeni.Infrastructure/Storage/` — Azure Blob, local disk |
| API | `MediaUploadController`, `TenantMediaController`, `CustomerMediaController` |

Registration: `AddAdeniStorage()` in `Program.cs`.

---

## Flow

1. Client requests upload URL (`POST .../media/upload-url`).
2. Client PUTs bytes to blob storage directly.
3. Client confirms key on tenant profile (`PATCH` cover/gallery).

---

## HTTP routes

| Area | Route |
|------|-------|
| Tenant media | `/api/v1/tenant/media/*`, cover/gallery |
| Customer | `/api/v1/customer/media` |

---

## Security

- Keys are tenant-scoped; validation on attach.
- Public URLs via configured `PublicBaseUrl` / CDN — see `docs/media-storage.md`.

---

## Clients

Portal (profile, staff avatars), Discover (displays CDN URLs from API).

---

## Related docs

- `docs/media-storage.md`
