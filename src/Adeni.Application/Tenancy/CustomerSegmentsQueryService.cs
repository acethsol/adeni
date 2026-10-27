namespace Adeni.Application.Tenancy;

using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;

public sealed class CustomerSegmentsQueryService(IPortalTenantAccessQuery access, ICustomerSegmentContextQuery contexts,
    ITenantLocationScopeService locations, ITenantSchedulingTimeZone timeZones, ICustomerSegmentFactsQuery facts,
    ICustomerErasureQuery erasure, TimeProvider clock) : ICustomerSegmentsQueryService
{
    public async Task<Result<IReadOnlyList<CustomerSegmentsResponse>>> GetAsync(Guid tenantId, IReadOnlyList<Guid>? customerIds,
        string actor, Guid? locationId = null, CancellationToken ct = default)
    {
        if (customerIds is null || customerIds.Count is < 1 or > 100 || customerIds.Contains(Guid.Empty) || customerIds.Distinct().Count() != customerIds.Count)
            return Result.Failure<IReadOnlyList<CustomerSegmentsResponse>>(Error.Validation("Supply 1–100 distinct customer identifiers."));
        var permitted = await AuthorizeAsync(tenantId, actor, ct);
        if (permitted.IsFailure) return Result.Failure<IReadOnlyList<CustomerSegmentsResponse>>(permitted.Error);
        return await EvaluateAsync(tenantId, customerIds, locationId, ct);
    }

    public async Task<Result<IReadOnlyList<CustomerSegmentDefinition>>> DefinitionsAsync(Guid tenantId, string actor, CancellationToken ct)
    {
        var permitted = await AuthorizeAsync(tenantId, actor, ct);
        return permitted.IsFailure ? Result.Failure<IReadOnlyList<CustomerSegmentDefinition>>(permitted.Error)
            : Result.Success(CustomerSegmentPolicy.Definitions);
    }

    private async Task<Result> AuthorizeAsync(Guid tenantId, string actor, CancellationToken ct)
    {
        var user = await access.ResolveAsync(tenantId, actor, ct);
        return user.IsFailure || !user.Value!.Permissions.Contains(PortalPermissions.Bookings)
            ? Result.Failure(ErrorCodes.PermissionDeniedError()) : Result.Success();
    }

    private async Task<Result<IReadOnlyList<CustomerSegmentsResponse>>> EvaluateAsync(Guid tenantId, IReadOnlyList<Guid> ids, Guid? locationId, CancellationToken ct)
    {
        var context = await contexts.GetAsync(tenantId, ids, ct);
        if (context.IsFailure) return Result.Failure<IReadOnlyList<CustomerSegmentsResponse>>(context.Error);
        var location = await locations.ResolveListLocationAsync(tenantId, locationId, ct);
        if (location.IsFailure) return Result.Failure<IReadOnlyList<CustomerSegmentsResponse>>(location.Error);
        var data = context.Value!;
        var asOf = clock.GetUtcNow();
        var entitled = data.Entitlements.BasicAnalytics && data.Entitlements.CustomerRetentionAnalytics;
        var erased = await erasure.GetErasedAsync(data.Customers.Where(x => x.CustomerId != null).Select(x => x.CustomerId!.Value).ToArray(), ct);
        var eligible = data.Customers.Where(x => entitled && (x.CustomerId is null || !erased.Contains(x.CustomerId.Value)))
            .Select(x => x with { LocationId = location.Value }).ToArray();
        ISchedulingTimeZone? zone = null;
        IReadOnlyList<CustomerSegmentFacts> evidence = [];
        if (eligible.Length > 0)
        {
            zone = location.Value is Guid branch ? await timeZones.ForLocationAsync(branch, ct) : await timeZones.ForTenantAsync(tenantId, ct);
            if (zone.IsConfiguredZoneValid)
                evidence = await facts.GetAsync(eligible, CustomerSegmentPolicy.Window(asOf, zone), ct);
        }
        var byId = evidence.ToDictionary(x => x.TenantCustomerId);
        return Result.Success<IReadOnlyList<CustomerSegmentsResponse>>(data.Customers.Select(x =>
        {
            byId.TryGetValue(x.TenantCustomerId, out var source);
            var availability = !entitled ? "notEntitled" : x.CustomerId is Guid id && erased.Contains(id) ? "erased"
                : zone?.IsConfiguredZoneValid != true ? "notAvailable" : source is null || source.Partial ? "partial" : "available";
            return new CustomerSegmentsResponse(x.TenantCustomerId, availability, asOf, CustomerSegmentPolicy.Version,
                location.Value, zone?.TimeZoneId, source?.ApproximateVisitDates ?? false,
                availability == "available" ? CustomerSegmentPolicy.Evaluate(source!, asOf, zone!) : []);
        }).ToArray());
    }

    public async Task<IReadOnlyList<CustomerSegmentsResponse>> ExportAsync(Guid customerId, CancellationToken ct)
    {
        var linked = await contexts.FindLinkedAsync(customerId, ct);
        var result = new List<CustomerSegmentsResponse>();
        foreach (var group in linked.GroupBy(x => x.TenantId))
        {
            // This internal port is used only by the existing MFA-protected, audited privacy export.
            var evaluation = await EvaluateAsync(group.Key, group.Select(x => x.TenantCustomerId).ToArray(), null, ct);
            if (evaluation.IsSuccess) result.AddRange(evaluation.Value!);
        }
        return result;
    }
}
