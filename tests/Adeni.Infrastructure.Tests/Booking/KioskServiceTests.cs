namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tests.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class KioskServiceTests
{
    [Fact]
    public void HashPin_is_stable_and_tenant_scoped()
    {
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var hashA1 = KioskService.HashPin(tenantA, "1234");
        var hashA2 = KioskService.HashPin(tenantA, "1234");
        var hashB = KioskService.HashPin(tenantB, "1234");
        var hashOther = KioskService.HashPin(tenantA, "5678");

        Assert.Equal(hashA1, hashA2);
        Assert.NotEqual(hashA1, hashB);
        Assert.NotEqual(hashA1, hashOther);
        Assert.Equal(64, hashA1.Length);
    }

    [Fact]
    public async Task Set_and_verify_pin_round_trip()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var kiosk = scope.ServiceProvider.GetRequiredService<IKioskService>();

        var tenantId = await SeedBarberAsync(db);

        var status = await kiosk.GetStatusAsync(tenantId);
        Assert.True(status.IsSuccess, status.Error?.Message);
        Assert.False(status.Value!.HasPin);
        Assert.True(status.Value.QueueEnabled);
        Assert.False(string.IsNullOrWhiteSpace(status.Value.Slug));

        var set = await kiosk.SetPinAsync(tenantId, new SetKioskPinRequest("4321"));
        Assert.True(set.IsSuccess, set.Error?.Message);
        Assert.True(set.Value!.HasPin);

        var wrong = await kiosk.VerifyPinAsync(tenantId, new VerifyKioskPinRequest("9999"));
        Assert.True(wrong.IsSuccess, wrong.Error?.Message);
        Assert.False(wrong.Value!.Ok);

        var right = await kiosk.VerifyPinAsync(tenantId, new VerifyKioskPinRequest("4321"));
        Assert.True(right.IsSuccess, right.Error?.Message);
        Assert.True(right.Value!.Ok);

        var cleared = await kiosk.SetPinAsync(tenantId, new SetKioskPinRequest(null));
        Assert.True(cleared.IsSuccess, cleared.Error?.Message);
        Assert.False(cleared.Value!.HasPin);

        var open = await kiosk.VerifyPinAsync(tenantId, new VerifyKioskPinRequest("anything"));
        Assert.True(open.IsSuccess, open.Error?.Message);
        Assert.True(open.Value!.Ok);
    }

    [Fact]
    public async Task Set_pin_rejects_non_queue_categories()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var kiosk = scope.ServiceProvider.GetRequiredService<IKioskService>();

        var tenantId = await SeedBarberAsync(db, categorySlug: "massage-bodywork");
        var set = await kiosk.SetPinAsync(tenantId, new SetKioskPinRequest("1234"));

        Assert.True(set.IsFailure);
        Assert.Contains("not enabled", set.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Set_pin_rejects_invalid_format()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var kiosk = scope.ServiceProvider.GetRequiredService<IKioskService>();

        var tenantId = await SeedBarberAsync(db);
        var set = await kiosk.SetPinAsync(tenantId, new SetKioskPinRequest("12ab"));

        Assert.True(set.IsFailure);
        Assert.Contains("4–8", set.Error.Message, StringComparison.Ordinal);
    }

    private static async Task<Guid> SeedBarberAsync(
        AdeniDbContext db,
        string categorySlug = "barbers")
    {
        var tenantId = Guid.NewGuid();
        var slug = $"kiosk-{tenantId:N}"[..20];
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Kiosk Cut",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = categorySlug,
            Phone = "+2348000000000",
            Description = "Kiosk tests",
            BusinessType = BusinessType.ScheduledAppointment,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            Name = "Main",
            MarketId = "lagos",
            AddressLine = "1 Cut Rd",
            Area = "Ikeja",
            IsPrimary = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCategoryWorkflowCatalog();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IKioskService, KioskService>();
        return services.BuildServiceProvider();
    }
}
