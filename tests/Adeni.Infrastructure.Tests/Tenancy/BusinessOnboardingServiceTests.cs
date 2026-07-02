namespace Adeni.Infrastructure.Tests.Tenancy;

using Adeni.Application.Tenancy;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Admin;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Catalog;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

public sealed class BusinessOnboardingServiceTests
{
    private static readonly BusinessLocationRequest ValidLocation = new(
        "lekki-cuts",
        "Lekki",
        "12 Admiralty Way",
        "Lekki",
        "lagos",
        6.4474,
        3.4700);

    private static readonly RegisterBusinessRequest ValidRequest = new(
        "Lekki Cuts",
        "barbers",
        "+2348012345678",
        ValidLocation,
        "Premium barber shop");

    [Fact]
    public async Task Register_creates_tenant_profile_and_returns_draft()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BusinessOnboardingService>();

        var result = await service.RegisterAsync(ValidRequest, "auth0|owner-1");

        Assert.True(result.IsSuccess);
        Assert.Equal("lekki-cuts", result.Value!.Slug);
        Assert.Equal(TenantStatus.Draft, result.Value.Status);

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        Assert.Equal(1, await db.Tenants.CountAsync());
        Assert.Equal(1, await db.BusinessProfiles.CountAsync());
        Assert.Equal(1, await db.BusinessLocations.CountAsync());
    }

    [Fact]
    public async Task SubmitVerification_moves_tenant_to_pending()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BusinessOnboardingService>();
        const string auth0Sub = "auth0|owner-1";

        var registered = await service.RegisterAsync(ValidRequest, auth0Sub);
        var tenantId = registered.Value!.TenantId;

        var result = await service.SubmitVerificationAsync(
            tenantId,
            new SubmitVerificationRequest(
            [
                new(VerificationDocumentType.Cac, "RC123456"),
                new(VerificationDocumentType.NationalId, "A12345678")
            ]),
            auth0Sub);

        Assert.True(result.IsSuccess);

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenant = await db.Tenants.FirstAsync(t => t.Id == tenantId);
        Assert.Equal(TenantStatus.PendingVerification, tenant.Status);
        Assert.Equal(2, await db.VerificationDocuments.CountAsync());
    }

    [Fact]
    public async Task Full_flow_register_submit_admin_approve()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var onboarding = scope.ServiceProvider.GetRequiredService<BusinessOnboardingService>();
        var admin = scope.ServiceProvider.GetRequiredService<AdminBusinessService>();
        const string auth0Sub = "auth0|owner-1";

        var tenantId = (await onboarding.RegisterAsync(ValidRequest, auth0Sub)).Value!.TenantId;
        await onboarding.SubmitVerificationAsync(
            tenantId,
            new SubmitVerificationRequest([new(VerificationDocumentType.Cac, "RC123456")]),
            auth0Sub);

        var pending = await admin.GetPendingVerificationsAsync();
        Assert.Single(pending);
        Assert.Equal("lekki-cuts", pending[0].Slug);

        var approved = await admin.ApproveAsync(tenantId, "admin-1");
        Assert.True(approved.IsSuccess);

        var profile = await onboarding.GetProfileAsync(tenantId, auth0Sub);
        Assert.True(profile.IsSuccess);
        Assert.Equal(TenantStatus.Verified, profile.Value!.Status);
        Assert.NotNull(profile.Value.VerifiedAt);
        Assert.Single(profile.Value.Locations);
    }

    [Fact]
    public async Task Register_rejects_duplicate_slug()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BusinessOnboardingService>();

        await service.RegisterAsync(ValidRequest, "auth0|owner-1");
        var second = await service.RegisterAsync(ValidRequest, "auth0|owner-2");

        Assert.True(second.IsFailure);
        Assert.Equal("conflict", second.Error.Code);
    }

    [Fact]
    public async Task Register_rejects_invalid_market()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BusinessOnboardingService>();

        var result = await service.RegisterAsync(
            ValidRequest with
            {
                Location = ValidLocation with { MarketId = "invalid-city" }
            },
            "auth0|owner-bad-market");

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task Register_accepts_non_beauty_category()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BusinessOnboardingService>();

        var result = await service.RegisterAsync(
            ValidRequest with
            {
                BusinessName = "Lekki Plumbing",
                CategorySlug = "plumbers",
                Location = ValidLocation with { Slug = "lekki-plumbing" }
            },
            "auth0|owner-plumber");

        Assert.True(result.IsSuccess);

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var profile = await db.BusinessProfiles.FirstAsync();
        Assert.Equal("plumbers", profile.CategorySlug);
        Assert.Equal("lekki-plumbing", await db.BusinessLocations.Select(x => x.Slug).FirstAsync());
    }

    [Fact]
    public async Task Register_rejects_unknown_category()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BusinessOnboardingService>();

        var result = await service.RegisterAsync(
            ValidRequest with { CategorySlug = "unknown-vertical" },
            "auth0|owner-1");

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<Application.Caching.ICacheService, DistributedCacheService>();
        services.AddSingleton<Application.Catalog.ICategoryService, CategoryService>();
        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<Application.Abstractions.ICorrelationContext, CorrelationContext>();
        services.AddSingleton<Application.Abstractions.IAuditLogWriter, InMemoryAuditLogWriter>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<BusinessOnboardingService>();
        services.AddScoped<AdminBusinessService>();
        return services.BuildServiceProvider();
    }
}
