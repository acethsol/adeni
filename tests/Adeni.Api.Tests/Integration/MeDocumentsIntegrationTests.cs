namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Api.Middleware;
using Adeni.Domain.Booking;
using Adeni.Domain.Finance;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class MeDocumentsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CustomerSub = "auth0|me-docs-customer";
    private const string OtherCustomerSub = "auth0|me-docs-other";

    private readonly WebApplicationFactory<Program> _factory;

    public MeDocumentsIntegrationTests(WebApplicationFactory<Program> factory) =>
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
        });

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/me/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsIssuedDocumentsForCustomerBookingsOnly()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Salon",
                Status = TenantStatus.Verified,
                CreatedAt = now,
            });
            db.Customers.Add(new Customer
            {
                Id = customerId,
                Auth0Sub = CustomerSub,
                Name = "Ada",
                CreatedAt = now,
            });
            db.Customers.Add(new Customer
            {
                Id = otherCustomerId,
                Auth0Sub = OtherCustomerSub,
                Name = "Other",
                CreatedAt = now,
            });
            var serviceId = Guid.NewGuid();
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = serviceId,
                TenantId = tenantId,
                Name = "Fade",
            });
            db.Bookings.Add(new BookingRecord
            {
                Id = bookingId,
                TenantId = tenantId,
                ServiceOfferingId = serviceId,
                CustomerId = customerId,
                StartAt = now.AddDays(1),
                EndAt = now.AddDays(1).AddMinutes(30),
                Status = BookingStatus.Confirmed,
                CreatedAt = now,
                UpdatedAt = now,
            });
            var doc = new BillingDocument
            {
                Id = documentId,
                TenantId = tenantId,
                Kind = BillingDocumentKind.Receipt,
                Status = BillingDocumentStatus.Issued,
                BookingId = bookingId,
                DocumentNumber = "R-9001",
                BusinessNameSnapshot = "Salon",
                CustomerNameSnapshot = "Ada",
                Currency = "NGN",
                Subtotal = 5000m,
                Total = 5000m,
                PublicToken = Guid.NewGuid().ToString("N"),
                IssuedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.BillingDocuments.Add(doc);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevCustomerAuthMiddleware.DevAuth0SubHeader, CustomerSub);

        var listResponse = await client.GetAsync("/api/v1/me/documents");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var items = listJson.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(documentId.ToString(), items[0]!.GetProperty("id").GetString());

        var getResponse = await client.GetAsync($"/api/v1/me/documents/{documentId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add(DevCustomerAuthMiddleware.DevAuth0SubHeader, OtherCustomerSub);
        var forbidden = await otherClient.GetAsync($"/api/v1/me/documents/{documentId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
