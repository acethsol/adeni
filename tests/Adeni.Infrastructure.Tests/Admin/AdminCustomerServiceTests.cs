namespace Adeni.Infrastructure.Tests.Admin;

using Adeni.Application.Abstractions;
using Adeni.Application.Admin;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Admin;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class AdminCustomerServiceTests
{
    [Fact]
    public async Task Export_and_erasure_work_for_customer()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IAdminCustomerService>();

        var customerId = Guid.NewGuid();
        db.Customers.Add(new Customer
        {
            Id = customerId,
            Auth0Sub = "auth0|privacy-test",
            Name = "Privacy Test",
            Email = "privacy@example.com",
            Phone = "+2348012345678",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var exported = await service.ExportAsync(customerId, "admin-1", CancellationToken.None);
        Assert.True(exported.IsSuccess);
        Assert.Equal("privacy@example.com", exported.Value!.Email);

        var erasure = await service.InitiateErasureAsync(customerId, "admin-1", CancellationToken.None);
        Assert.True(erasure.IsSuccess);

        var updated = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customerId);
        Assert.Equal("[erased]", updated.Name);
        Assert.Null(updated.Email);
        Assert.NotNull(updated.ErasureRequestedAt);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICorrelationContext, CorrelationContext>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Adeni.Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IAuditLogWriter, EfAuditLogWriter>();
        services.AddScoped<IAdminCustomerService, AdminCustomerService>();
        return services.BuildServiceProvider();
    }
}
