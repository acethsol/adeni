namespace Adeni.Application.Tenancy;
using Adeni.Application.Finance;
using Adeni.Domain.Common;
public interface ICustomerGiftCardsQueryService
{
    Task<Result<IReadOnlyList<GiftCardView>>> GetAsync(Guid tenant, Guid customer, string actor, Guid? location, CancellationToken ct);
}
public interface ICustomerGiftCardQuery
{
    Task<Result<IReadOnlyList<GiftCardView>>> GetPurchasedAsync(CustomerProfileScope scope, string actor, CancellationToken ct);
}
public sealed class CustomerGiftCardsQueryService(ICustomerProfileQueryService profiles, ICustomerProfileContextQuery contexts, ICustomerGiftCardQuery gifts) : ICustomerGiftCardsQueryService
{
    public async Task<Result<IReadOnlyList<GiftCardView>>> GetAsync(Guid tenant, Guid customer, string actor, Guid? location, CancellationToken ct)
    {
        var profile=await profiles.GetProfileAsync(tenant,customer,actor,location,ct);
        if(profile.IsFailure)return Result.Failure<IReadOnlyList<GiftCardView>>(profile.Error);
        if(profile.Value!.Availability.Finance!="available")return Result.Failure<IReadOnlyList<GiftCardView>>(Error.Forbidden("Finance source is unavailable."));
        if(profile.Value.LocationId is not null)return Result.Failure<IReadOnlyList<GiftCardView>>(Error.Validation("Purchased gift cards are business-wide; select all locations."));
        var context=await contexts.GetAsync(tenant,customer,ct);
        if(context.IsFailure)return Result.Failure<IReadOnlyList<GiftCardView>>(context.Error);
        return await gifts.GetPurchasedAsync(new(tenant,customer,context.Value!.CustomerId,null),actor,ct);
    }
}
