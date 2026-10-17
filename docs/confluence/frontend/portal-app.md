# Frontend — Business Portal (`@adeni/portal`)

> **Parent:** [Frontend Overview](../frontend-overview.md)

---

## Purpose

**Tenant operations hub** for beauty & wellness businesses: dashboard, bookings, queue floor, staff, services, payments, plan/subscription, customers, messaging, quotes, kiosk, and settings.

---

## Stack

| Item | Value |
|------|-------|
| Framework | Angular 22 SPA |
| Dev URL | http://localhost:5173 |
| Command | `npm run dev:portal` |
| UI kit | `@adeni/ui` + Carbon patterns |

---

## Directory layout

```
apps/portal/src/app/
  core/           guards, portal-nav, theme, business-api, session
  layout/         portal-shell, nav icons
  pages/          feature pages (dashboard, bookings, queue, staff, …)
  shared/         pending bookings bell, tabs, tooltips
  environments/
```

---

## Guards

| Guard | Role |
|-------|------|
| `portalAccessGuard` | Tenant registered / setup complete |
| `businessAuthGuard` | Auth0 business login |
| `portalPermissionGuard` | Fine-grained portal permissions from API |
| `staffCalendarGuard` | Staff can only open own calendar unless manager |
| `homeRedirectGuard` | Role-based default landing |

---

## Routes (feature map)

| Path | Feature | API areas |
|------|---------|-----------|
| `/dashboard` | KPIs, charts, layout | Tenant dashboard |
| `/bookings` | Appointment inbox | Tenant bookings |
| `/queue` | Walk-in floor board | Queue |
| `/kiosk` | Check-in PIN mode | Kiosk |
| `/calendar`, `/my-calendar`, `/staff/:id/calendar` | Staff scheduling | Staff + availability |
| `/customers`, `/customers/:id` | Tenant CRM | Tenant customers |
| `/services` | Service menu | Services, menu groups |
| `/staff` | Team, invites, access | Staff, access |
| `/availability` | Weekly hours | Availability |
| `/locations` | Locations | Tenancy |
| `/profile`, `/public-page` | Brand & public site | Tenancy |
| `/payments` | Ledger, documents | Payments |
| `/plan` | Subscription | Subscriptions |
| `/messages` | Inbox | Messaging |
| `/quotes` | Quote inbox | Quotes |
| `/settings` | Policies, walk-in, notifications | Tenancy + booking settings |
| `/register` | Onboarding | Tenancy |
| `/accept-invite` | Staff invite | Identity |

---

## API integration

- **`BusinessApiService`** — central HTTP layer; sets **`X-Tenant-Id`** from session.
- **`PortalSessionService`** — auth/me + tenant context.
- Entitlement gating: hide nav items when plan lacks capability (check API + `@adeni/shared` tiers).

---

## Notable UX patterns

- **Pending bookings bell** — polls tenant pending count.
- **Dashboard layout sync** — persists widget layout to API (`dashboard-layout-sync.ts`).
- **Kiosk** — full-screen route outside main shell chrome.

---

## Build

`npm run build:portal` — static SPA; configure `base href` for production host/subpath.

---

## Related backend modules

All tenant modules — start with [Tenancy](../backend/tenancy.md), [Booking](../backend/booking.md), [Dashboard](../backend/dashboard.md), [Subscriptions](../backend/subscriptions.md).

---

## Related docs

- `apps/portal/README.md`, `packages/ui/README.md`
