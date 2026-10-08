namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Storage;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class StaffService(
    AdeniDbContext dbContext,
    IFileStorage fileStorage,
    ICacheService cache) : IStaffService
{
    public async Task<IReadOnlyList<StaffMemberResponse>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var staff = await dbContext.StaffMembers
            .AsNoTracking()
            .Include(x => x.ServiceLinks)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        var responses = new List<StaffMemberResponse>(staff.Count);
        foreach (var member in staff)
        {
            responses.Add(await ToResponseAsync(member, cancellationToken));
        }

        return responses;
    }

    public async Task<Result<IReadOnlyList<StaffMemberResponse>>> ListPublicBySlugAsync(
        string slug,
        Guid? serviceOfferingId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<IReadOnlyList<StaffMemberResponse>>(
                Error.Validation("Business slug is required."));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var match = await VerifiedLocationQueries.ResolveLocationBySlugAsync(
            dbContext,
            normalizedSlug,
            cancellationToken);

        if (match is null)
        {
            return Result.Failure<IReadOnlyList<StaffMemberResponse>>(Error.NotFound("Business"));
        }

        var query = dbContext.StaffMembers
            .AsNoTracking()
            .Include(x => x.ServiceLinks)
            .Where(x => x.TenantId == match.Value.TenantId && x.IsActive);

        if (serviceOfferingId is Guid serviceId)
        {
            var serviceExists = await dbContext.ServiceOfferings
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == serviceId && x.TenantId == match.Value.TenantId && x.IsActive,
                    cancellationToken);

            if (!serviceExists)
            {
                return Result.Failure<IReadOnlyList<StaffMemberResponse>>(Error.NotFound("Service"));
            }

            query = query.Where(x =>
                !x.ServiceLinks.Any()
                || x.ServiceLinks.Any(link => link.ServiceOfferingId == serviceId));
        }

        var staff = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        var responses = new List<StaffMemberResponse>(staff.Count);
        foreach (var member in staff)
        {
            responses.Add(await ToResponseAsync(member, cancellationToken));
        }

        return Result.Success<IReadOnlyList<StaffMemberResponse>>(responses);
    }

    public async Task<Result<StaffMemberResponse>> CreateAsync(
        Guid tenantId,
        CreateStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateProfile(request.DisplayName, request.Title, request.Bio);
        if (validation.IsFailure)
        {
            return Result.Failure<StaffMemberResponse>(validation.Error);
        }

        if (!await dbContext.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return Result.Failure<StaffMemberResponse>(Error.NotFound("Tenant"));
        }

        var serviceIds = request.ServiceOfferingIds ?? Array.Empty<Guid>();
        var serviceCheck = await ValidateServiceIdsAsync(tenantId, serviceIds, cancellationToken);
        if (serviceCheck.IsFailure)
        {
            return Result.Failure<StaffMemberResponse>(serviceCheck.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new StaffMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DisplayName = request.DisplayName.Trim(),
            Title = NormalizeOptional(request.Title, 120),
            Bio = NormalizeOptional(request.Bio, 500),
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var serviceId in serviceIds.Distinct())
        {
            entity.ServiceLinks.Add(new StaffServiceLink
            {
                StaffMemberId = entity.Id,
                ServiceOfferingId = serviceId,
                TenantId = tenantId,
            });
        }

        dbContext.StaffMembers.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);

        return Result.Success(await ToResponseAsync(entity, cancellationToken));
    }

    public async Task<Result<StaffMemberResponse>> UpdateAsync(
        Guid tenantId,
        Guid staffMemberId,
        UpdateStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateProfile(request.DisplayName, request.Title, request.Bio);
        if (validation.IsFailure)
        {
            return Result.Failure<StaffMemberResponse>(validation.Error);
        }

        var entity = await dbContext.StaffMembers
            .Include(x => x.ServiceLinks)
            .FirstOrDefaultAsync(x => x.Id == staffMemberId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<StaffMemberResponse>(Error.NotFound("Staff member"));
        }

        entity.DisplayName = request.DisplayName.Trim();
        entity.Title = NormalizeOptional(request.Title, 120);
        entity.Bio = NormalizeOptional(request.Bio, 500);
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);

        return Result.Success(await ToResponseAsync(entity, cancellationToken));
    }

    public async Task<Result> DeactivateAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.Id == staffMemberId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.NotFound("Staff member"));
        }

        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<StaffMemberResponse>> ReplaceServicesAsync(
        Guid tenantId,
        Guid staffMemberId,
        IReadOnlyList<Guid> serviceOfferingIds,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StaffMembers
            .Include(x => x.ServiceLinks)
            .FirstOrDefaultAsync(x => x.Id == staffMemberId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<StaffMemberResponse>(Error.NotFound("Staff member"));
        }

        var ids = serviceOfferingIds ?? Array.Empty<Guid>();
        var serviceCheck = await ValidateServiceIdsAsync(tenantId, ids, cancellationToken);
        if (serviceCheck.IsFailure)
        {
            return Result.Failure<StaffMemberResponse>(serviceCheck.Error);
        }

        dbContext.StaffServiceLinks.RemoveRange(entity.ServiceLinks);
        entity.ServiceLinks.Clear();

        foreach (var serviceId in ids.Distinct())
        {
            entity.ServiceLinks.Add(new StaffServiceLink
            {
                StaffMemberId = entity.Id,
                ServiceOfferingId = serviceId,
                TenantId = tenantId,
            });
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);

        return Result.Success(await ToResponseAsync(entity, cancellationToken));
    }

    private async Task<Result> ValidateServiceIdsAsync(
        Guid tenantId,
        IReadOnlyList<Guid> serviceIds,
        CancellationToken cancellationToken)
    {
        if (serviceIds.Count == 0)
        {
            return Result.Success();
        }

        var distinct = serviceIds.Distinct().ToArray();
        var found = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && distinct.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (found.Count != distinct.Length)
        {
            return Result.Failure(Error.Validation("One or more services were not found for this business."));
        }

        return Result.Success();
    }

    private async Task InvalidatePublicStaffCacheAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var slugs = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);

        foreach (var slug in slugs)
        {
            await cache.RemoveAsync(CacheKeys.PublicStaff(slug), cancellationToken);
        }
    }

    private async Task<StaffMemberResponse> ToResponseAsync(
        StaffMember entity,
        CancellationToken cancellationToken)
    {
        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(entity.AvatarImageKey))
        {
            avatarUrl = await fileStorage.GetDownloadUrlAsync(entity.AvatarImageKey, cancellationToken);
        }

        return new StaffMemberResponse(
            entity.Id,
            entity.DisplayName,
            entity.Title,
            entity.Bio,
            entity.IsActive,
            entity.SortOrder,
            avatarUrl,
            entity.ServiceLinks.Select(x => x.ServiceOfferingId).ToArray());
    }

    private static Result ValidateProfile(string displayName, string? title, string? bio)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length < 2)
        {
            return Result.Failure(Error.Validation("Display name is required (at least 2 characters)."));
        }

        if (displayName.Trim().Length > 120)
        {
            return Result.Failure(Error.Validation("Display name must be at most 120 characters."));
        }

        if (title is { Length: > 120 })
        {
            return Result.Failure(Error.Validation("Title must be at most 120 characters."));
        }

        if (bio is { Length: > 500 })
        {
            return Result.Failure(Error.Validation("Bio must be at most 500 characters."));
        }

        return Result.Success();
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
