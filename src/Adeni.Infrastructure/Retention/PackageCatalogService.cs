namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PackageCatalogService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IRetentionServiceLookup services,
    TimeProvider clock,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null) : IPackageCatalogService
{
    private static readonly HashSet<string> Currencies = ["NGN", "CAD", "USD"];
    private const int MaxBenefitLines = 10;

    public async Task<Result<PackageDefinitionListResponse>> ListAsync(
        Guid tenantId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = db.Set<PackageDefinition>().AsNoTracking()
            .Include(x => x.Benefits)
            .Where(x => x.TenantId == tenantId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var rows = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Result.Success(new PackageDefinitionListResponse(rows.Select(RetentionMapper.ToResponse).ToArray()));
    }

    public async Task<Result<PackageDefinitionResponse>> GetAsync(
        Guid tenantId, Guid packageDefinitionId, CancellationToken cancellationToken = default)
    {
        var row = await db.Set<PackageDefinition>().AsNoTracking()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == packageDefinitionId, cancellationToken);
        return row is null
            ? Result.Failure<PackageDefinitionResponse>(RetentionErrorCodes.PackageNotFoundError())
            : Result.Success(RetentionMapper.ToResponse(row));
    }

    public async Task<Result<PackageDefinitionResponse>> CreateAsync(
        Guid tenantId, RetentionActor actor, CreatePackageDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsurePackagesEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<PackageDefinitionResponse>(entitled.Error);
        }

        var now = clock.GetUtcNow();
        var definition = new PackageDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var applied = await ApplyAsync(
            definition,
            request.Name,
            request.Description,
            request.ListPrice,
            request.Currency,
            request.DefaultExpiryMonths,
            request.RedemptionScope,
            request.SellScope,
            request.RedemptionLocationIds,
            request.SellLocationIds,
            request.Benefits,
            cancellationToken);
        if (applied.IsFailure)
        {
            return Result.Failure<PackageDefinitionResponse>(applied.Error);
        }

        db.Add(definition);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(actor, AuditActions.RetentionPackageCreated, definition.Id, tenantId, cancellationToken);
        return Result.Success(RetentionMapper.ToResponse(definition));
    }

    public async Task<Result<PackageDefinitionResponse>> UpdateAsync(
        Guid tenantId, RetentionActor actor, Guid packageDefinitionId, UpdatePackageDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsurePackagesEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<PackageDefinitionResponse>(entitled.Error);
        }

        var definition = await db.Set<PackageDefinition>()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == packageDefinitionId, cancellationToken);
        if (definition is null)
        {
            return Result.Failure<PackageDefinitionResponse>(RetentionErrorCodes.PackageNotFoundError());
        }

        // PATCH semantics: null keeps the current value. Sold instances snapshot their own fields.
        var applied = await ApplyAsync(
            definition,
            request.Name ?? definition.Name,
            request.Description ?? definition.Description,
            request.ListPrice ?? definition.ListPrice,
            request.Currency ?? definition.Currency,
            request.DefaultExpiryMonths ?? definition.DefaultExpiryMonths,
            request.RedemptionScope ?? RetentionScopeCodes.ToApi(definition.RedemptionScope),
            request.SellScope ?? RetentionScopeCodes.ToApi(definition.SellScope),
            request.RedemptionLocationIds ?? LocationIdList.Parse(definition.RedemptionLocationIdsJson),
            request.SellLocationIds ?? LocationIdList.Parse(definition.SellLocationIdsJson),
            request.Benefits?.ToArray(),
            cancellationToken);
        if (applied.IsFailure)
        {
            return Result.Failure<PackageDefinitionResponse>(applied.Error);
        }

        definition.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(actor, AuditActions.RetentionPackageUpdated, definition.Id, tenantId, cancellationToken);
        return Result.Success(RetentionMapper.ToResponse(definition));
    }

    public async Task<Result<PackageDefinitionResponse>> DeactivateAsync(
        Guid tenantId, RetentionActor actor, Guid packageDefinitionId, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsurePackagesEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<PackageDefinitionResponse>(entitled.Error);
        }

        var definition = await db.Set<PackageDefinition>()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == packageDefinitionId, cancellationToken);
        if (definition is null)
        {
            return Result.Failure<PackageDefinitionResponse>(RetentionErrorCodes.PackageNotFoundError());
        }

        if (definition.IsActive)
        {
            var now = clock.GetUtcNow();
            definition.IsActive = false;
            definition.DeactivatedAt = now;
            definition.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await AuditAsync(actor, AuditActions.RetentionPackageDeactivated, definition.Id, tenantId, cancellationToken);
        }

        return Result.Success(RetentionMapper.ToResponse(definition));
    }

    private async Task<Result> ApplyAsync(
        PackageDefinition definition,
        string? name,
        string? description,
        decimal listPrice,
        string? currency,
        int? expiryMonths,
        string? redemptionScope,
        string? sellScope,
        IReadOnlyList<Guid>? redemptionLocationIds,
        IReadOnlyList<Guid>? sellLocationIds,
        IReadOnlyList<PackageBenefitInput>? benefits,
        CancellationToken cancellationToken)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > 200)
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Package name must be 1–200 characters."));
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is { Length: > 1000 })
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Description must be at most 1000 characters."));
        }

        if (listPrice < 0 || listPrice > 9_999_999_999.99m || decimal.Round(listPrice, 2) != listPrice)
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("List price must be a non-negative amount with at most 2 decimals."));
        }

        var iso = currency?.Trim().ToUpperInvariant();
        if (iso is null || !Currencies.Contains(iso))
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Unsupported currency."));
        }

        if (expiryMonths is < 1 or > 120)
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Expiry must be between 1 and 120 months."));
        }

        if (!RetentionScopeCodes.TryParseRedemption(redemptionScope, out var redemption)
            || !RetentionScopeCodes.TryParseSell(sellScope, out var sell))
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Unknown redemption or sell scope."));
        }

        var redemptionIds = redemption == PackageRedemptionScope.SelectedLocations
            ? (redemptionLocationIds ?? []).Distinct().ToArray()
            : [];
        var sellIds = sell == PackageSellScope.SelectedLocations
            ? (sellLocationIds ?? []).Distinct().ToArray()
            : [];
        if ((redemption == PackageRedemptionScope.SelectedLocations && redemptionIds.Length == 0)
            || (sell == PackageSellScope.SelectedLocations && sellIds.Length == 0))
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Choose at least one location for a selected-locations scope."));
        }

        var allIds = redemptionIds.Concat(sellIds).Distinct().ToArray();
        if (allIds.Length > 0)
        {
            var valid = await db.BusinessLocations.AsNoTracking()
                .CountAsync(x => x.TenantId == definition.TenantId && x.IsActive && allIds.Contains(x.Id), cancellationToken);
            if (valid != allIds.Length)
            {
                return Result.Failure(RetentionErrorCodes.ValidationError("Unknown or inactive location for this business."));
            }
        }

        if (benefits is not null)
        {
            var lines = benefits.ToArray();
            if (lines.Length is < 1 or > MaxBenefitLines
                || lines.Any(x => x.QuantityGranted is < 1 or > 1000)
                || lines.Select(x => x.ServiceOfferingId).Distinct().Count() != lines.Length)
            {
                return Result.Failure(RetentionErrorCodes.ValidationError(
                    $"A package needs 1–{MaxBenefitLines} distinct services with quantities of 1–1000."));
            }

            var found = await services.FindAsync(definition.TenantId, lines.Select(x => x.ServiceOfferingId).ToArray(), cancellationToken);
            var byId = found.ToDictionary(x => x.Id);
            if (lines.Any(x => !byId.TryGetValue(x.ServiceOfferingId, out var svc) || !svc.IsActive))
            {
                return Result.Failure(RetentionErrorCodes.ValidationError("Every package service must be an active service of this business."));
            }

            // Reconcile by service (unique per package) instead of delete + insert.
            var existingByService = definition.Benefits.ToDictionary(x => x.ServiceOfferingId);
            foreach (var stale in existingByService.Values.Where(x => lines.All(l => l.ServiceOfferingId != x.ServiceOfferingId)))
            {
                db.Remove(stale);
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var serviceName = byId[lines[i].ServiceOfferingId].Name;
                if (existingByService.TryGetValue(lines[i].ServiceOfferingId, out var current))
                {
                    current.QuantityGranted = lines[i].QuantityGranted;
                    current.ServiceName = serviceName;
                    current.SortOrder = i;
                    continue;
                }

                definition.Benefits.Add(new PackageBenefit
                {
                    Id = Guid.NewGuid(),
                    TenantId = definition.TenantId,
                    PackageDefinitionId = definition.Id,
                    ServiceOfferingId = lines[i].ServiceOfferingId,
                    ServiceName = serviceName,
                    QuantityGranted = lines[i].QuantityGranted,
                    SortOrder = i,
                });
            }
        }
        else if (definition.Benefits.Count == 0)
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("A package needs at least one service."));
        }

        definition.Name = trimmedName;
        definition.Description = trimmedDescription;
        definition.ListPrice = listPrice;
        definition.Currency = iso;
        definition.DefaultExpiryMonths = expiryMonths;
        definition.RedemptionScope = redemption;
        definition.SellScope = sell;
        definition.RedemptionLocationIdsJson = LocationIdList.Serialize(redemptionIds);
        definition.SellLocationIdsJson = LocationIdList.Serialize(sellIds);
        return Result.Success();
    }

    private Task AuditAsync(RetentionActor actor, string action, Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actor.Id,
                action,
                "retention.package_definition",
                id.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\"}}"),
            cancellationToken) ?? Task.CompletedTask;
}