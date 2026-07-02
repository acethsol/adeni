namespace Adeni.Infrastructure.Tenancy;

using System.Text.RegularExpressions;
using Adeni.Application.Catalog;
using Adeni.Application.Markets;
using Adeni.Application.Caching;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Result = Adeni.Domain.Common.Result;

internal static partial class LocationFieldValidator
{
    private static readonly Regex SlugPattern = SlugRegex();

    public static Result Validate(
        string slug,
        string addressLine,
        string area,
        string marketId)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (!SlugPattern.IsMatch(normalizedSlug))
        {
            return Result.Failure(Error.Validation("Slug must be 3-64 lowercase letters, numbers, or hyphens."));
        }

        if (!KnownMarketCatalog.IsValid(marketId))
        {
            return Result.Failure(Error.Validation("Market is not valid."));
        }

        if (string.IsNullOrWhiteSpace(addressLine) || addressLine.Trim().Length < 5)
        {
            return Result.Failure(Error.Validation("Address must be at least 5 characters."));
        }

        if (string.IsNullOrWhiteSpace(area) || area.Trim().Length < 2)
        {
            return Result.Failure(Error.Validation("Area is required."));
        }

        return Result.Success();
    }

    public static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,62}[a-z0-9])?$")]
    private static partial Regex SlugRegex();
}

