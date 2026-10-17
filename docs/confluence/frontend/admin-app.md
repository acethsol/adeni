# Frontend — Admin Portal (`@adeni/admin`)

> **Parent:** [Frontend Overview](../frontend-overview.md)

---

## Purpose

**Adeni internal operations:** verification queue, trust badges, markets, business directory, subscription insights, payment transactions, customer privacy tools, review moderation.

---

## Stack

| Item | Value |
|------|-------|
| Framework | Angular 22 SPA |
| Dev URL | http://localhost:5180 |
| Command | `npm run dev:admin` |
| UI kit | `@adeni/ui` (shared shell patterns with portal) |

---

## Directory layout

```
apps/admin/src/app/
  core/           admin-nav, theme, admin-api, guards
  layout/         admin shell
  pages/          dashboard, pending, businesses, markets, …
  shared/         pills, paging, load-error helpers
```

---

## Guards

| Guard | Role |
|-------|------|
| `adminAccessGuard` | Admin role present |
| `adminAuthGuard` | Auth0 session valid |

Production: enforce **MFA** via Auth0 Action + API policy.

---

## Routes

| Path | Page | Backend |
|------|------|---------|
| `/dashboard` | Ops summary | Admin metrics |
| `/pending` | Business verification queue | Admin businesses |
| `/trust-badges` | Badge approvals | Trust admin API |
| `/reviews` | Review moderation | Admin reviews |
| `/markets` | Market editor | Admin markets |
| `/insights` | Subscription KPIs | Admin subscription metrics |
| `/transactions` | Payment search | Admin transactions |
| `/businesses` | Directory + tier tools | Admin businesses |
| `/businesses/:tenantId/billing` | Commercial terms wizard | Subscriptions + admin |
| `/customers` | Export / erasure | Admin customers |

---

## API integration

- **`AdminApiService`** — typed calls via `@adeni/api-client` admin modules.
- Shared list paging: `admin-list-paging.ts`, error display: `admin-load-error.ts`.

---

## Security UX

- Separate Auth0 application from portal/discover.
- Recommended separate production origin (`admin.adeni.com`).
- No tenant header — global admin scope with audit on every mutation.

---

## Build

`npm run build:admin`

---

## Related backend module

[Admin Operations](../backend/admin.md)

---

## Related docs

- `apps/admin/README.md`
- `docs/mfa-enforcement.md`
