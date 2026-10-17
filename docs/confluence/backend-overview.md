# Backend — Overview (.NET modular monolith)

> **Parent:** [Technical Documentation](./technical-documentation-index.md)  
> **Repo:** `src/Adeni.{Domain,Application,Infrastructure,Api}`

---

## 1. Purpose

The Adeni API is a **single deployable ASP.NET Core application** organized as a **modular monolith**: vertical feature modules inside Clean Architecture layers. All business rules live here; web and mobile clients are thin.

---

## 2. Layer responsibilities

| Layer | Project | Contains |
|-------|---------|----------|
| Domain | `Adeni.Domain` | Entities, enums, domain events, `Result`/`Error`, no framework deps |
| Application | `Adeni.Application` | `I*Service` ports, DTOs, options, cross-module contracts |
| Infrastructure | `Adeni.Infrastructure` | EF Core, Redis, Auth0, Paystack/Stripe, module service implementations |
| API | `Adeni.Api` | Controllers, middleware, auth policies, OpenAPI (Development) |

**Dependency direction:** Domain ← Application ← Infrastructure ← Api.

---

## 3. Registered modules

Modules are wired in `AddInfrastructure`:

| Extension method | Namespace folder | Application ports (examples) |
|------------------|------------------|--------------------------------|
| `AddIdentityModule` | `Identity/` | `IAuthSyncService`, `IStaffAccessService`, `IPortalPermissionService` |
| `AddAdminModule` | `Admin/` | `IAdminBusinessService`, `IAdminCustomerService`, `IAdminMarketService`, revenue/transactions |
| `AddTenancyModule` | `Tenancy/` | `IBusinessOnboardingService`, `ITenantCustomerService`, locations |
| `AddCatalogModule` | `Catalog/` | `ICategoryService`, `IBusinessCapabilitiesService` |
| `AddBookingModule` | `Booking/` | `IBookingService`, `IQueueService`, `IStaffService`, `IQuoteRequestService`, … |
| `AddDashboardModule` | `Dashboard/` | `ITenantDashboardService` |
| `AddReviewsModule` | `Reviews/` | `IReviewService` |
| `AddDiscoveryModule` | `Discovery/` | `IDiscoveryService` |
| `AddNotificationsModule` | `Notifications/` | `INotificationDispatcher`, preferences |
| `AddPaymentsModule` | `Payments/` | `IPaymentProvider`, orchestrator, billing documents |
| `AddSubscriptionsModule` | `Subscriptions/` | `ISubscriptionService`, billing providers, entitlements |
| `AddTrustModule` | `Trust/` | `IVerificationBadgeService` |
| `AddMessagingModule` | `Messaging/` | `IMessageThreadService`, FAQ auto-responder |

Also registered (not separate “product modules”): **Markets** (`AddAdeniMarkets`), **Translation** (`AddAdeniTranslation`), **Storage** (`AddAdeniStorage` from Api `Program.cs`), **Events** (`AddAdeniDomainEvents`), **Auditing** (`IAuditLogWriter`), **Caching** (`ICacheService`).

---

## 4. HTTP surface conventions

- Version prefix: **`/api/v1/`**
- Admin: **`/api/v1/admin/`** (role `admin`, MFA when enabled)
- Tenant business: **`/api/v1/tenant/`** + **`X-Tenant-Id`**
- Public: discovery, public business profiles, anonymous booking where designed
- Errors: RFC 7807 Problem Details with stable **`code`** — see `docs/api-errors.md`
- OpenAPI: Scalar at `/scalar/v1` in Development

Full route list per module: see each [backend module page](./technical-documentation-index.md#2-backend-modules-quick-map).

---

## 5. Middleware pipeline

Order in `ApplicationBuilderExtensions`:

1. `CorrelationIdMiddleware` — propagates `X-Correlation-Id`
2. `SecurityHeadersMiddleware`
3. Authentication / Authorization (Auth0 JWT)
4. *(Development only)* Dev auth middleware for business/customer/admin
5. `TenantScopeMiddleware` — resolves tenant context from header/route
6. `TenantAccessMiddleware` — JWT tenant claim vs header
7. `TenantFilterSyncMiddleware` — aligns EF global filters
8. `AuditMiddleware` — admin mutation audit trail

---

## 6. Persistence

- **Primary store:** SQL Server via `AdeniDbContext`
- **Schema project:** `db/Adeni.Database` (SqlPackage publish)
- **Tenant filtering:** global query filters on `ITenantEntity`
- **Stored procedures:** `StoredProcedureExecutor` for discovery search performance

---

## 7. Cross-module communication

| Pattern | Use when |
|---------|----------|
| Inject `I*Service` from Application | Synchronous validation or reads across modules |
| Domain events + `IDomainEventDispatcher` | Side effects (notifications, payment reactions) |
| Shared DB, logical schemas | Same monolith; e.g. payments schema separation |

**Forbidden:** Infrastructure module A importing Infrastructure module B (enforced by NetArchTest).

---

## 8. Testing

| Suite | Location | Focus |
|-------|----------|-------|
| Unit | `tests/Adeni.Infrastructure.Tests` | Services, providers, tenancy |
| API integration | `tests/Adeni.Api.Tests/Integration` | HTTP contracts, isolation |
| Architecture | `ModuleBoundaryArchitectureTests` | Module deps |

Run: `dotnet test Adeni.slnx -c Release`

---

## 9. Adding a new backend capability

1. Domain entity/event under `Adeni.Domain/{Module}/`
2. Port + DTOs in `Adeni.Application/{Module}/`
3. Implementation in `Adeni.Infrastructure/{Module}/`
4. Register in `{Module}ServiceCollectionExtensions` and `AddInfrastructure`
5. Controller in `Adeni.Api/Controllers/`
6. Update `packages/shared` + `@adeni/api-client` if contract changes
7. Tests + architecture rules if new module folder

Checklist source: `docs/architecture.md` §4.4.

---

## 10. Module documentation index

| Module | Page |
|--------|------|
| Identity & Auth | [identity-and-auth.md](./backend/identity-and-auth.md) |
| Tenancy | [tenancy.md](./backend/tenancy.md) |
| Catalog & Markets | [catalog-and-markets.md](./backend/catalog-and-markets.md) |
| Booking | [booking.md](./backend/booking.md) |
| Discovery | [discovery.md](./backend/discovery.md) |
| Reviews | [reviews.md](./backend/reviews.md) |
| Payments | [payments.md](./backend/payments.md) |
| Subscriptions | [subscriptions.md](./backend/subscriptions.md) |
| Messaging | [messaging.md](./backend/messaging.md) |
| Notifications | [notifications.md](./backend/notifications.md) |
| Trust | [trust.md](./backend/trust.md) |
| Dashboard | [dashboard.md](./backend/dashboard.md) |
| Admin | [admin.md](./backend/admin.md) |
| Storage | [storage.md](./backend/storage.md) |
| Translation | [translation.md](./backend/translation.md) |
| Platform | [platform-cross-cutting.md](./backend/platform-cross-cutting.md) |

---

_Last updated: October 2026_
