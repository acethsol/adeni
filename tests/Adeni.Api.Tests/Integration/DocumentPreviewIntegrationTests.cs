namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Text.Json;
using Adeni.Api.Middleware;
using Adeni.Application.Reporting;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class DocumentPreviewIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly Renderer renderer = new();
    private readonly Branding branding = new();
    public DocumentPreviewIntegrationTests(WebApplicationFactory<Program> factory) => this.factory = factory.WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Testing");
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:AdeniDb"] = "", ["Redis:ConnectionString"] = "", ["Auth0:Enabled"] = "false" }));
        b.ConfigureServices(s => { s.AddSingleton<IReportRenderer>(renderer); s.AddSingleton<IReportPdfBrandingLoader>(branding); });
    });
    private sealed class Renderer : IReportRenderer
    {
        public List<(ReportKind Kind, IReportDocumentData Data)> Calls { get; } = [];
        public byte[] Payload { get; set; } = "%PDF-private"u8.ToArray();
        public Task<Result<byte[]>> RenderDocumentAsync(ReportKind kind, IReportDocumentData data, CancellationToken cancellationToken = default)
        { Calls.Add((kind, data)); return Task.FromResult(Result.Success(Payload)); }
    }
    private sealed class Branding : IReportPdfBrandingLoader
    {
        public byte[] Bytes { get; set; } = [1, 2, 3];
        public Task<ReportPdfBranding> LoadForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(new ReportPdfBranding("Current name", Bytes));
        public Task<ReportPdfBranding> LoadForTenantAsync(Guid tenantId, string fallback, CancellationToken cancellationToken = default) => LoadForTenantAsync(tenantId, cancellationToken);
    }
    private async Task<(HttpClient Client, Guid Tenant, Guid Document, Guid Location)> Seed(string role = "owner", BillingDocumentKind kind = BillingDocumentKind.Invoice)
    {
        var tenant = Guid.NewGuid(); var location = Guid.NewGuid(); var sub = "preview-" + tenant;
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Tenants.Add(new Tenant { Id = tenant, Name = "Current name", Status = TenantStatus.Verified, SubscriptionSku = SubscriptionSku.Professional, BillingStatus = TenantBillingStatus.Active });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = sub, Role = role });
        db.BusinessLocations.Add(new BusinessLocation { Id = location, TenantId = tenant, Slug = location.ToString(), IsActive = true, IsPrimary = true });
        var service = new ServiceOffering { Id = Guid.NewGuid(), TenantId = tenant, Name = "Care" }; db.ServiceOfferings.Add(service);
        var booking = new BookingRecord { Id = Guid.NewGuid(), TenantId = tenant, ServiceOfferingId = service.Id, CustomerId = Guid.NewGuid(), BusinessLocationId = location }; db.Bookings.Add(booking);
        var doc = new BillingDocument { Id = Guid.NewGuid(), TenantId = tenant, Kind = kind, BookingId = booking.Id, Status = BillingDocumentStatus.Draft,
            BusinessNameSnapshot = "Historical business", CustomerNameSnapshot = "Ada", DocumentNumber = "DRAFT-TEST", PublicToken = Guid.NewGuid().ToString(), Currency = "CAD", Total = 12.50m };
        doc.Lines.Add(new BillingDocumentLine { Id = Guid.NewGuid(), TenantId = tenant, DocumentId = doc.Id, Description = "Care é", SortOrder = 0, Quantity = 1, UnitAmount = 12.50m, LineTotal = 12.50m });
        db.BillingDocuments.Add(doc); await db.SaveChangesAsync();
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, sub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenant.ToString());
        return (client, tenant, doc.Id, location);
    }

    [Theory]
    [InlineData("owner", HttpStatusCode.OK)]
    [InlineData("manager", HttpStatusCode.OK)]
    [InlineData("receptionist", HttpStatusCode.OK)]
    [InlineData("accountant", HttpStatusCode.OK)]
    [InlineData("practitioner", HttpStatusCode.Forbidden)]
    [InlineData("legacy-invalid", HttpStatusCode.Forbidden)]
    public async Task Preview_EnforcesRecognizedMembershipBeforeRendering(string role, HttpStatusCode expected)
    {
        var data = await Seed(role); using var client = data.Client; var calls = renderer.Calls.Count;
        var response = await client.GetAsync($"/api/v1/tenant/reporting/documents/{data.Document}/preview");
        Assert.Equal(expected, response.StatusCode); Assert.Equal(calls + (expected == HttpStatusCode.OK ? 1 : 0), renderer.Calls.Count);
    }

    [Theory]
    [InlineData(BillingDocumentKind.Receipt, ReportKind.BillingReceipt)]
    [InlineData(BillingDocumentKind.Invoice, ReportKind.BillingInvoice)]
    public async Task Preview_AndFinalDownload_UseIdenticalRendererInputsWithoutIssuing(BillingDocumentKind kind, ReportKind expected)
    {
        var data = await Seed(kind: kind); using var client = data.Client;
        var response = await client.GetAsync($"/api/v1/tenant/reporting/documents/{data.Document}/preview?locationId={data.Location}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore); Assert.True(response.Headers.CacheControl.Private);
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.DoesNotContain("Ada", response.Content.Headers.ContentDisposition!.FileName!);
        var preview = renderer.Calls.Last();
        var final = await client.GetAsync($"/api/v1/finance/documents/{data.Document}/pdf?locationId={data.Location}");
        Assert.Equal(HttpStatusCode.OK, final.StatusCode); var download = renderer.Calls.Last(); Assert.Equal(expected, preview.Kind); Assert.Equal(preview.Kind, download.Kind);
        Assert.Equal(JsonSerializer.Serialize(preview.Data), JsonSerializer.Serialize(download.Data));
        var inputs = Assert.IsType<BillingDocumentReportData>(preview.Data); Assert.Equal("Historical business", inputs.BusinessNameSnapshot);
        Assert.Equal(new byte[] { 1, 2, 3 }, inputs.BusinessLogoBytes); Assert.Equal(12.50m, inputs.Total); Assert.Equal("Care é", Assert.Single(inputs.Lines).Description);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        Assert.Equal(BillingDocumentStatus.Draft, db.BillingDocuments.Single(x => x.Id == data.Document).Status);
        Assert.False(db.PaymentIntents.Any(x => x.TenantId == data.Tenant)); Assert.False(db.BillingDocumentSequences.Any(x => x.TenantId == data.Tenant));
    }

    [Fact]
    public async Task PreviewAndFinalDownload_RejectOtherBranchAndTenantBeforeRenderer()
    {
        var data = await Seed(); var other = await Seed(); using var client = data.Client; using var second = other.Client;
        var branch = Guid.NewGuid(); using (var scope = factory.Services.CreateScope())
        { var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>(); db.BusinessLocations.Add(new BusinessLocation { Id = branch, TenantId = data.Tenant, Slug = branch.ToString(), IsActive = true }); await db.SaveChangesAsync(); }
        var calls = renderer.Calls.Count;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/tenant/reporting/documents/{data.Document}/preview?locationId={branch}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/finance/documents/{data.Document}/pdf?locationId={branch}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/tenant/reporting/documents/{other.Document}/preview")).StatusCode);
        Assert.Equal(calls, renderer.Calls.Count);
    }

    [Theory]
    [InlineData("void")]
    [InlineData("unlinked")]
    [InlineData("unlocated")]
    [InlineData("oversized")]
    public async Task Preview_UnsupportedOrUnboundedDocuments_DoNotRender(string scenario)
    {
        var data = await Seed(); using var client = data.Client;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>(); var doc = db.BillingDocuments.Single(x => x.Id == data.Document);
            if (scenario == "void") doc.Status = BillingDocumentStatus.Void;
            if (scenario == "unlinked") doc.BookingId = null;
            if (scenario == "unlocated") db.Bookings.Single(x => x.Id == doc.BookingId).BusinessLocationId = null;
            if (scenario == "oversized") doc.MethodLabel = new string('x', 301);
            await db.SaveChangesAsync();
        }
        var calls = renderer.Calls.Count; var response = await client.GetAsync($"/api/v1/tenant/reporting/documents/{data.Document}/preview");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode); Assert.Equal(calls, renderer.Calls.Count);
        Assert.DoesNotContain("Ada", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Preview_RejectsOversizedLogoBeforeNativeRenderer()
    {
        var data = await Seed(); using var client = data.Client; branding.Bytes = new byte[5 * 1024 * 1024 + 1];
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/tenant/reporting/documents/{data.Document}/preview")).StatusCode);
        Assert.Empty(renderer.Calls);
    }

    [Fact]
    public async Task Preview_RejectsOversizedOutputWithoutReturningItsContent()
    {
        var data = await Seed(); using var client = data.Client; renderer.Payload = new byte[DocumentPreviewService.MaxBytes + 1];
        var response = await client.GetAsync($"/api/v1/tenant/reporting/documents/{data.Document}/preview");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.NotEqual("application/pdf", response.Content.Headers.ContentType!.MediaType);
    }

}
