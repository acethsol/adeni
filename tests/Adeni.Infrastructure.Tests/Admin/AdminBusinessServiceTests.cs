namespace Adeni.Infrastructure.Tests.Admin;

using Adeni.Application.Admin;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Admin;
using Adeni.Application.Notifications;
using Adeni.Application.Realtime;
using Adeni.Application.Storage;
using Adeni.Infrastructure.Tests.Realtime;
using Adeni.Infrastructure.Subscriptions;
using Adeni.Infrastructure.Tests.Notifications;
using Adeni.Infrastructure.Tests.Storage;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class AdminBusinessServiceTests
{
    [Fact]
    public async Task GetPendingVerifications_returns_only_pending()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        db.Tenants.AddRange(
            new Tenant { Id = Guid.NewGuid(), Name = "Pending", Status = TenantStatus.PendingVerification, CreatedAt = DateTimeOffset.UtcNow },
            new Tenant { Id = Guid.NewGuid(), Name = "Verified", Status = TenantStatus.Verified, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();
        var pending = await service.ListPendingVerificationsAsync(null, 1, 25);

        Assert.True(pending.IsSuccess);
        Assert.Single(pending.Value!.Items);
        Assert.Equal("Pending", pending.Value.Items[0].Name);
    }

    [Fact]
    public async Task Approve_sets_verified_and_writes_audit()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Salon",
            Status = TenantStatus.PendingVerification,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();
        var result = await service.ApproveAsync(tenantId, "admin-1");

        Assert.True(result.IsSuccess);
        var tenant = await db.Tenants.FirstAsync(t => t.Id == tenantId);
        Assert.Equal(TenantStatus.Verified, tenant.Status);
        Assert.NotNull(tenant.VerifiedAt);

        var writer = (InMemoryAuditLogWriter)scope.ServiceProvider.GetRequiredService<Application.Abstractions.IAuditLogWriter>();
        Assert.Contains(writer.Entries, e => e.Action == Domain.Auditing.AuditActions.BusinessApproved);
    }

    [Fact]
    public async Task Approve_publishes_admin_pending_count_and_tenant_verification()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();

        db.Tenants.AddRange(
            new Tenant { Id = tenantId, Name = "Salon", Status = TenantStatus.PendingVerification, CreatedAt = DateTimeOffset.UtcNow },
            new Tenant { Id = Guid.NewGuid(), Name = "Other", Status = TenantStatus.PendingVerification, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();
        var result = await service.ApproveAsync(tenantId, "admin-1");

        Assert.True(result.IsSuccess);
        var publisher = provider.GetRequiredService<RecordingTenantRealtimePublisher>();
        Assert.Equal([1], publisher.AdminVerificationCounts);
        Assert.Equal([(tenantId, "verified")], publisher.TenantVerification);
    }

    [Fact]
    public async Task Reject_publishes_admin_pending_count_and_tenant_verification()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Salon", Status = TenantStatus.PendingVerification, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();
        var result = await service.RejectAsync(tenantId, "admin-1", "Documents are unreadable, please resubmit.");

        Assert.True(result.IsSuccess);
        var publisher = provider.GetRequiredService<RecordingTenantRealtimePublisher>();
        Assert.Equal([0], publisher.AdminVerificationCounts);
        Assert.Equal([(tenantId, "rejected")], publisher.TenantVerification);
    }

    [Fact]
    public async Task ListBusinesses_sortBy_status_orders_before_paging()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        db.Tenants.AddRange(
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Verified Co",
                Status = TenantStatus.Verified,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Pending Co",
                Status = TenantStatus.PendingVerification,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();
        var result = await service.ListBusinessesAsync(
            null,
            1,
            25,
            sortBy: "status",
            sortDirection: Application.Common.ListSortDirection.Asc);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.PendingVerification, result.Value!.Items[0].Status);
        Assert.Equal(TenantStatus.Verified, result.Value.Items[1].Status);
    }

    [Fact]
    public async Task Pending_submittedAt_uses_latest_document_time()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow.AddDays(-30);
        var docSubmitted = DateTimeOffset.UtcNow.AddDays(-2);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Pending Salon",
            Status = TenantStatus.PendingVerification,
            CreatedAt = created,
        });
        db.VerificationDocuments.Add(new VerificationDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentType = VerificationDocumentType.Cac,
            ReferenceNumber = "RC12345",
            SubmittedAt = docSubmitted,
        });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();
        var pending = await service.ListPendingVerificationsAsync(null, 1, 25);

        Assert.True(pending.IsSuccess);
        var row = Assert.Single(pending.Value!.Items);
        Assert.Equal(docSubmitted, row.SubmittedAt);
    }

    [Fact]
    public async Task Reject_requires_reason_min_length()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();

        var result = await service.RejectAsync(Guid.NewGuid(), "admin", "short");

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task SetCommercialTerms_requires_change_reason_when_commercial_fields_change()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Salon",
            Status = TenantStatus.Verified,
            SubscriptionSku = SubscriptionSku.Starter,
            BillingStatus = TenantBillingStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await service.SetTenantCommercialTermsAsync(
            tenantId,
            new SetTenantCommercialTermsRequest(SubscriptionSku: "professional"),
            "admin-1");

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<Application.Caching.ICacheService, Adeni.Infrastructure.Caching.DistributedCacheService>();
        services.AddSingleton<Application.Abstractions.ICorrelationContext, CorrelationContext>();
        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<Application.Abstractions.IAuditLogWriter, InMemoryAuditLogWriter>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IPricingCatalogService, PricingCatalogService>();
        services.AddScoped<ITenantPricingResolver, TenantPricingResolver>();
        services.AddSingleton<IFileStorage, FakeFileStorage>();
        services.AddSingleton<IVerificationLifecycleNotifier, NoopVerificationLifecycleNotifier>();
        services.AddSingleton<RecordingTenantRealtimePublisher>();
        services.AddSingleton<ITenantRealtimePublisher>(sp => sp.GetRequiredService<RecordingTenantRealtimePublisher>());
        services.AddScoped<AdminBusinessService>();
        return services.BuildServiceProvider();
    }
}
