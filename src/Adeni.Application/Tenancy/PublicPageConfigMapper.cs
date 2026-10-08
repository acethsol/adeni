namespace Adeni.Application.Tenancy;

using System.Text.RegularExpressions;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;

public static partial class PublicPageConfigMapper
{
    public static PublicPageConfigDto FromProfile(
        BusinessProfile profile,
        string? logoImageUrl) =>
        new(
            PublicPageTemplates.Normalize(profile.PublicPageTemplateId),
            NormalizeAccentOrNull(profile.PublicPageAccentColor),
            logoImageUrl,
            new PublicPageSectionsDto(
                profile.PublicPageShowAbout,
                profile.PublicPageShowServices,
                profile.PublicPageShowReviews,
                profile.PublicPageShowVisit,
                profile.PublicPageShowBook,
                profile.PublicPageShowPolicies));

    public static Result Validate(UpdatePublicPageRequest request)
    {
        if (!PublicPageTemplates.IsValid(request.TemplateId))
        {
            return Result.Failure(ErrorCodes.PublicPageInvalidTemplateError());
        }

        if (!IsValidAccent(request.AccentColor))
        {
            return Result.Failure(ErrorCodes.PublicPageInvalidAccentError());
        }

        var sections = request.Sections;
        if (!sections.About && !sections.Services && !sections.Visit)
        {
            return Result.Failure(ErrorCodes.PublicPageSectionsRequiredError());
        }

        return Result.Success();
    }

    public static void Apply(BusinessProfile profile, UpdatePublicPageRequest request)
    {
        profile.PublicPageTemplateId = PublicPageTemplates.Normalize(request.TemplateId);
        profile.PublicPageAccentColor = NormalizeAccentOrNull(request.AccentColor);
        profile.PublicPageShowAbout = request.Sections.About;
        profile.PublicPageShowServices = request.Sections.Services;
        profile.PublicPageShowReviews = request.Sections.Reviews;
        profile.PublicPageShowVisit = request.Sections.Visit;
        profile.PublicPageShowBook = request.Sections.Book;
        profile.PublicPageShowPolicies = request.Sections.Policies;

        if (request.LogoImageKey is null)
        {
            return;
        }

        var key = request.LogoImageKey.Trim();
        profile.LogoImageKey = key.Length == 0 ? null : key;
    }

    public const int MaxPolicyTextLength = 8000;

    public static BusinessPoliciesDto PoliciesFromProfile(BusinessProfile profile) =>
        new(
            profile.PolicyBookingText,
            profile.PolicyPaymentText,
            profile.PolicyCancellationText,
            profile.PolicyTermsText,
            profile.RequirePolicyAcceptance);

    public static BusinessPoliciesDto? PoliciesFromProfileOrNull(BusinessProfile profile)
    {
        var dto = PoliciesFromProfile(profile);
        if (string.IsNullOrWhiteSpace(dto.Booking)
            && string.IsNullOrWhiteSpace(dto.Payment)
            && string.IsNullOrWhiteSpace(dto.Cancellation)
            && string.IsNullOrWhiteSpace(dto.Terms)
            && !dto.RequireAcceptance)
        {
            return null;
        }

        return dto;
    }

    public static Result ValidatePolicies(UpdateBusinessPoliciesRequest request)
    {
        if (ExceedsPolicyLength(request.Booking)
            || ExceedsPolicyLength(request.Payment)
            || ExceedsPolicyLength(request.Cancellation)
            || ExceedsPolicyLength(request.Terms))
        {
            return Result.Failure(Error.Validation($"Each policy field must be at most {MaxPolicyTextLength} characters."));
        }

        return Result.Success();
    }

    public static void ApplyPolicies(BusinessProfile profile, UpdateBusinessPoliciesRequest request)
    {
        profile.PolicyBookingText = NormalizePolicyText(request.Booking);
        profile.PolicyPaymentText = NormalizePolicyText(request.Payment);
        profile.PolicyCancellationText = NormalizePolicyText(request.Cancellation);
        profile.PolicyTermsText = NormalizePolicyText(request.Terms);
        profile.RequirePolicyAcceptance = request.RequireAcceptance;
    }

    private static bool ExceedsPolicyLength(string? text) =>
        text is not null && text.Length > MaxPolicyTextLength;

    private static string? NormalizePolicyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }

    public static bool IsValidAccent(string? accent)
    {
        if (string.IsNullOrWhiteSpace(accent))
        {
            return true;
        }

        return HexAccentRegex().IsMatch(accent.Trim());
    }

    public static string? NormalizeAccentOrNull(string? accent)
    {
        if (string.IsNullOrWhiteSpace(accent))
        {
            return null;
        }

        return accent.Trim().ToUpperInvariant();
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexAccentRegex();
}
