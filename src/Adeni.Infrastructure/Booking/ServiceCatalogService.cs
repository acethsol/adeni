namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Catalog;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class ServiceCatalogService(
    AdeniDbContext dbContext,
    ICategoryService categoryService) : IServiceCatalogService
{
    public async Task<ServiceCatalogListResponse> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var groups = await dbContext.ServiceMenuGroups
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var groupOrder = groups.ToDictionary(g => g.Id, g => g.SortOrder);

        var items = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync(cancellationToken);

        var ordered = items
            .OrderBy(x => x.MenuGroupId is { } gid && groupOrder.TryGetValue(gid, out var go) ? go : int.MaxValue)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(ServiceOfferingMapper.ToResponse)
            .ToArray();

        return new ServiceCatalogListResponse(
            ordered,
            groups.Select(g => new ServiceMenuGroupResponse(g.Id, g.Name, g.SortOrder)).ToArray());
    }

    public async Task<ServiceCatalogListResponse> ListPublicBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return new ServiceCatalogListResponse([], []);
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var tenantId = await VerifiedLocationQueries.ResolveTenantIdBySlugAsync(
            dbContext,
            normalizedSlug,
            cancellationToken);

        if (tenantId == Guid.Empty)
        {
            return new ServiceCatalogListResponse([], []);
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

        var menuGroupValidation = await ValidateMenuGroupAsync(tenantId, request.MenuGroupId, cancellationToken);
        if (menuGroupValidation.IsFailure)
        {
            return Result.Failure<ServiceOfferingResponse>(menuGroupValidation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var pricingType = PricingTypeMapping.FromApiValue(request.PricingType);
        var pricingValidation = await ValidatePricingTypeAsync(tenantId, pricingType, cancellationToken);
        if (pricingValidation.IsFailure)
        {
            return Result.Failure<ServiceOfferingResponse>(pricingValidation.Error);
        }

        var entity = new ServiceOffering
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PriceAmount = request.PriceAmount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            PricingType = pricingType,
            DurationMinutes = request.DurationMinutes,
            CategorySlug = NormalizeOptionalCategorySlug(request.CategorySlug),
            CatalogServiceId = NormalizeOptionalId(request.CatalogServiceId),
            BookingDeliveryType = BookingDeliveryTypeMapping.FromApiValue(request.BookingDeliveryType),
            MenuGroupId = request.MenuGroupId,
            SortOrder = request.SortOrder,
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

        var menuGroupValidation = await ValidateMenuGroupAsync(tenantId, request.MenuGroupId, cancellationToken);
        if (menuGroupValidation.IsFailure)
        {
            return Result.Failure<ServiceOfferingResponse>(menuGroupValidation.Error);
        }

        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.PriceAmount = request.PriceAmount;
        entity.Currency = request.Currency.Trim().ToUpperInvariant();
        var pricingType = PricingTypeMapping.FromApiValue(request.PricingType);
        var pricingValidation = await ValidatePricingTypeAsync(tenantId, pricingType, cancellationToken);
        if (pricingValidation.IsFailure)
        {
            return Result.Failure<ServiceOfferingResponse>(pricingValidation.Error);
        }

        entity.PricingType = pricingType;
        entity.DurationMinutes = request.DurationMinutes;
        entity.CategorySlug = NormalizeOptionalCategorySlug(request.CategorySlug);
        entity.CatalogServiceId = NormalizeOptionalId(request.CatalogServiceId);
        entity.BookingDeliveryType = BookingDeliveryTypeMapping.FromApiValue(request.BookingDeliveryType);
        entity.MenuGroupId = request.MenuGroupId;
        entity.SortOrder = request.SortOrder;
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

    private async Task<Result> ValidateMenuGroupAsync(
        Guid tenantId,
        Guid? menuGroupId,
        CancellationToken cancellationToken)
    {
        if (menuGroupId is null)
        {
            return Result.Success();
        }

        var exists = await dbContext.ServiceMenuGroups
            .AsNoTracking()
            .AnyAsync(x => x.Id == menuGroupId && x.TenantId == tenantId, cancellationToken);

        return exists
            ? Result.Success()
            : Result.Failure(Error.Validation("Menu collection was not found for this business."));
    }

    private static Result ValidateCreate(CreateServiceOfferingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2)
        {
            return Result.Failure(Error.Validation("Service name is required."));
        }

        var pricingType = PricingTypeMapping.FromApiValue(request.PricingType);
        if (pricingType != PricingType.QuoteRequest && request.PriceAmount < 0)
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

    private async Task<Result> ValidatePricingTypeAsync(
        Guid tenantId,
        PricingType pricingType,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(Error.NotFound("Business profile"));
        }

        if (pricingType == PricingType.QuoteRequest && profile.BusinessType != BusinessType.QuoteRequest)
        {
            return Result.Failure(Error.Validation("Quote-request pricing is only available for quote-type businesses."));
        }

        if (pricingType == PricingType.Hourly && profile.BusinessType == BusinessType.QuoteRequest)
        {
            return Result.Failure(Error.Validation("Hourly pricing is not available for quote-type businesses."));
        }

        return Result.Success();
    }

    private static Result ValidateUpdate(UpdateServiceOfferingRequest request) =>
        ValidateCreate(new CreateServiceOfferingRequest(
            request.Name,
            request.Description,
            request.PriceAmount,
            request.Currency,
            request.DurationMinutes,
            request.PricingType,
            request.CategorySlug,
            request.CatalogServiceId,
            request.BookingDeliveryType,
            request.MenuGroupId,
            request.SortOrder));

    private string? NormalizeOptionalCategorySlug(string? slug) =>
        string.IsNullOrWhiteSpace(slug) ? null : categoryService.NormalizeSlug(slug);

    private static string? NormalizeOptionalId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : id.Trim();
}
