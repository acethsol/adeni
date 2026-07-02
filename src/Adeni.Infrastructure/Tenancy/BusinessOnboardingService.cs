namespace Adeni.Infrastructure.Tenancy;

using System.Text.RegularExpressions;
using Adeni.Application.Catalog;
using Adeni.Application.Tenancy;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Result = Adeni.Domain.Common.Result;

public sealed partial class BusinessOnboardingService(
    AdeniDbContext dbContext,
    ICategoryService categoryService) : IBusinessOnboardingService
{
    private static readonly Regex SlugPattern = SlugRegex();

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

        var normalizedSlug = NormalizeSlug(request.Slug);
        var slugTaken = await dbContext.BusinessProfiles
            .AsNoTracking()
            .AnyAsync(p => p.Slug == normalizedSlug, cancellationToken);
        if (slugTaken)
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

        ApplyProfile(profile, request, normalizedSlug, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new RegisterBusinessResponse(
            businessUser.TenantId,
            profile.Slug,
            businessUser.Tenant!.Status));
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
        return Result.Success(MapProfile(tenant, profile, await GetDocumentsAsync(tenantId, cancellationToken)));
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

        var validation = await ValidateProfileFieldsAsync(
            request.BusinessName,
            request.Slug,
            request.CategorySlug,
            request.Phone,
            request.AddressLine,
            request.Area,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<BusinessProfileResponse>(validation.Error);
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        var slugTaken = await dbContext.BusinessProfiles
            .AsNoTracking()
            .AnyAsync(p => p.Slug == normalizedSlug && p.TenantId != tenantId, cancellationToken);
        if (slugTaken)
        {
            return Result.Failure<BusinessProfileResponse>(Error.Conflict("Business slug is already taken."));
        }

        tenant.Name = request.BusinessName.Trim();
        ApplyProfile(profile, request, normalizedSlug, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(MapProfile(tenant, profile, await GetDocumentsAsync(tenantId, cancellationToken)));
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

        var (tenant, profile) = access.Value!;

        if (tenant.Status is not TenantStatus.Draft and not TenantStatus.Rejected)
        {
            return Result.Failure(Error.Validation("Verification can only be submitted from draft or rejected status."));
        }

        if (string.IsNullOrWhiteSpace(profile.Slug))
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
        CancellationToken cancellationToken) =>
        await ValidateProfileFieldsAsync(
            request.BusinessName,
            request.Slug,
            request.CategorySlug,
            request.Phone,
            request.AddressLine,
            request.Area,
            cancellationToken);

    private async Task<Result> ValidateProfileFieldsAsync(
        string businessName,
        string slug,
        string categorySlug,
        string phone,
        string addressLine,
        string area,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(businessName) || businessName.Trim().Length < 2)
        {
            return Result.Failure(Error.Validation("Business name must be at least 2 characters."));
        }

        var normalizedSlug = NormalizeSlug(slug);
        if (!SlugPattern.IsMatch(normalizedSlug))
        {
            return Result.Failure(Error.Validation("Slug must be 3-64 lowercase letters, numbers, or hyphens."));
        }

        if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length < 10)
        {
            return Result.Failure(Error.Validation("Phone number must be at least 10 characters."));
        }

        if (string.IsNullOrWhiteSpace(addressLine) || addressLine.Trim().Length < 5)
        {
            return Result.Failure(Error.Validation("Address must be at least 5 characters."));
        }

        if (string.IsNullOrWhiteSpace(area) || area.Trim().Length < 2)
        {
            return Result.Failure(Error.Validation("Area is required."));
        }

        var categories = await categoryService.GetCategoriesAsync(cancellationToken);
        if (!categories.Any(c => c.Slug.Equals(categorySlug.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(Error.Validation("Category is not valid."));
        }

        return Result.Success();
    }

    private static void ApplyProfile(
        BusinessProfile profile,
        RegisterBusinessRequest request,
        string normalizedSlug,
        DateTimeOffset updatedAt)
    {
        profile.Slug = normalizedSlug;
        profile.CategorySlug = request.CategorySlug.Trim().ToLowerInvariant();
        profile.Phone = request.Phone.Trim();
        profile.AddressLine = request.AddressLine.Trim();
        profile.Area = request.Area.Trim();
        profile.Description = request.Description?.Trim() ?? string.Empty;
        profile.Latitude = request.Latitude;
        profile.Longitude = request.Longitude;
        profile.UpdatedAt = updatedAt;
    }

    private static void ApplyProfile(
        BusinessProfile profile,
        UpdateBusinessProfileRequest request,
        string normalizedSlug,
        DateTimeOffset updatedAt)
    {
        profile.Slug = normalizedSlug;
        profile.CategorySlug = request.CategorySlug.Trim().ToLowerInvariant();
        profile.Phone = request.Phone.Trim();
        profile.AddressLine = request.AddressLine.Trim();
        profile.Area = request.Area.Trim();
        profile.Description = request.Description?.Trim() ?? string.Empty;
        profile.Latitude = request.Latitude;
        profile.Longitude = request.Longitude;
        profile.UpdatedAt = updatedAt;
    }

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private async Task<IReadOnlyList<VerificationDocument>> GetDocumentsAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.VerificationDocuments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderBy(d => d.SubmittedAt)
            .ToListAsync(cancellationToken);

    private static BusinessProfileResponse MapProfile(
        Tenant tenant,
        BusinessProfile profile,
        IReadOnlyList<VerificationDocument> documents) =>
        new(
            tenant.Id,
            tenant.Name,
            profile.Slug,
            tenant.Status,
            profile.CategorySlug,
            profile.Phone,
            profile.AddressLine,
            profile.Area,
            profile.Description,
            profile.Latitude,
            profile.Longitude,
            tenant.CreatedAt,
            tenant.VerifiedAt,
            documents.Select(d => new VerificationDocumentResponse(d.DocumentType, d.SubmittedAt)).ToList());

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,62}[a-z0-9])?$")]
    private static partial Regex SlugRegex();
}
