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
                Book: true));

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

        if (!sections.Book)
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

        if (request.LogoImageKey is null)
        {
            return;
        }

        var key = request.LogoImageKey.Trim();
        profile.LogoImageKey = key.Length == 0 ? null : key;
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
