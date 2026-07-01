namespace Adeni.Application.Tenancy;

using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;

public sealed record RegisterBusinessRequest(
    string BusinessName,
    string Slug,
    string CategorySlug,
    string Phone,
    string AddressLine,
    string Area,
    string? Description,
    double? Latitude,
    double? Longitude);

public sealed record UpdateBusinessProfileRequest(
    string BusinessName,
    string Slug,
    string CategorySlug,
    string Phone,
    string AddressLine,
    string Area,
    string? Description,
    double? Latitude,
    double? Longitude);

public sealed record VerificationDocumentRequest(
    VerificationDocumentType DocumentType,
    string ReferenceNumber);

public sealed record SubmitVerificationRequest(
    IReadOnlyList<VerificationDocumentRequest> Documents);

public sealed record BusinessProfileResponse(
    Guid TenantId,
    string BusinessName,
    string Slug,
    TenantStatus Status,
    string CategorySlug,
    string Phone,
    string AddressLine,
    string Area,
    string Description,
    double? Latitude,
    double? Longitude,
    DateTimeOffset CreatedAt,
    DateTimeOffset? VerifiedAt,
    IReadOnlyList<VerificationDocumentResponse> VerificationDocuments);

public sealed record VerificationDocumentResponse(
    VerificationDocumentType DocumentType,
    DateTimeOffset SubmittedAt);

public sealed record RegisterBusinessResponse(
    Guid TenantId,
    string Slug,
    TenantStatus Status);

public interface IBusinessOnboardingService
{
    Task<Result<RegisterBusinessResponse>> RegisterAsync(
        RegisterBusinessRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<BusinessProfileResponse>> GetProfileAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<BusinessProfileResponse>> UpdateProfileAsync(
        Guid tenantId,
        UpdateBusinessProfileRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result> SubmitVerificationAsync(
        Guid tenantId,
        SubmitVerificationRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default);
}
