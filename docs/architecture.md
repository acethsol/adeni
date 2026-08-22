# Adeni — Modular monolith architecture

> **Status:** Living architecture doc | **Last updated:** August 2026  
> **Parent:** [product-strategy.md §7.2](./product-strategy.md#72-architecture--modular-monolith-not-microservices) | **Confluence:** [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649)

Adeni runs as a **modular monolith**: one deployable API, clear module boundaries, designed for extraction — **not** microservices on day one. This matches the ChatGPT strategy research and fits a solo/small team building toward local-service commerce infrastructure.

---

## 1. Principles

| Principle | Meaning |
|-----------|---------|
| **One deployable unit** | Single `Adeni.Api` process + one PostgreSQL database (schema-separated where useful). One release train, one observability surface. |
| **Modules, not layers only** | Clean Architecture layers (Domain → Application → Infrastructure → Api) **and** vertical modules (Booking, Tenancy, Discovery, …) inside those layers. |
| **Communicate through contracts** | Modules expose `I*Service` interfaces in `Adeni.Application`. No module reaches into another module's Infrastructure folder or EF entities directly. |
| **Domain events for side effects** | `BookingConfirmed` → notify, update analytics, enqueue review prompt. In-process dispatcher now; message bus when a module extracts. |
| **Design for extraction** | When traffic, team size, or compliance justify it, a module can become its own service. Boundaries are drawn so extraction is boring, not a rewrite. |
| **Frontend mirrors contracts** | `packages/shared` (Zod schemas, capabilities, business types) + `packages/api-client` stay the public contract for web/mobile/AI agents. |

**Explicit non-goal (early):** separate repos, separate databases per service, distributed tracing as a prerequisite to ship features, or Kubernetes for five containers.

---

## 2. Solution layout

```
src/
  Adeni.Domain/           Entities, value objects, domain events (per module folder)
  Adeni.Application/      Module interfaces (ports), DTOs, options
  Adeni.Infrastructure/   Module implementations (adapters), EF, Redis, external APIs
  Adeni.Api/              HTTP controllers, middleware, composition root
apps/
  web/                    Next.js — route groups mirror portal surfaces, not backend modules
  mobile/                 Expo
packages/
  shared/                 Cross-client types aligned with API contracts
  api-client/             Typed HTTP client
tests/                    Unit + integration + architecture tests per module
```

The **composition root** is `Adeni.Api` + `Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`. Target state: each module registers itself via `AddBookingModule()`, `AddTenancyModule()`, etc., called from the composition root.

---

## 3. Module map

### 3.1 Current modules (shipped)

| Module | Domain folder | Application port(s) | Responsibility |
|--------|---------------|---------------------|----------------|
| **Tenancy** | `Domain/Tenancy` | `IBusinessOnboardingService`, location services | Tenants, profiles, verification docs, locations |
| **Catalog** | `Domain/Catalog` | `ICategoryService`, `IMarketCatalog` | Categories, markets, business-type defaults (Sprint 15) |
| **Booking** | `Domain/Booking` | `IBookingService`, `IAvailabilityService`, `IServiceCatalogService` | Services, availability, slot search, bookings |
| **Discovery** | — (read models) | `IDiscoveryService` | Public search, geo, keyword; reads Tenancy + Reviews |
| **Reviews** | `Domain/Booking/Review` | `IReviewService` | Post-booking reviews, ratings aggregation |
| **Identity** | `Domain/Identity` | `IAuthSyncService` | Auth0 sync, customer/business user linkage |
| **Admin** | — | `IAdminBusinessService`, `IAdminCustomerService`, `IAdminMarketService` | Verification queue, markets admin, SOC2 export/delete |
| **Storage** | — | `IFileStorage`, `ITenantMediaService` | Cover uploads, presigned URLs |
| **Auditing** | `Domain/Auditing` | `IAuditLogWriter` | Cross-cutting audit trail |
| **Markets** | `Domain/Catalog` | Market catalog loader | Multi-market config |
| **Translation** | — | `ITranslationService` | i18n support |

**Shared kernel:** `Domain/Common` (`Result`, `Error`), `Application/Abstractions` (`ITenantContext`, correlation).

### 3.2 Planned modules (by sprint)

| Module | Sprint | Application port | Notes |
|--------|--------|-------------------|-------|
| **Notifications** | 15 | `INotificationDispatcher` | Email/push/SMS; subscribes to booking events |
| **Payments** | 17 | `IPaymentProvider`, `IPaymentOrchestrator` | Paystack; no wallet tables |
| **Messaging** | 18 | `IMessageThreadService` | In-app threads; WhatsApp deep links |
| **Subscriptions** | 16 | `ISubscriptionService`, entitlements | SaaS tiers |
| **Quotes** | 19 | `IQuoteRequestService` | Full quote workflow on business-type framework |
| **AI** | 20 | Tool-calling against existing module APIs only | No direct DB from LLM |

---

## 4. Module rules

### 4.1 Allowed dependencies

```
Adeni.Api  →  Adeni.Application, Adeni.Infrastructure (composition only)
Adeni.Infrastructure.{Module}  →  Adeni.Application.{Module}, Adeni.Domain
Adeni.Application.{Module}  →  Adeni.Domain, other Application interfaces (not Infrastructure)
Adeni.Domain  →  nothing external
```

- **Discovery** may call **Tenancy** and **Reviews** via their Application interfaces or denormalized read queries owned by Discovery — not by importing `BusinessOnboardingService` internals.
- **Booking** confirms a tenant is `Verified` via Tenancy port, not by duplicating verification logic.
- **Reviews** requires a completed booking via Booking port.

### 4.2 Cross-module data access

| Pattern | When | Example |
|---------|------|---------|
| **Application service call** | Need another module to act or validate | Booking checks tenant status via Tenancy |
| **Domain event** | Fire-and-forget side effect | `BookingConfirmed` → Notifications |
| **Read model / query in owning module** | Discovery needs ratings | Reviews exposes `GetPublicRatingSummary(tenantId)` |
| **Shared DB, separate schemas** | Same monolith, logical separation | `booking.*`, `tenancy.*`, `payments.*` PostgreSQL schemas (incremental) |

**Avoid:** BookingService querying `Reviews` DbSet directly; Discovery embedding SQL against tenancy tables without going through a defined port.

### 4.3 Domain events (target — Sprint 15a)

```csharp
// Adeni.Domain — example
public sealed record BookingConfirmed(Guid BookingId, Guid TenantId, DateTimeOffset At) : IDomainEvent;

// Adeni.Application
public interface IDomainEventDispatcher
{
    Task PublishAsync<T>(T domainEvent, CancellationToken ct) where T : IDomainEvent;
}

// Handlers live in the module that reacts (Notifications, Reviews, Analytics)
```

Start **in-process** (scoped dispatcher after `SaveChanges`). Swap to Azure Service Bus / RabbitMQ when Notifications or Analytics extracts.

### 4.4 New feature checklist

Before merging any sprint work:

1. Code lives under the correct **module folder** in Domain / Application / Infrastructure.
2. Cross-module calls go through **Application interfaces** only.
3. New entities get **tenant isolation** if tenant-scoped (`ITenantEntity` + global filter).
4. Side effects use **domain events** (once 15a lands) or explicit port calls — not hidden coupling.
5. API surface documented in OpenAPI; client types updated in `packages/shared` when contract changes.
6. Architecture test added if introducing a new module or dependency direction.

---

## 5. Frontend monorepo alignment

The client stack is also a monolith split by **apps**, not micro-frontends:

| Package | Role |
|---------|------|
| `packages/shared` | Business types, capabilities, Zod schemas — must match API contracts |
| `packages/api-client` | HTTP client; one client, module-shaped endpoint groups |
| `apps/web` | Next.js App Router; `/business`, `/admin`, public routes |
| `apps/mobile` | Expo tabs; same API contracts as web |

**Rule:** business-type workflow branching (Sprint 15) reads `capabilities[]` from shared config + API — never hard-code category slugs in UI without a shared definition.

---

## 6. Extraction criteria

Extract a module to its own service **only when** at least two apply:

- Independent scaling need (e.g. search QPS >> rest of API)
- Separate team ownership
- Different release cadence or compliance boundary (PCI for Payments)
- Proven stable interface + event contract for 2+ quarters

**First extraction candidates:** Search/Discovery, Notifications, Messaging, Payments orchestration, AI inference.

Extraction steps (when justified): copy module folders → new service → replace in-process calls with HTTP/events → keep shared contract package → migrate schema or sync via events.

---

## 7. Current gaps → sprint work

| Gap | Status | Sprint |
|-----|--------|--------|
| Folder-based modules exist | ✅ | — |
| Single flat DI registration | ✅ Per-module `AddXxxModule()` | **15a** |
| Domain events | ✅ In-process dispatcher | **15a** |
| Module boundary architecture tests | ✅ NetArchTest | **15a** |
| PostgreSQL schema separation | ⚠️ Single EF model | Incremental with Payments (17) |
| Payments as isolated module | ⚠️ Stub only (`IPaymentProvider`) | **17** (Paystack) |
| Messaging as isolated module | ❌ | **18** |
| Notifications decoupled from Booking | ✅ Domain events (logging stub) | **15b** |

---

## 8. What we will NOT do (architecture)

- Microservices before modular monolith boundaries are enforced in code and tests
- Shared “god” DbContext methods that bypass module ports
- LLM / AI agents with direct database access
- Per-module databases before extraction is decided
- Premature event bus infrastructure before in-process events prove the contracts

---

Confluence: [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649)

## 9. Related documents

| Doc | Purpose |
|-----|---------|
| [product-strategy.md](./product-strategy.md) | Business strategy + §7 technical input |
| [tenant-isolation.md](./tenant-isolation.md) | Multi-tenant EF filters and route classes |
| [frontend.md](./frontend.md) | Web + Expo monorepo |
| [observability.md](./observability.md) | App Insights, SLOs |
| [sprints.md](./sprints.md) | Sprint 15a and module rollout by sprint |
