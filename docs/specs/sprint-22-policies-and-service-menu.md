# Spec: Business policies + service menu groups

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 22 — Policies & service menu |
| **Author** | Agent + product |
| **Status** | Implemented |
| **Created** | 2026-10-08 |

---

## 1. Goal (one sentence)

Let businesses publish booking/payment/cancellation/terms policies from the portal (shown on Discover + accepted at confirm), and organize the public service menu into searchable collections.

---

## 2. Context

- Follow-up to Sprint 21 public-page templates and Zenoti-style booking discovery.
- Product direction: profile → services → book; portal owns policies and catalog.
- Modules: **Tenancy** (policies + public page section), **Booking** (menu groups + offering fields), **Discovery** (public DTO + services payload), **Portal** + **Discover**.

**Slices**

| Slice | Scope |
|-------|--------|
| **1 (this first)** | Per-tenant policy texts, public-page `policies` section, portal editor, Discover display + booking accept checkbox |
| **2 (then)** | Service menu groups (“collections”), assign/order services, public search on menu + booking service step |

---

## 3. In scope

### Slice 1 — Policies

- [ ] Four policy fields on `BusinessProfile`: booking, payment, cancellation, terms (plain text)
- [ ] `requirePolicyAcceptance` flag
- [ ] `publicPage.sections.policies` toggle (default off)
- [ ] `PATCH /api/v1/tenant/policies` + expose on tenant profile + public business profile
- [ ] Portal: policies editor on Public page settings
- [ ] Discover: Policies section when enabled and any text present; booking confirm checkbox when `requirePolicyAcceptance`
- [ ] Cache invalidation on policy write (`tenant:{id}:profile` + location slugs)

### Slice 2 — Menu groups + search

- [ ] `ServiceMenuGroup` entity (name, sortOrder) per tenant
- [ ] `ServiceOffering.MenuGroupId` (nullable) + `SortOrder`
- [ ] Tenant CRUD for groups; create/update service accepts `menuGroupId` + `sortOrder`
- [ ] Public/tenant services responses include `groups` + item group/sort fields
- [ ] Portal Services: manage collections + assign services
- [ ] Discover: group services under collection headings; client-side search on services section + booking panel

## 4. Out of scope

- Staff / practitioners
- Multi-service cart, add-ons, multi-guest
- Rich HTML / file upload for policies (plain text only)
- Structured cancel windows / fee engines
- Guest checkout / CRM overhaul
- Flutter parity

---

## 5. API contract

### Policies (on profile + public)

```json
{
  "policies": {
    "booking": "…",
    "payment": "…",
    "cancellation": "…",
    "terms": "…",
    "requireAcceptance": true
  }
}
```

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `PATCH` | `/api/v1/tenant/policies` | Business JWT + `X-Tenant-Id` | Upsert policy texts + requireAcceptance |
| `GET` | `/api/v1/tenant/profile` | Business | Includes `policies` |
| `GET` | `/api/v1/businesses/{slug}` | Anonymous | Includes `policies` when any text set |
| `PATCH` | `/api/v1/tenant/public-page` | Business | `sections.policies` boolean |

Validation: each text max 8000 chars; empty → null.

### Menu groups

```json
{
  "groups": [{ "id": "…", "name": "Hair", "sortOrder": 0 }],
  "items": [{ "id": "…", "name": "Cut", "menuGroupId": "…", "sortOrder": 0, "…": "…" }]
}
```

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/tenant/service-menu-groups` | Business | List groups |
| `POST` | `/api/v1/tenant/service-menu-groups` | Business | Create `{ name, sortOrder? }` |
| `PATCH` | `/api/v1/tenant/service-menu-groups/{id}` | Business | Update name/sort |
| `DELETE` | `/api/v1/tenant/service-menu-groups/{id}` | Business | Delete; clear FK on offerings |
| `GET` | `/api/v1/tenant/services` | Business | `{ groups, items }` |
| `GET` | `/api/v1/businesses/{slug}/services` | Anonymous | `{ groups, items }` |
| `POST`/`PATCH` | `/api/v1/tenant/services` | Business | Optional `menuGroupId`, `sortOrder` |

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `BusinessProfile`, new `ServiceMenuGroup`, `ServiceOffering` |
| Application | Tenancy DTOs/mapper; `IServiceCatalogService` + new `IServiceMenuGroupService` |
| Infrastructure | EF + migration; onboarding; catalog; discovery mapping |
| Api | `TenantController`, `TenantServicesController`, new groups controller, `DiscoveryController` |
| Tests | Mapper/policy validation; menu group CRUD; public profile policies |
| Frontend | `@adeni/shared`, `@adeni/api-client`, portal public-page + services, discover business + booking-panel |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Policies + groups are tenant-scoped; query filters on new entity |
| **Auth / roles** | Mutations: business JWT; public read anonymous |
| **Cache** | Invalidate profile caches on policy + public-page writes |
| **Audit** | N/A (tenant self-serve, not `/admin/*`) |
| **PII** | Policy text is business content, not customer PII |
| **Observability** | Existing correlation only |

---

## 8. Tests

- [ ] Unit: public page mapper accepts `policies` section; rejects invalid policy length
- [ ] Unit/integration: update policies persists and appears on public profile
- [ ] Unit: menu group create/assign/delete clears FK
- [ ] Services list ordering: group sort → service sort → name

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Acceptance criteria

- [ ] Portal can edit four policies + require-acceptance; toggle Policies section with template settings
- [ ] Discover shows Policies when section on and content exists
- [ ] Booking confirm blocks submit until accept when required
- [ ] Portal can create collections and assign services
- [ ] Discover menu shows collections with search; booking service step has search

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| Markdown vs plain text | Plain text for V1; preserve newlines in UI |
| Ungrouped services | Shown under “Other services” after named groups |
| Empty policies section on | Hide section if all texts empty even when toggle on |
