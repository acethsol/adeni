namespace Adeni.Application.Booking;

using Adeni.Application.Tenancy;

public sealed record CustomerSegmentWindow(DateTimeOffset AsOf, DateTimeOffset RecentFrom, DateTimeOffset NoShowFrom, DateTimeOffset FrequencyFrom);
public sealed record CustomerSegmentFacts(Guid TenantCustomerId, int CompletedVisits, int RecentVisits, int FrequencyVisits,
    DateTimeOffset? LastVisit, int QueueNoShows, bool HasUpcomingBooking, bool Partial = false, bool ApproximateVisitDates = false);

public interface ICustomerSegmentFactsQuery
{
    Task<IReadOnlyList<CustomerSegmentFacts>> GetAsync(IReadOnlyList<CustomerProfileScope> scopes, CustomerSegmentWindow window, CancellationToken ct);
}
