namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Catalog;
using Adeni.Application.Markets;
using Adeni.Application.Storage;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Result = Adeni.Domain.Common.Result;

public sealed class BusinessOnboardingService(
    AdeniDbContext dbContext,
    ICategoryService categoryService,
    IFileStorage fileStorage) : IBusinessOnboardingService
{
    public async Task<Result<RegisterBusinessResponse>> RegisterAsync(
        RegisterBusinessRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRegistrationAsync(request, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<RegisterBusinessResponse>(validation.Error);
        }

        var normalizedSlug = LocationFieldValidator.NormalizeSlug(request.Location.Slug);
        if (await dbContext.BusinessLocations.AsNoTracking().AnyAsync(
                x => x.IsActive && x.Slug == normalizedSlug,
                cancellationToken))
        {
            return Result.Failure<RegisterBusinessResponse>(Error.Conflict("Business slug is already taken."));
        }

        var businessUser = await dbContext.BusinessUsers
            .Include(b => b.Tenant)
            .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub, cancellationToken);

        if (businessUser?.Tenant is not null && businessUser.Tenant.Status is TenantStatus.Verified or TenantStatus.PendingVerification)
        {
            return Result.Failure<RegisterBusinessResponse>(
                Error.Validation("Business is already registered or awaiting verification."));
        }

        var now = DateTimeOffset.UtcNow;

        if (businessUser is null)
        {
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = request.BusinessName.Trim(),
                Status = TenantStatus.Draft,
                CreatedAt = now
            };

            businessUser = new BusinessUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Auth0Sub = auth0Sub,
                Role = "owner",
                CreatedAt = now,
                Tenant = tenant
            };

            dbContext.Tenants.Add(tenant);
            dbContext.BusinessUsers.Add(businessUser);
        }
        else
        {
            businessUser.Tenant!.Name = request.BusinessName.Trim();
        }

        var profile = await dbContext.BusinessProfiles
            .FirstOrDefaultAsync(p => p.TenantId == businessUser.TenantId, cancellationToken);

        if (profile is null)
        {
            profile = new BusinessProfile { TenantId = businessUser.TenantId };
            dbContext.BusinessProfiles.Add(profile);
        }

        ApplyBrandProfile(profile, request, now);

        var location = await dbContext.BusinessLocations
            .FirstOrDefaultAsync(x => x.TenantId == businessUser.TenantId && x.IsPrimary, cancellationToken);

        if (location is null)
        {
            location = new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = businessUser.TenantId,
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
            };
            dbContext.BusinessLocations.Add(location);
        }

        ApplyLocation(location, request.Location, normalizedSlug, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new RegisterBusinessResponse(
            businessUser.TenantId,
            location.Slug,
            businessUser.Tenant!.Status));
    }

    public async Task<Result<BusinessContextResponse>> GetBusinessContextAsync(
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<BusinessContextResponse>(Error.Forbidden("Authentication is required."));
        }

        var businessUser = await dbContext.BusinessUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub, cancellationToken);

        if (businessUser is null)
        {
            return Result.Failure<BusinessContextResponse>(Error.NotFound("Business account"));
        }

        var row = await (
            from location in dbContext.BusinessLocations.AsNoTracking()
            join tenant in dbContext.Tenants.AsNoTracking() on location.TenantId equals tenant.Id
            where location.TenantId == businessUser.TenantId && location.IsActive
            orderby location.IsPrimary descending
            select new { location.Slug, tenant.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return Result.Failure<BusinessContextResponse>(Error.NotFound("Business location"));
        }

        return Result.Success(new BusinessContextResponse(
            businessUser.TenantId,
            row.Slug,
            row.Status));
    }

    public async Task<Result<BusinessProfileResponse>> GetProfileAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<BusinessProfileResponse>(access.Error);
        }

        var (tenant, profile) = access.Value!;
        var locations = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Result.Success(await MapProfileAsync(
            tenant,
            profile,
            locations,
            await GetDocumentsAsync(tenantId, cancellationToken),
            fileStorage,
            cancellationToken));
    }

    public async Task<Result<BusinessProfileResponse>> UpdateProfileAsync(
        Guid tenantId,
        UpdateBusinessProfileRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<BusinessProfileResponse>(access.Error);
        }

        var (tenant, profile) = access.Value!;

        if (tenant.Status is not TenantStatus.Draft and not TenantStatus.Rejected)
        {
            return Result.Failure<BusinessProfileResponse>(
                Error.Validation("Profile can only be edited while in draft or rejected status."));
        }

        var validation = await ValidateBrandFieldsAsync(
            request.BusinessName,
            request.CategorySlug,
            request.Phone,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<BusinessProfileResponse>(validation.Error);
        }

        tenant.Name = request.BusinessName.Trim();
        ApplyBrandProfile(profile, request, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        var locations = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Result.Success(await MapProfileAsync(
            tenant,
            profile,
            locations,
            await GetDocumentsAsync(tenantId, cancellationToken),
            fileStorage,
            cancellationToken));
    }

    public async Task<Result> SubmitVerificationAsync(
        Guid tenantId,
        SubmitVerificationRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var (tenant, _) = access.Value!;

        if (tenant.Status is not TenantStatus.Draft and not TenantStatus.Rejected)
        {
            return Result.Failure(Error.Validation("Verification can only be submitted from draft or rejected status."));
        }

        var hasActiveLocation = await dbContext.BusinessLocations
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.IsActive && x.Slug != string.Empty, cancellationToken);
        if (!hasActiveLocation)
        {
            return Result.Failure(Error.Validation("Complete business registration before submitting verification."));
        }

        if (request.Documents is null || request.Documents.Count == 0)
        {
            return Result.Failure(Error.Validation("At least one verification document is required."));
        }

        foreach (var document in request.Documents)
        {
            if (string.IsNullOrWhiteSpace(document.ReferenceNumber) || document.ReferenceNumber.Trim().Length < 5)
            {
                return Result.Failure(Error.Validation("Each document reference number must be at least 5 characters."));
            }
        }

        var existing = await dbContext.VerificationDocuments
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        dbContext.VerificationDocuments.RemoveRange(existing);

        var submittedAt = DateTimeOffset.UtcNow;
        foreach (var document in request.Documents)
        {
            dbContext.VerificationDocuments.Add(new VerificationDocument
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DocumentType = document.DocumentType,
                ReferenceNumber = document.ReferenceNumber.Trim(),
                SubmittedAt = submittedAt
            });
        }

        tenant.Status = TenantStatus.PendingVerification;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<(Tenant Tenant, BusinessProfile Profile)>> ResolveAccessAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<(Tenant, BusinessProfile)>(Error.Forbidden("Authentication is required."));
        }

        var businessUser = await dbContext.BusinessUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub, cancellationToken);

        if (businessUser is null || businessUser.TenantId != tenantId)
        {
            return Result.Failure<(Tenant, BusinessProfile)>(Error.Forbidden("You do not have access to this business."));
        }

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<(Tenant, BusinessProfile)>(Error.NotFound("Business"));
        }

        var profile = await dbContext.BusinessProfiles.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<(Tenant, BusinessProfile)>(Error.NotFound("Business profile"));
        }

        return Result.Success((tenant, profile));
    }

    private async Task<Result> ValidateRegistrationAsync(
        RegisterBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var brandValidation = await ValidateBrandFieldsAsync(
            request.BusinessName,
            request.CategorySlug,
            request.Phone,
            cancellationToken);
        if (brandValidation.IsFailure)
        {
            return brandValidation;
        }

        return LocationFieldValidator.Validate(
            request.Location.Slug,
            request.Location.AddressLine,
            request.Location.Area,
            request.Location.MarketId);
    }

    private async Task<Result> ValidateBrandFieldsAsync(
        string businessName,
        string categorySlug,
        string phone,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(businessName) || businessName.Trim().Length < 2)
        {
            return Result.Failure(Error.Validation("Business name must be at least 2 characters."));
        }

        if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length < 10)
        {
            return Result.Failure(Error.Validation("Phone number must be at least 10 characters."));
        }

        var categories = await categoryService.GetCategoriesAsync(cancellationToken);
        if (!categories.Any(c => c.Slug.Equals(categorySlug.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(Error.Validation("Category is not valid."));
        }

        return Result.Success();
    }

    private static void ApplyBrandProfile(
        BusinessProfile profile,
        RegisterBusinessRequest request,
        DateTimeOffset updatedAt)
    {
        profile.CategorySlug = request.CategorySlug.Trim().ToLowerInvariant();
        profile.Phone = request.Phone.Trim();
        profile.Description = request.Description?.Trim() ?? string.Empty;
        profile.UpdatedAt = updatedAt;
    }

    private static void ApplyBrandProfile(
        BusinessProfile profile,
        UpdateBusinessProfileRequest request,
        DateTimeOffset updatedAt)
    {
        profile.CategorySlug = request.CategorySlug.Trim().ToLowerInvariant();
        profile.Phone = request.Phone.Trim();
        profile.Description = request.Description?.Trim() ?? string.Empty;
        profile.UpdatedAt = updatedAt;
    }

    private static void ApplyLocation(
        BusinessLocation location,
        BusinessLocationRequest request,
        string normalizedSlug,
        DateTimeOffset updatedAt)
    {
        location.Slug = normalizedSlug;
        location.Name = string.IsNullOrWhiteSpace(request.Name) ? request.Area.Trim() : request.Name.Trim();
        location.MarketId = KnownMarketCatalog.Normalize(request.MarketId);
        location.AddressLine = request.AddressLine.Trim();
        location.Area = request.Area.Trim();
        location.Latitude = request.Latitude;
        location.Longitude = request.Longitude;
        location.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? null : request.TimeZoneId.Trim();
        location.UpdatedAt = updatedAt;
    }

    private async Task<IReadOnlyList<VerificationDocument>> GetDocumentsAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.VerificationDocuments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderBy(d => d.SubmittedAt)
            .ToListAsync(cancellationToken);

    private static async Task<BusinessProfileResponse> MapProfileAsync(
        Tenant tenant,
        BusinessProfile profile,
        IReadOnlyList<BusinessLocation> locations,
        IReadOnlyList<VerificationDocument> documents,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        string? coverImageUrl = null;
        if (!string.IsNullOrWhiteSpace(profile.CoverImageKey))
        {
            coverImageUrl = await fileStorage.GetDownloadUrlAsync(profile.CoverImageKey, cancellationToken);
        }

        return new BusinessProfileResponse(
            tenant.Id,
            tenant.Name,
            tenant.Status,
            profile.CategorySlug,
            profile.Phone,
            profile.Description,
            tenant.CreatedAt,
            tenant.VerifiedAt,
            BusinessLocationService.MapLocations(locations),
            documents.Select(d => new VerificationDocumentResponse(d.DocumentType, d.SubmittedAt)).ToList(),
            coverImageUrl);
    }
}
