# Spec: Sprint 35 — Tenant customer records (business roster)

> Replaces the interim `EnsureGuestCustomerAsync` + global phone match used by queue v1.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 35 — Tenant customer records |
| **Author** | Agent + product |
| **Status** | Implemented |
| **Created** | 2026-10-16 |

---

## 1. Goal (one sentence)

Each business gets a **tenant-scoped customer roster** (`TenantCustomer`) that includes **logged-in bookers (Auth0)** and **walk-ins**, with one canonical profile per person per tenant, while queue/booking rows keep **event snapshots** where needed for the floor board and audit.

**Naming:** Platform **`Customer`** = Auth0 consumer (`identity.customers`). **`TenantCustomer`** = that business’s view of a person. Portal UI label: **Customers**.

---

## 2. Context

- **Discover booking requires login** — `BookingService.CreateAsync` resolves platform **`Customer`** by Auth0 sub. Bookings store **`CustomerId`** only.
- Walk-in queue uses **phone + name** (optional email) without login.
- Queue tickets keep **`GuestName/GuestPhone/GuestEmail`** as join-time snapshots (not renamed).

### Two consumer paths

```
Logged-in book (Discover)          Walk-in queue (Discover / kiosk / staff)
        │                                      │
        ▼                                      ▼
  Customer (Auth0 sub)                   Phone + name (+ email)
        │                                      │
        └──────────────┬───────────────────────┘
                       ▼
              TenantCustomer (per TenantId)
                       │
         ┌─────────────┴─────────────┐
         ▼                           ▼
   Booking.CustomerId          QueueTicket snapshot + TenantCustomerId
```

---

## 5. API contract

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/tenant/customers` | Business JWT + tenant | Paginated roster |
| GET | `/api/v1/tenant/customers/{id}` | Business JWT + tenant | Profile + recent ids |

Queue join upserts `TenantCustomer` then sets `tenantCustomerId` on staff queue ticket responses.

---

## 6. Implementation map

| Layer | Files |
|-------|-------|
| Domain | `TenantCustomer.cs`, `QueueTicket.TenantCustomerId` |
| Application | `ITenantCustomerService` |
| Infrastructure | `TenantCustomerService`, `QueueService`, `BookingService` |
| Db | `tenancy.tenant_customers`, `db/scripts/tenant-customers-sprint35.sql` |
| Api | `TenantCustomersController` |
| packages | `tenantCustomer*Schema`, `listTenantCustomers` / `getTenantCustomer` |

---

## 10. Merge policy (within tenant)

1. Platform **`CustomerId`** on existing `TenantCustomer` row  
2. **`NormalizedPhone`** within tenant  
3. Create new row  

---

## 12. Migration

Run **`db/scripts/tenant-customers-sprint35.sql`** (renames legacy `tenant_guests` / `TenantGuestId` if present).
