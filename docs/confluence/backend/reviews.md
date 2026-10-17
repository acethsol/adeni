# Backend module — Reviews

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Post-booking reviews and ratings** tied to completed appointments. Public aggregation for discovery cards; tenant tools for replies; admin moderation.

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Booking/Review.cs` |
| Application | `Adeni.Application/Reviews/IReviewService` |
| Infrastructure | `Adeni.Infrastructure/Reviews/ReviewService` |
| API | Public reviews on `DiscoveryController`; `TenantReviewsController`; `AdminReviewsController` |

Registration: `AddReviewsModule()`.

---

## Rules

- Review creation requires a **completed booking** for that customer/tenant pair.
- Public lists expose moderated content; tenant can **reply** to reviews.
- Admin can list/remove per policy (SOC2 audit).

---

## HTTP routes

| Method | Route |
|--------|-------|
| POST | `/api/v1/bookings/{id}/reviews` (customer) |
| GET | `/api/v1/tenant/reviews` |
| POST | `/api/v1/tenant/reviews/{id}/reply` |
| GET | `/api/v1/admin/reviews` |

---

## Clients

Discover (submit after booking), Portal (inbox), Admin (moderation).

---

## Tests

- `ReviewIntegrationTests`, review service unit tests

---

## Related

- [Booking](./booking.md), [Discovery](./discovery.md)
