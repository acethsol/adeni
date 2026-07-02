namespace Adeni.Infrastructure.Tests.TestData;

using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;

public static class BusinessTestSeed
{
    public static Guid SeedVerifiedBusiness(
        AdeniDbContext db,
        string slug,
        string name,
        string category,
        string area,
        double lat,
        double lng,
        string marketId = "lagos",
        string? timeZoneId = null,
        string? description = "Test")
    {
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = name,
            Status = TenantStatus.Verified,
            CreatedAt = now,
            VerifiedAt = now,
        });

        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = category,
            Phone = "+2348012345678",
            Description = description ?? string.Empty,
            UpdatedAt = now,
        });

        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            Name = area,
            MarketId = marketId,
            AddressLine = "Test address",
            Area = area,
            Latitude = lat,
            Longitude = lng,
            TimeZoneId = timeZoneId,
            IsPrimary = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });

        return tenantId;
    }

    public static void SeedDraftBusiness(
        AdeniDbContext db,
        string slug,
        string name,
        string category,
        string area,
        double lat,
        double lng,
        string marketId = "lagos")
    {
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = name,
            Status = TenantStatus.Draft,
            CreatedAt = now,
        });

        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = category,
            Phone = "+2348012345678",
            UpdatedAt = now,
        });

        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            Name = area,
            MarketId = marketId,
            AddressLine = "Test address",
            Area = area,
            Latitude = lat,
            Longitude = lng,
            IsPrimary = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }
}
