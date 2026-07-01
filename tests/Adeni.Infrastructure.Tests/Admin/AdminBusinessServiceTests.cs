namespace Adeni.Infrastructure.Tests.Admin;

using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Admin;
using Adeni.Infrastructure.Auditing;
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
        var pending = await service.GetPendingVerificationsAsync();

        Assert.Single(pending);
        Assert.Equal("Pending", pending[0].Name);
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
    public async Task Reject_requires_reason_min_length()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();

        var result = await service.RejectAsync(Guid.NewGuid(), "admin", "short");

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Application.Abstractions.ICorrelationContext, CorrelationContext>();
        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<Application.Abstractions.IAuditLogWriter, InMemoryAuditLogWriter>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<AdminBusinessService>();
        return services.BuildServiceProvider();
    }
}
