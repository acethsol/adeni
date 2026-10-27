namespace Adeni.Application.Tenancy;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;

public sealed record CustomerSegmentDefinition(string Id, int WindowDays, int MinimumVisits, int? MinimumRecencyDays = null);
public sealed record CustomerSegmentMatch(string Id, int Count, int? DaysSinceLastVisit);
public sealed record CustomerSegmentsResponse(Guid TenantCustomerId, string Availability, DateTimeOffset AsOf,
    string DefinitionVersion, Guid? LocationId, string? TimeZoneId, bool ApproximateVisitDates,
    IReadOnlyList<CustomerSegmentMatch> Matches);
public sealed record CustomerSegmentContext(IReadOnlyList<CustomerProfileScope> Customers, TenantEntitlements Entitlements);
public sealed record CustomerSegmentsBatchRequest(IReadOnlyList<Guid>? CustomerIds, Guid? LocationId = null);

public interface ICustomerSegmentContextQuery
{
    Task<Result<CustomerSegmentContext>> GetAsync(Guid tenantId, IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<(Guid TenantId, Guid TenantCustomerId)>> FindLinkedAsync(Guid customerId, CancellationToken ct);
}

public interface ICustomerSegmentsQueryService
{
    Task<Result<IReadOnlyList<CustomerSegmentsResponse>>> GetAsync(Guid tenantId, IReadOnlyList<Guid>? customerIds,
        string actor, Guid? locationId = null, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CustomerSegmentDefinition>>> DefinitionsAsync(Guid tenantId, string actor, CancellationToken ct);
    Task<IReadOnlyList<CustomerSegmentsResponse>> ExportAsync(Guid customerId, CancellationToken ct);
}
