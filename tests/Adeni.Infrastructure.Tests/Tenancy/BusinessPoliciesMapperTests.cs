using Adeni.Application.Tenancy;
using Adeni.Domain.Tenancy;

namespace Adeni.Infrastructure.Tests.Tenancy;

public sealed class BusinessPoliciesMapperTests
{
    [Fact]
    public void ValidatePolicies_TooLong_Fails()
    {
        var longText = new string('a', PublicPageConfigMapper.MaxPolicyTextLength + 1);
        var result = PublicPageConfigMapper.ValidatePolicies(
            new UpdateBusinessPoliciesRequest(Booking: longText, RequireAcceptance: false));

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public void ApplyPolicies_TrimsAndNullsEmpty()
    {
        var profile = new BusinessProfile();
        PublicPageConfigMapper.ApplyPolicies(
            profile,
            new UpdateBusinessPoliciesRequest(
                Booking: "  Book 48h ahead  ",
                Payment: "   ",
                Cancellation: null,
                Terms: "Terms",
                RequireAcceptance: true));

        Assert.Equal("Book 48h ahead", profile.PolicyBookingText);
        Assert.Null(profile.PolicyPaymentText);
        Assert.Null(profile.PolicyCancellationText);
        Assert.Equal("Terms", profile.PolicyTermsText);
        Assert.True(profile.RequirePolicyAcceptance);
    }

    [Fact]
    public void PoliciesFromProfileOrNull_Empty_ReturnsNull()
    {
        var profile = new BusinessProfile();
        Assert.Null(PublicPageConfigMapper.PoliciesFromProfileOrNull(profile));
    }

    [Fact]
    public void FromProfile_IncludesPoliciesSection()
    {
        var profile = new BusinessProfile { PublicPageShowPolicies = true };
        var dto = PublicPageConfigMapper.FromProfile(profile, null);
        Assert.True(dto.Sections?.Policies);
    }

    [Fact]
    public void Validate_WithPoliciesSection_Succeeds()
    {
        var result = PublicPageConfigMapper.Validate(
            new UpdatePublicPageRequest(
                "studio",
                new PublicPageSectionsDto(true, true, true, true, true, true),
                "#0F766E"));

        Assert.True(result.IsSuccess);
    }
}
