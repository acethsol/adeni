# Audit trail runbook (`admin.audit_logs`)

> **SOC2-01** — actor, timestamp, entity, action, correlation id  
> **Hardening spec:** [specs/sprint-soc2-audit-hardening.md](./specs/sprint-soc2-audit-hardening.md)  
> **Readiness split (code vs deploy):** [soc2-readiness-checklist.md](./soc2-readiness-checklist.md)

V1 stores **admin and high-impact portal events** in SQL Server table **`admin.audit_logs`**. There is no separate tenant audit table yet.

---

## 1. When a row is written

| Layer | Rule |
|-------|------|
| **`AuditMiddleware`** | `/api/v1/admin` **mutations** (POST/PUT/PATCH/DELETE) and sensitive **GET** (`/admin/reporting/*`, `/admin/metrics/*`, `/admin/transactions`) — **only on 2xx** responses |
| **Service** | Authoritative row with correct `entityType`, metadata, actor (`sub` or `system:*`) |
| **Portal (tenant)** | Verification submit, booking status transitions, notification preference updates |

**Dedupe:** Middleware **does not** write when the service already audits the same route (approve/reject, commercial terms PATCH, markets, pricing catalog, customer delete, review hide, verification badge grant/revoke). See `AdminAuditRules.IsMiddlewareAuditSuppressed`.

**Failed requests:** 4xx/5xx admin mutations do **not** get middleware audit rows (avoids false “success” trail).

---

## 2. Action catalog (stable `action` values)

Defined in `Adeni.Domain.Auditing.AuditActions`:

| Action | Typical source | `entityType` |
|--------|----------------|--------------|
| `business.approved` / `business.rejected` | Admin business service | `tenant` |
| `business.verification_submitted` | Portal verification submit | `tenant` |
| `booking.accepted` / `rejected` / `checked_in` / `completed` / `marked_paid` | Portal booking transitions | `booking` |
| `tenant.notification_preferences_updated` | Portal notification settings | `tenant` |
| `subscription.webhook_received` / `subscription.webhook_applied` | Paystack SaaS webhook | `system` / tenant |
| `payment.webhook_received` (and related) | Paystack payment webhook | payment intent |
| `admin.reporting.read` | Admin reporting GET | route segment |
| `admin.metrics.read` | Admin metrics GET | route segment |
| `admin.transactions.read` | Admin transactions GET | route segment |
| `customer.exported` / `customer.deleted` | Admin privacy | `customer` |
| `security.cross_tenant_denied` | Tenant middleware | attempted tenant |
| `staff.*` | Staff invite / role / revoke | staff access |
| `pricing_catalog.*` | Admin catalog publish | catalog version |
| `market.*` | Admin markets CRUD | `market` |
| `review.hidden` | Admin moderation | `review` |
| `verification_badge.granted` / `revoked` | Admin trust | `tenant` |

Finance billing documents use string actions such as `document.issued`, `document.emailed` (see `BillingDocumentService`).

---

## 3. Row shape

| Column | Meaning |
|--------|---------|
| `Id` | UUID |
| `ActorId` | Auth0 `sub`, or `system:webhook`, or `portal:unknown` if JWT missing |
| `Action` | Dotted string from catalog above |
| `EntityType` | e.g. `tenant`, `booking`, `customer` |
| `EntityId` | GUID string or route-derived id |
| `CorrelationId` | From `X-Correlation-Id` / middleware |
| `CreatedAt` | UTC |
| `MetadataJson` | **No PII** — counts, flags, status enums only |

---

## 4. Operator queries (SQL Server)

Recent admin reporting reads:

```sql
SELECT TOP 50 CreatedAt, ActorId, Action, EntityType, EntityId, CorrelationId
FROM admin.audit_logs
WHERE Action = N'admin.reporting.read'
ORDER BY CreatedAt DESC;
```

Portal booking accepts for a tenant (metadata contains `tenantId`):

```sql
SELECT TOP 50 CreatedAt, ActorId, Action, EntityId, MetadataJson
FROM admin.audit_logs
WHERE Action = N'booking.accepted'
  AND MetadataJson LIKE N'%"tenantId":"<TENANT_GUID>"%'
ORDER BY CreatedAt DESC;
```

Cross-tenant denials (SOC2-02 spot-check):

```sql
SELECT TOP 20 CreatedAt, ActorId, Action, EntityId, MetadataJson
FROM admin.audit_logs
WHERE Action = N'security.cross_tenant_denied'
ORDER BY CreatedAt DESC;
```

Duplicate approve rows (should be **one** per approve — middleware suppressed):

```sql
SELECT EntityId, Action, COUNT(*) AS Cnt
FROM admin.audit_logs
WHERE Action = N'business.approved'
  AND CreatedAt > DATEADD(day, -7, SYSUTCDATETIME())
GROUP BY EntityId, Action
HAVING COUNT(*) > 1;
```

---

## 5. Staging spot-check (after deploy)

Requires MFA admin and real tenant data — **not** run on dev branch merge.

1. Admin: open Insights → MRR/GMV → confirm `admin.reporting.read` (and `admin.metrics.read` if metrics UI used).
2. Portal: accept one pending booking → `booking.accepted` with business user `sub`.
3. Portal: save notification preferences → `tenant.notification_preferences_updated`.
4. Intentionally fail an admin PATCH (validation) → **no** new generic `admin.patch` row for that failure.
5. Subscription webhook: unsigned body rejected when secret set; valid HMAC → `subscription.webhook_received` even if body parse fails later.

Export checklist also in [soc2-readiness-checklist.md §C](./soc2-readiness-checklist.md).

---

## 6. Logging vs audit

- **Audit table:** compliance trail for mutations and sensitive reads listed above.
- **Application logs:** use `PiiMasker`; never log full webhook bodies, emails, or message text.
- Webhook receipt: audit event type + intent id only ([AGENTS.md](../AGENTS.md) Murphy rules).

---

## 7. Tests (local / CI)

- `tests/Adeni.Api.Tests/Middleware/MiddlewareTests.cs` — middleware 2xx, dedupe, action mapping
- `tests/Adeni.Api.Tests/Integration/AdminReportingAuditIntegrationTests.cs` — reporting read audit
- `tests/Adeni.Api.Tests/Integration/PortalSoc2AuditIntegrationTests.cs` — portal high-impact audits
- `tests/Adeni.Infrastructure.Tests/Booking/BookingFlowTests.cs` — booking accept audit
- `tests/Adeni.Infrastructure.Tests/Tenancy/BusinessOnboardingServiceTests.cs` — verification submit audit

Run: `dotnet test Adeni.slnx -c Release`
