namespace Adeni.Api.Tests.Integration;

using System.Net;
using Adeni.Api.Middleware;
using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auditing;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class TenantReportingExportIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TenantReportingExportIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty,
                    ["Auth0:Enabled"] = "false",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditLogWriter>();
                services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
            });
        });

    [Fact]
    public async Task BookingsRegister_csv_returns_ok_for_starter()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|report-export-csv";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub, SubscriptionSku.Starter);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/bookings-register?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var writer = _factory.Services.GetRequiredService<IAuditLogWriter>() as InMemoryAuditLogWriter;
        Assert.NotNull(writer);
        Assert.Contains(
            writer!.Entries,
            e => e.Action == AuditActions.TenantReportingExported
                && e.EntityType == "tenant"
                && e.EntityId == tenantId.ToString()
                && e.ActorId == ownerSub);
    }

    [Fact]
    public async Task BookingsRegister_xlsx_returns_forbidden_without_excel_entitlement()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|report-export-xlsx";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub, SubscriptionSku.Starter);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/bookings-register?from={from}&to={to}&format=xlsx");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("entitlements.excel_export", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Compensation_csv_returns_forbidden_without_finance_permission()
    {
        var tenantId = Guid.NewGuid();
        const string staffSub = "auth0|report-export-finance-deny";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, staffSub, SubscriptionSku.Professional, role: "receptionist");
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, staffSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/compensation?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BookingsRegister_csv_returns_forbidden_when_multi_location_gate_blocks()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|report-export-multi-loc";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub, SubscriptionSku.Professional);
            var now = DateTimeOffset.UtcNow;
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Branch A",
                Slug = "branch-a",
                MarketId = "lagos",
                AddressLine = "1 Test St",
                Area = "Lekki",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Branch B",
                Slug = "branch-b",
                MarketId = "lagos",
                AddressLine = "2 Test St",
                Area = "VI",
                IsPrimary = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/bookings-register?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("entitlements.multi_location_reporting", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TenantCustomers_csv_returns_ok_with_multiple_locations_without_multi_location_entitlement()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|report-export-tenant-customers-multi-loc";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub, SubscriptionSku.Professional);
            var now = DateTimeOffset.UtcNow;
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Branch A",
                Slug = "branch-a-customers",
                MarketId = "lagos",
                AddressLine = "1 Test St",
                Area = "Lekki",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Branch B",
                Slug = "branch-b-customers",
                MarketId = "lagos",
                AddressLine = "2 Test St",
                Area = "VI",
                IsPrimary = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/tenant-customers?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TenantCustomers_csv_returns_ok_for_starter()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|report-export-tenant-customers";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub, SubscriptionSku.Starter);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/tenant-customers?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BookingsRegister_csv_filters_by_locationId()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|report-export-loc-filter";
        var locA = Guid.NewGuid();
        var locB = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub, SubscriptionSku.Professional);
            db.Customers.Add(new Customer
            {
                Id = customerId,
                Auth0Sub = "auth0|cust",
                Name = "Test Customer",
                Phone = "+2348011111111",
                CreatedAt = now,
            });
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = serviceId,
                TenantId = tenantId,
                Name = "Cut",
                PriceAmount = 1000m,
                Currency = "NGN",
                DurationMinutes = 30,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = locA,
                TenantId = tenantId,
                Name = "Branch A",
                Slug = "branch-a-filter",
                MarketId = "lagos",
                AddressLine = "1 Test St",
                Area = "Lekki",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = locB,
                TenantId = tenantId,
                Name = "Branch B",
                Slug = "branch-b-filter",
                MarketId = "lagos",
                AddressLine = "2 Test St",
                Area = "VI",
                IsPrimary = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.Bookings.Add(new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceOfferingId = serviceId,
                CustomerId = customerId,
                BusinessLocationId = locA,
                StartAt = now.AddHours(1),
                EndAt = now.AddHours(2),
                Status = BookingStatus.Confirmed,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.Bookings.Add(new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceOfferingId = serviceId,
                CustomerId = customerId,
                BusinessLocationId = locB,
                StartAt = now.AddHours(3),
                EndAt = now.AddHours(4),
                Status = BookingStatus.Confirmed,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/bookings-register?from={from}&to={to}&format=csv&locationId={locA:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        var dataLines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, dataLines.Length);
    }

    [Fact]
    public async Task BookingsRegister_csv_returns_ok_for_practitioner_with_bookings_self_only()
    {
        var tenantId = Guid.NewGuid();
        const string practitionerSub = "auth0|report-export-practitioner";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, practitionerSub, SubscriptionSku.Professional, role: "practitioner");
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, practitionerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/bookings-register?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SalesSummary_csv_returns_forbidden_for_practitioner_without_finance()
    {
        var tenantId = Guid.NewGuid();
        const string practitionerSub = "auth0|report-export-practitioner-finance-deny";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, practitionerSub, SubscriptionSku.Professional, role: "practitioner");
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, practitionerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/sales-summary?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SalesSummary_csv_returns_ok_for_accountant_with_finance_permission()
    {
        var tenantId = Guid.NewGuid();
        const string accountantSub = "auth0|report-export-accountant";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, accountantSub, SubscriptionSku.Professional, role: "accountant");
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, accountantSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/sales-summary?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BookingsRegister_csv_returns_forbidden_for_accountant_without_bookings()
    {
        var tenantId = Guid.NewGuid();
        const string accountantSub = "auth0|report-export-accountant-bookings-deny";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, accountantSub, SubscriptionSku.Professional, role: "accountant");
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, accountantSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/bookings-register?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffRoster_csv_returns_ok_for_professional()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|report-export-staff-roster";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub, SubscriptionSku.Professional);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/staff-roster?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Compensation_csv_escapes_staff_text_and_preserves_negative_amount()
    {
        var tenantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var ownerSub = $"auth0|compensation-safety-{tenantId:N}";
        var now = DateTimeOffset.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub, SubscriptionSku.Professional);
            db.StaffMembers.Add(new StaffMember
            {
                Id = staffId, TenantId = tenantId, DisplayName = "=SUM(1,2)\r\n\"Àda\"",
                RoleKey = "stylist", IsActive = true, CreatedAt = now, UpdatedAt = now,
            });
            db.StaffCompensationAllocations.Add(new Adeni.Domain.Finance.StaffCompensationAllocation
            {
                Id = Guid.NewGuid(), TenantId = tenantId, StaffMemberId = staffId,
                Type = Adeni.Domain.Finance.StaffCompensationType.Commission,
                Amount = -12.50m, Currency = "NGN", CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());
        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/tenant/reporting/exports/compensation?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("CreatedAt,StaffMemberId,StaffDisplayName,Type,Amount,Currency,BookingId,PaymentIntentId", csv);
        Assert.Contains("\"'=SUM(1,2)\r\n\"\"Àda\"\"\",commission,-12.50,NGN,,", csv);
    }

    private static void SeedTenant(
        AdeniDbContext db,
        Guid tenantId,
        string auth0Sub,
        SubscriptionSku sku,
        string role = "owner")
    {
        var now = DateTimeOffset.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Reporting Export Test",
            Status = TenantStatus.Verified,
            SubscriptionSku = sku,
            CreatedAt = now,
            VerifiedAt = now,
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = auth0Sub,
            Role = role,
            CreatedAt = now,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            UpdatedAt = now,
        });
    }
}
