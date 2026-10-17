# Adeni Confluence documentation (repo mirror)

Markdown in this folder is the **source of truth for publishing** to the Adeni Confluence space (`SD`). Copy each page into Confluence (or use your team’s import pipeline). Keep repo and Confluence in sync when architecture or modules change.

## Recommended Confluence page tree

```
Adeni Product Bible (existing)
├── Technical Documentation (NEW — use technical-documentation-index.md)
│   ├── Backend Overview (backend-overview.md)
│   │   ├── Identity & Auth (backend/identity-and-auth.md)
│   │   ├── Tenancy (backend/tenancy.md)
│   │   ├── Catalog & Markets (backend/catalog-and-markets.md)
│   │   ├── Booking & Operations (backend/booking.md)
│   │   ├── Discovery (backend/discovery.md)
│   │   ├── Reviews (backend/reviews.md)
│   │   ├── Payments & Billing Documents (backend/payments.md)
│   │   ├── Subscriptions & Entitlements (backend/subscriptions.md)
│   │   ├── Messaging (backend/messaging.md)
│   │   ├── Notifications (backend/notifications.md)
│   │   ├── Trust & Verification (backend/trust.md)
│   │   ├── Tenant Dashboard (backend/dashboard.md)
│   │   ├── Admin Operations (backend/admin.md)
│   │   ├── Media Storage (backend/storage.md)
│   │   ├── Translation (backend/translation.md)
│   │   └── Cross-cutting: Events, Auditing, Persistence (backend/platform-cross-cutting.md)
│   ├── Frontend Overview (frontend-overview.md)
│   │   ├── Discover App (frontend/discover-app.md)
│   │   ├── Business Portal (frontend/portal-app.md)
│   │   ├── Admin Portal (frontend/admin-app.md)
│   │   ├── Flutter Mobile (frontend/mobile-app.md)
│   │   └── Shared Packages (frontend/shared-packages.md)
│   ├── System Architecture v1.3 (system-architecture-v1.3.md)
│   └── Product Bible mirror (product-bible.md — update index only)
```

## Publishing checklist

1. Create parent page **Technical Documentation** under the Product Bible.
2. Paste each `.md` file as a child page; use Confluence code blocks for paths and commands.
3. Link **System Architecture v1.3** from the existing System Architecture v1.2 page (mark v1.2 as superseded for stack facts).
4. Update the Product Bible documentation index table (`product-bible.md` § Documentation index).
5. On each sprint that adds a module or app route, update the relevant module page and `technical-documentation-index.md` changelog.

## Local references

| Repo doc | Role |
|----------|------|
| [architecture.md](../architecture.md) | Module rules, extraction criteria |
| [frontend.md](../frontend.md) | Dev commands |
| [target-client-architecture.md](../target-client-architecture.md) | Client topology |
| [AGENTS.md](../../AGENTS.md) | Agent conventions |
| OpenAPI (dev) | `http://localhost:5169/scalar/v1` |

_Last updated: October 2026_
