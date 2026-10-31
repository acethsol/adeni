# Spec: Business post-visit customer feedback (private)

> **Not** public “business reviews the customer.” Adeni keeps **customer → business** reviews on the public profile; this spec covers **tenant-private** CRM notes after a completed **booking**.  
> Use standing play 1 in [AGENTS.md](../../AGENTS.md). Do not write code until this spec is **Approved**.

| Field | Value |
|-------|-------|
| **Sprint** | Unscheduled (after Sprint 35 tenant customers / F1 profile) |
| **Author** | Product + engineering |
| **Status** | Draft |
| **Created** | 2026-10-31 |
| **Updated** | 2026-10-31 |

---

## 1. Goal (one sentence)

Let authorized staff record **private, tenant-scoped** post-visit notes tied to **completed bookings**—for service preferences and follow-up—**without** exposing content to the customer, other businesses, discovery, or any platform trust/reputation system.

---

## 2. Context

- **Today:** Only **customers** review **businesses** via `booking.reviews` (`POST /api/v1/bookings/{id}/review`). Businesses **reply** publicly (`OwnerReply`). Completing a booking enables the customer review prompt—not a reciprocal review.
- **Related:** [Sprint 13 / 19 reviews](../sprints.md), F1 [customer profile & activity](./f1-customer-profile-activity-foundation.md), [product-direction.md](../product-direction.md) journey (review = customer rates provider).
- **Modules:** Tenancy (tenant customer + notes), Booking (completion hook for UX timing).

### Hard boundary: Customer Trust & Verification

This feature is **completely separate** from [Customer Verification & Trust Foundation](./customer-verification-trust-foundation.md).

| Must never happen |
|-------------------|
| Visit notes feed a platform **trust score** or ranking |
| Notes sync to **cross-business** reputation or blocklist |
| Tags or note text trigger **automated booking rejection** |
| Notes appear in business **`trust`** API fields (`phoneVerified`, `customerSince`) |
| Admin or ML **sentiment/risk** models consume note bodies in V1 |

Operational booking/queue status (completed, no-show) remains in Booking/Queue modules—not note tags.

### Product decision (recommended)

| Option | Description | V1 recommendation |
|--------|-------------|-------------------|
| **A — Private visit notes (booking-only)** | Free-text + optional tags; tenant CRM only | **Yes — MVP** |
| **B — Internal structured flags** | Reuse booking/queue status for attendance/no-show | **Yes — existing data, not notes** |
| **C — Star rating of customer by business** | Numeric score | **Defer** |
| **D — Public business review of customer** | Mutual public reviews | **Out of scope** |

**Queue-ticket notes:** **Out of V1 MVP** — may be revisited in V1.1 after booking notes ship (see §10).

---

## 3. In scope (V1 MVP — booking only)

- [ ] **Domain + DACPAC:** `tenancy.customer_visit_notes` — tenant-scoped, `TenantCustomerId`, **`BookingId` required**, author membership/staff id, note body, optional tag enum, `CreatedAt` / `UpdatedAt`, soft-delete columns
- [ ] **One note per booking:** Upsert per `(TenantId, BookingId)`; booking must be **Completed** (or equivalent paid/complete statuses per existing booking rules)
- [ ] **API:** List on F1 customer profile; get/upsert/delete on booking
- [ ] **Portal UX:** Optional note after “Mark completed” or from customer profile / booking detail—skippable, non-blocking
- [ ] **F1 customer profile:** Notes section (permission-gated)
- [ ] **Visibility:** **Absent** from Customer Web, Discover, SSR/TransferState, and any `/api/v1/me/*` route
- [ ] **Audit:** Create/update/delete — metadata only (**no note body** in audit payload)
- [ ] **Privacy export / erasure:** Include notes in applicable tenant/platform customer exports; delete or anonymize on platform customer erasure (see §7)
- [ ] **Tests:** Permission, cross-tenant denial, customer JWT cannot read/write, audit without body

## 4. Out of scope

- Public or discover-visible customer ratings
- Cross-tenant or platform-wide reputation / blocklist / trust score
- Automated booking rejection, deposit penalties, or segment rules driven by note content
- ML sentiment on notes
- Customer-visible “business reviewed you”
- Replacing **customer → business** `Review` entity
- Mandatory note before complete
- Attachments / clinical charting (see F2 intake boundary)
- **Queue-ticket visit notes** in V1 MVP
- Any integration with customer verification gates or `ICustomerVerificationPolicy`

---

## 5. API contract (V1 — booking only)

