# Spec: [FEATURE NAME]

> Copy this file to `docs/specs/sprint-XX-short-name.md` before implementation.  
> Use standing play 1 in [AGENTS.md](../../AGENTS.md). Do not write code until this spec is reviewed.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint XX — [name] |
| **Author** | |
| **Status** | Draft / Approved / Implemented |
| **Created** | YYYY-MM-DD |

---

## 1. Goal (one sentence)

What user or business outcome does this deliver?

---

## 2. Context

- Link to [docs/sprints.md](../sprints.md) task
- Related strategy: [product-strategy.md](../product-strategy.md) section if any
- Module(s): Tenancy / Booking / Discovery / Payments / …

---

## 3. In scope

- [ ] …
- [ ] …

## 4. Out of scope

Explicitly list what this spec does **not** include (prevents AI scope creep):

- …
- …

---

## 5. API contract

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| | | | |

**Request / response shapes** (or link to `packages/shared` types):

```json
{}
```

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | |
| Application | |
| Infrastructure | |
| Api | |
| Tests | |
| Frontend (`apps/web`, `apps/mobile`, `packages/shared`) | |

Follow [architecture.md](../architecture.md) module boundaries.

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Tenant-scoped? Query filters? `X-Tenant-Id`? |
| **Auth / roles** | Customer / business / admin? MFA? |
| **Cache** | New keys? Invalidation? |
| **Audit** | Admin mutation → `admin.audit_logs`? |
| **PII** | Phone/email in response or logs? Masking? |
| **Observability** | Correlation ID, App Insights events? |

---

## 8. Tests

- [ ] Unit: …
- [ ] Integration: …
- [ ] Tenant cross-access denial (if tenant-scoped)
- [ ] Validation failure path

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [ ] …
- [ ] …

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| | |