public sealed class BusinessLocationService(
    AdeniDbContext dbContext,
    ICacheService cache) : IBusinessLocationService
{
    public async Task<Result<IReadOnlyList<BusinessLocationResponse>>> ListAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<BusinessLocationResponse>>(access.Error);
        }

        var locations = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Result.Success(MapLocations(locations));
    }

    public async Task<Result<BusinessLocationResponse>> AddAsync(
        Guid tenantId,
        UpsertBusinessLocationRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<BusinessLocationResponse>(access.Error);
        }

        var tenant = access.Value!;
        if (tenant.Status is not TenantStatus.Draft and not TenantStatus.Rejected and not TenantStatus.Verified)
        {
            return Result.Failure<BusinessLocationResponse>(
                Error.Validation("Locations cannot be added for this business status."));
        }

        var validation = LocationFieldValidator.Validate(
            request.Slug,
            request.AddressLine,
            request.Area,
            request.MarketId);
        if (validation.IsFailure)
        {
            return Result.Failure<BusinessLocationResponse>(validation.Error);
        }

        var normalizedSlug = LocationFieldValidator.NormalizeSlug(request.Slug);
        if (await SlugTakenAsync(normalizedSlug, null, cancellationToken))
        {
            return Result.Failure<BusinessLocationResponse>(Error.Conflict("Location slug is already taken."));
        }

        var now = DateTimeOffset.UtcNow;
        var makePrimary = request.IsPrimary == true
            || !await dbContext.BusinessLocations.AnyAsync(
                x => x.TenantId == tenantId && x.IsActive,
                cancellationToken);

        if (makePrimary)
        {
            await ClearPrimaryAsync(tenantId, cancellationToken);
        }

        var location = new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = normalizedSlug,
            Name = ResolveLocationName(request.Name, request.Area),
            MarketId = KnownMarketCatalog.Normalize(request.MarketId),
            AddressLine = request.AddressLine.Trim(),
            Area = request.Area.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? null : request.TimeZoneId.Trim(),
            IsPrimary = makePrimary,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.BusinessLocations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(MapLocation(location));
    }

    public async Task<Result<BusinessLocationResponse>> UpdateAsync(
        Guid tenantId,
        Guid locationId,
        UpsertBusinessLocationRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<BusinessLocationResponse>(access.Error);
        }

        var tenant = access.Value!;
        if (tenant.Status is not TenantStatus.Draft and not TenantStatus.Rejected and not TenantStatus.Verified)
        {
            return Result.Failure<BusinessLocationResponse>(
                Error.Validation("Locations cannot be edited for this business status."));
        }

        var location = await dbContext.BusinessLocations
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == locationId && x.IsActive, cancellationToken);
        if (location is null)
        {
            return Result.Failure<BusinessLocationResponse>(Error.NotFound("Location"));
        }

        var validation = LocationFieldValidator.Validate(
            request.Slug,
            request.AddressLine,
            request.Area,
            request.MarketId);
        if (validation.IsFailure)
        {
            return Result.Failure<BusinessLocationResponse>(validation.Error);
        }

        var normalizedSlug = LocationFieldValidator.NormalizeSlug(request.Slug);
        if (await SlugTakenAsync(normalizedSlug, locationId, cancellationToken))
        {
            return Result.Failure<BusinessLocationResponse>(Error.Conflict("Location slug is already taken."));
        }

        if (request.IsPrimary == true && !location.IsPrimary)
        {
            await ClearPrimaryAsync(tenantId, cancellationToken);
            location.IsPrimary = true;
        }

        location.Slug = normalizedSlug;
        location.Name = ResolveLocationName(request.Name, request.Area);
        location.MarketId = KnownMarketCatalog.Normalize(request.MarketId);
        location.AddressLine = request.AddressLine.Trim();
        location.Area = request.Area.Trim();
        location.Latitude = request.Latitude;
        location.Longitude = request.Longitude;
        location.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? null : request.TimeZoneId.Trim();
        location.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateLocationCacheAsync(normalizedSlug, cancellationToken);

        return Result.Success(MapLocation(location));
    }

    public async Task<Result> DeactivateAsync(
        Guid tenantId,
        Guid locationId,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var location = await dbContext.BusinessLocations
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == locationId && x.IsActive, cancellationToken);
        if (location is null)
        {
            return Result.Failure(Error.NotFound("Location"));
        }

        var activeCount = await dbContext.BusinessLocations
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);
        if (activeCount <= 1)
        {
            return Result.Failure(Error.Validation("At least one active location is required."));
        }

        location.IsActive = false;
        location.IsPrimary = false;
        location.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateLocationCacheAsync(location.Slug, cancellationToken);

        if (!await dbContext.BusinessLocations.AnyAsync(
                x => x.TenantId == tenantId && x.IsActive && x.IsPrimary,
                cancellationToken))
        {
            var replacement = await dbContext.BusinessLocations
                .Where(x => x.TenantId == tenantId && x.IsActive)
                .OrderBy(x => x.CreatedAt)
                .FirstAsync(cancellationToken);
            replacement.IsPrimary = true;
            replacement.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Result<Tenant>> ResolveAccessAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<Tenant>(Error.Forbidden("Authentication is required."));
        }

        var businessUser = await dbContext.BusinessUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub, cancellationToken);

        if (businessUser is null || businessUser.TenantId != tenantId)
        {
            return Result.Failure<Tenant>(Error.Forbidden("You do not have access to this business."));
        }

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<Tenant>(Error.NotFound("Business"));
        }

        return Result.Success(tenant);
    }

    private Task<bool> SlugTakenAsync(
        string normalizedSlug,
        Guid? excludeLocationId,
        CancellationToken cancellationToken) =>
        dbContext.BusinessLocations
            .AsNoTracking()
            .AnyAsync(
                x => x.IsActive
                     && x.Slug == normalizedSlug
                     && (excludeLocationId == null || x.Id != excludeLocationId),
                cancellationToken);

    private async Task ClearPrimaryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var primaries = await dbContext.BusinessLocations
            .Where(x => x.TenantId == tenantId && x.IsPrimary && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var primary in primaries)
        {
            primary.IsPrimary = false;
            primary.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string ResolveLocationName(string? name, string area)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        return area.Trim();
    }

    private static BusinessLocationResponse MapLocation(BusinessLocation location) =>
        new(
            location.Id,
            location.Slug,
            location.Name,
            location.AddressLine,
            location.Area,
            location.MarketId,
            location.Latitude,
            location.Longitude,
            location.TimeZoneId,
            location.IsPrimary,
            location.IsActive);

    internal static IReadOnlyList<BusinessLocationResponse> MapLocations(
        IReadOnlyList<BusinessLocation> locations) =>
        locations.Select(MapLocation).ToList();

    private Task InvalidateLocationCacheAsync(string slug, CancellationToken cancellationToken) =>
        cache.RemoveAsync(CacheKeys.LocationProfile(slug), cancellationToken);
}
