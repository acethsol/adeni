namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

public sealed class ServiceCatalogService(AdeniDbContext dbContext) : IServiceCatalogService
{
    public async Task<IReadOnlyList<ServiceOfferingResponse>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return items.Select(ServiceOfferingMapper.ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<ServiceOfferingResponse>> ListPublicBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return [];
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var tenantId = await VerifiedLocationQueries.ResolveTenantIdBySlugAsync(
            dbContext,
            normalizedSlug,
            cancellationToken);

        if (tenantId == Guid.Empty)
        {
            return [];
        }

        return await ListForTenantAsync(tenantId, cancellationToken);
    }

    public async Task<Result<ServiceOfferingResponse>> CreateAsync(
        Guid tenantId,
        CreateServiceOfferingRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreate(request);
        if (validation.IsFailure)
        {
            return Result.Failure<ServiceOfferingResponse>(validation.Error);
        }

        if (!await TenantExistsAsync(tenantId, cancellationToken))
        {
            return Result.Failure<ServiceOfferingResponse>(Error.NotFound("Tenant"));
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ServiceOffering
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PriceAmount = request.PriceAmount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            DurationMinutes = request.DurationMinutes,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ServiceOfferings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ServiceOfferingMapper.ToResponse(entity));
    }

    public async Task<Result<ServiceOfferingResponse>> UpdateAsync(
        Guid tenantId,
        Guid serviceId,
        UpdateServiceOfferingRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateUpdate(request);
        if (validation.IsFailure)
        {
            return Result.Failure<ServiceOfferingResponse>(validation.Error);
        }

        var entity = await dbContext.ServiceOfferings
            .FirstOrDefaultAsync(x => x.Id == serviceId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<ServiceOfferingResponse>(Error.NotFound("Service"));
        }

        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.PriceAmount = request.PriceAmount;
        entity.Currency = request.Currency.Trim().ToUpperInvariant();
        entity.DurationMinutes = request.DurationMinutes;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ServiceOfferingMapper.ToResponse(entity));
    }

    public async Task<Result> DeactivateAsync(
        Guid tenantId,
        Guid serviceId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ServiceOfferings
            .FirstOrDefaultAsync(x => x.Id == serviceId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.NotFound("Service"));
        }

        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken);

    private static Result ValidateCreate(CreateServiceOfferingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2)
        {
            return Result.Failure(Error.Validation("Service name is required."));
        }

        if (request.PriceAmount < 0)
        {
            return Result.Failure(Error.Validation("Price must be zero or greater."));
        }

        if (request.DurationMinutes is < 5 or > 480)
        {
            return Result.Failure(Error.Validation("Duration must be between 5 and 480 minutes."));
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
        {
            return Result.Failure(Error.Validation("Currency must be a 3-letter code."));
        }

        return Result.Success();
    }

    private static Result ValidateUpdate(UpdateServiceOfferingRequest request) =>
        ValidateCreate(new CreateServiceOfferingRequest(
            request.Name,
            request.Description,
            request.PriceAmount,
            request.Currency,
            request.DurationMinutes));
}
