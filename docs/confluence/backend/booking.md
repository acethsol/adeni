# Backend module — Booking & Operations

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

Core **scheduling and floor operations** for beauty & wellness tenants: services, staff, availability, appointments, waitlist, walk-in **queue**, **kiosk** check-in, **closed dates**, and **quote requests**.

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Booking/` — bookings, staff, queue, quotes, events |
| Application | `Adeni.Application/Booking/` — full port list |
| Infrastructure | `Adeni.Infrastructure/Booking/` |
| API | `BookingsController`, `TenantBookingsController`, `TenantServicesController`, `TenantStaffController`, `TenantAvailabilityController`, `TenantQueueController`, `PublicQueueController`, `TenantKioskController`, `TenantClosedDatesController`, `QuotesController`, `TenantServiceMenuGroupsController` |

Registration: `AddBookingModule()`.

---

## Application ports

| Interface | Responsibility |
|-----------|----------------|
| `IServiceCatalogService` | Service offerings CRUD |
| `IServiceMenuGroupService` | Menu grouping (Sprint 22) |
| `IStaffService` | Staff members, services, leave, calendar |
| `IAvailabilityService` | Weekly hours, slot search |
| `IBookingService` | Create/accept/reject/cancel lifecycle |
| `IWaitlistService` | Waitlist when slots full |
| `IQueueService` | Walk-in queue tickets, floor board |
| `IKioskService` | PIN-based kiosk mode |
| `IBusinessClosedDateService` | Business closed dates |
| `IQuoteRequestService` | Quote workflow (Sprint 19) |
| `ISchedulingTimeZone` | Tenant-local scheduling |

---

## Domain events (examples)

- `BookingConfirmed`, `BookingCancelled`, `BookingRejected`, `SlotOpened`

Consumed by Notifications and other handlers via `IDomainEventDispatcher`.

---

## HTTP routes (selected)

| Area | Route prefix |
|------|----------------|
| Customer bookings | `/api/v1/bookings` |
| Tenant bookings | `/api/v1/tenant/bookings` |
| Services | `/api/v1/tenant/services` |
| Staff | `/api/v1/tenant/staff` |
| Availability | `/api/v1/tenant/availability` |
| Queue (staff) | `/api/v1/tenant/queue` |
| Queue (public join) | `/api/v1/public/queue` |
| Kiosk | `/api/v1/tenant/kiosk` |
| Closed dates | `/api/v1/tenant/closed-dates` |
| Quotes | `/api/v1/quotes`, `/api/v1/tenant/quotes` |
| Menu groups | `/api/v1/tenant/service-menu-groups` |

**Slot locking:** Redis distributed locks for concurrent booking — see caching docs.

---

## Dependencies

- **Tenancy** — tenant must be eligible (verified, plan)
- **Payments** — deposits, mark-paid, payment loop
- **Identity** — staff permissions, practitioner scope
- **Reviews** — completed booking prerequisite

---

## Clients

| App | Features |
|-----|----------|
| Discover | Booking panel, queue join, quote panel |
| Portal | Bookings, queue floor, kiosk, staff calendar, services |
| Mobile (future) | Front desk, owner dashboards |

---

## Tests

- Extensive suite under `tests/Adeni.Infrastructure.Tests/Booking/`
- API integration: `BookingIntegrationTests`, queue, kiosk tests

---

## Related docs

- `docs/specs/sprint-29-*`, `sprint-30-kiosk-mode.md`, `sprint-31-business-closed-dates.md`
- [Payments](./payments.md), [Reviews](./reviews.md)