Base: `/api/v1/tenant/...` with `X-Tenant-Id` + business JWT.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/customers/{tenantCustomerId}/visit-notes` | Business: `business.bookings` (same as F1 profile) | Paginated notes for CRM |
| `GET` | `/api/v1/tenant/bookings/{bookingId}/visit-note` | Bookings permission + tenant | Single note |
| `PUT` | `/api/v1/tenant/bookings/{bookingId}/visit-note` | Bookings permission | Upsert |
| `DELETE` | `/api/v1/tenant/bookings/{bookingId}/visit-note` | Same as edit | Soft-delete |

**Request (upsert):**

```json
{
  "body": "Prefers morning appointments; follow up on balayage maintenance in 6 weeks.",
  "tags": ["preference", "follow_up"]
}
```

**Response:** Same shape as before; `queueTicketId` **omitted in V1** (always null/absent).

**Tags (V1 closed enum in `@adeni/shared`):** `preference`, `follow_up`, `service_note`, `other` — operational, non-judgmental labels only. **No** `behavior`, `high_risk`, `bad_customer`, or similar.

**Errors:** `visit_note.booking_not_completed`, `visit_note.not_found`, `auth.forbidden`.

**Customer API:** None.

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `Adeni.Domain/Tenancy/CustomerVisitNote.cs` |
| DB | `db/Adeni.Database/tenancy/Tables/customer_visit_notes.sql`, FK to `tenant_customers`, `booking.bookings` |
| Application | `ICustomerVisitNoteService`, DTOs, validators |
| Infrastructure | EF, service, audit |
| Api | Tenant controllers |
| Tests | Integration + unit |
| Frontend | `apps/business` bookings + F1 customer detail; `@adeni/shared` Zod + i18n |
| Docs | This spec |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | `ITenantEntity`; booking must belong to tenant |
| **Auth / roles** | F1-aligned **Bookings** permission; practitioner `BookingsSelf` — see §10 |
| **Cache** | None V1 |
| **Audit** | `customer_visit_note.created` / `.updated` / `.deleted` — ids + tenant only |
| **PII / sensitivity** | Notes are tenant-confidential; mask in application logs; never in metrics labels |
| **Observability** | Counter `visit_note.upsert` without tenant/customer id labels |

### Privacy / erasure

| Event | Behavior |
|-------|----------|
| Platform customer erasure | Delete or redact notes linked via tenant customer → platform `CustomerId` |
| Tenant removes customer record | Cascade or soft-delete per tenant retention policy |
| DSAR / privacy export | Include note body and metadata when export scope includes tenant CRM for that data subject |
| Customer Trust module | **No** read path from trust sync or verification policy into notes |

---

## 8. Tests

- [ ] Customer JWT / anonymous → 403 on all visit-note routes
- [ ] Cross-tenant booking → 404/403
- [ ] Non-completed booking upsert → `visit_note.booking_not_completed`
- [ ] Audit row without note body
- [ ] Erasure removes/anonymizes notes
- [ ] **Regression:** note creation does not call or update customer trust tables

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [ ] Booking-only MVP; no queue note routes in V1
- [ ] Notes tenant-private; zero exposure on Customer Web / Discover
- [ ] No coupling to trust score, blacklist, or auto booking denial
- [ ] Tags use `service_note` (not `behavior`)
- [ ] UX copy and validation discourage highly sensitive content (§11)
- [ ] DACPAC via SqlPackage

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| Queue-ticket notes | **Deferred V1.1** — booking MVP first |
| `BookingsSelf` practitioners: notes on assigned bookings? | **TBD** — align with practitioner booking access |
| Max note length | **Proposed:** 2000 chars |
| Soft-delete | **Proposed:** `DeletedAt` |
| Note preview on booking list row | **Defer** |

---

## 11. UX, copy, and data minimization (portal)

**Purpose (show in empty states and help):**

- Notes are for **service preferences**, **visit context**, and **follow-up reminders** visible only to your team.
- They are **not** a replacement for intake forms (F2), medical records, or customer reviews.

**Do not use notes for (UI warning + optional server-side hint text, not automated rejection):**

- Government IDs or identity documents  
- Payment card numbers, bank details, or gift card secrets  
- Passwords or authentication credentials  
- Full clinical diagnoses or regulated health records (use appropriate intake/compliance paths if ever in scope)

**Interaction:**

- Title: **“Private note (only your team sees this)”** — never “Review customer.”
- Complete booking: primary **Complete**; secondary **Complete and add note**.
- F1 profile: **Notes** section with the purpose copy above.
- Do not block completion if note is empty.

**Validation (V1):** Max length + profanity optional; **no** keyword blocking tied to trust.

---

## 12. Relationship to customer reviews (existing)

```text
Customer ──rating/comment──► Business (public on profile)
Business ──private note──────► Tenant CRM only (this spec; no trust pipeline)
```

Customer review rules unchanged: completed booking, one review per booking, authenticated customer owns booking.

---

## 13. Deferred future work

- Queue-ticket visit notes (V1.1)
- Full-text search with entitlement gate
- Note templates
- Internal analytics on tag counts (aggregate only, no customer-facing score)
- Mutual public reviews — not planned
- Flutter business mobile parity
