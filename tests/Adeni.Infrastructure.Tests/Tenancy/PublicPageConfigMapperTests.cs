using Adeni.Application.Tenancy;
using Adeni.Domain.Common;

namespace Adeni.Infrastructure.Tests.Tenancy;

public sealed class PublicPageConfigMapperTests
{
    [Fact]
    public void Validate_InvalidTemplate_Fails()
    {
        var result = PublicPageConfigMapper.Validate(
            new UpdatePublicPageRequest(
                "neon",
                new PublicPageSectionsDto(),
                "#0F766E"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PublicPageInvalidTemplate, result.Error.Code);
    }

    [Fact]
    public void Validate_InvalidAccent_Fails()
    {
        var result = PublicPageConfigMapper.Validate(
            new UpdatePublicPageRequest(
                "studio",
                new PublicPageSectionsDto(),
                "teal"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PublicPageInvalidAccent, result.Error.Code);
    }

    [Fact]
    public void Validate_NoContentSections_Fails()
    {
        var result = PublicPageConfigMapper.Validate(
            new UpdatePublicPageRequest(
                "spa",
                new PublicPageSectionsDto(false, false, true, false, true)));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PublicPageSectionsRequired, result.Error.Code);
    }

    [Fact]
    public void Validate_ValidRequest_Succeeds()
    {
        var result = PublicPageConfigMapper.Validate(
            new UpdatePublicPageRequest(
                "luxe",
                new PublicPageSectionsDto(true, true, false, true, true),
                "#ABCDEF"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_BookingOff_Succeeds()
    {
        var result = PublicPageConfigMapper.Validate(
            new UpdatePublicPageRequest(
                "studio",
                new PublicPageSectionsDto(true, true, true, true, false)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void FromProfile_RespectsBookingToggle()
    {
        var profile = new Adeni.Domain.Tenancy.BusinessProfile { PublicPageShowBook = false };
        var dto = PublicPageConfigMapper.FromProfile(profile, null);
        Assert.False(dto.Sections?.Book);
    }
}
