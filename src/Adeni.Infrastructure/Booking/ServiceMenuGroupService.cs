namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class ServiceMenuGroupService(AdeniDbContext dbContext) : IServiceMenuGroupService
{
    public async Task<IReadOnlyList<ServiceMenuGroupResponse>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ServiceMenuGroups
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return items.Select(ToResponse).ToArray();
    }

    public async Task<Result<ServiceMenuGroupResponse>> CreateAsync(
        Guid tenantId,
        CreateServiceMenuGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateName(request.Name);
        if (validation.IsFailure)
        {
            return Result.Failure<ServiceMenuGroupResponse>(validation.Error);
        }

        if (!await dbContext.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return Result.Failure<ServiceMenuGroupResponse>(Error.NotFound("Tenant"));
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ServiceMenuGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.ServiceMenuGroups.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(entity));
    }

    public async Task<Result<ServiceMenuGroupResponse>> UpdateAsync(
        Guid tenantId,
        Guid groupId,
        UpdateServiceMenuGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateName(request.Name);
        if (validation.IsFailure)
        {
            return Result.Failure<ServiceMenuGroupResponse>(validation.Error);
        }

        var entity = await dbContext.ServiceMenuGroups
            .FirstOrDefaultAsync(x => x.Id == groupId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<ServiceMenuGroupResponse>(Error.NotFound("Menu group"));
        }

        entity.Name = request.Name.Trim();
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(entity));
    }

    public async Task<Result> DeleteAsync(
        Guid tenantId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ServiceMenuGroups
            .FirstOrDefaultAsync(x => x.Id == groupId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.NotFound("Menu group"));
        }

        var offerings = await dbContext.ServiceOfferings
            .Where(x => x.TenantId == tenantId && x.MenuGroupId == groupId)
            .ToListAsync(cancellationToken);

        foreach (var offering in offerings)
        {
            offering.MenuGroupId = null;
            offering.UpdatedAt = DateTimeOffset.UtcNow;
        }

        dbContext.ServiceMenuGroups.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Result ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
        {
            return Result.Failure(Error.Validation("Collection name is required (at least 2 characters)."));
        }

        if (name.Trim().Length > 120)
        {
            return Result.Failure(Error.Validation("Collection name must be at most 120 characters."));
        }

        return Result.Success();
    }

    private static ServiceMenuGroupResponse ToResponse(ServiceMenuGroup entity) =>
        new(entity.Id, entity.Name, entity.SortOrder);
}
