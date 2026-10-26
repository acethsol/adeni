namespace Adeni.Application.Tenancy;

using Adeni.Application.Booking;

public static class CustomerSegmentPolicy
{
    public const string Version = "v1";
    public static IReadOnlyList<CustomerSegmentDefinition> Definitions { get; } = Array.AsReadOnly<CustomerSegmentDefinition>([
        new("newCustomer", 30, 1), new("recentlyActive", 30, 1), new("lapsed", 90, 1, 90),
        new("atRisk", 180, 3, 30), new("vip", 180, 6), new("frequentQueueNoShow", 90, 2)]);

    public static IReadOnlyList<CustomerSegmentMatch> Evaluate(CustomerSegmentFacts facts, DateTimeOffset asOf, ISchedulingTimeZone zone)
    {
        // Incomplete identity/location evidence must not create confident classifications.
        if (facts.Partial) return [];
        var days = facts.LastVisit is { } last
            ? (int?)(zone.ToLocal(asOf).Date - zone.ToLocal(last).Date).Days : null;
        var matches = new List<CustomerSegmentMatch>();
        if (facts.CompletedVisits == 1 && facts.RecentVisits == 1) matches.Add(new("newCustomer", 1, days));
        if (facts.RecentVisits > 0) matches.Add(new("recentlyActive", facts.RecentVisits, days));
        if (days >= 90 && !facts.HasUpcomingBooking) matches.Add(new("lapsed", facts.CompletedVisits, days));
        if (facts.FrequencyVisits >= 3 && days is >= 30 and < 90 && !facts.HasUpcomingBooking) matches.Add(new("atRisk", facts.FrequencyVisits, days));
        if (facts.FrequencyVisits >= 6 && facts.RecentVisits > 0) matches.Add(new("vip", facts.FrequencyVisits, days));
        if (facts.QueueNoShows >= 2) matches.Add(new("frequentQueueNoShow", facts.QueueNoShows, null));
        return matches;
    }

    public static CustomerSegmentWindow Window(DateTimeOffset asOf, ISchedulingTimeZone zone)
    {
        var today = zone.ToLocal(asOf).Date;
        return new(asOf, zone.ToUtc(today.AddDays(-29), TimeOnly.MinValue),
            zone.ToUtc(today.AddDays(-89), TimeOnly.MinValue), zone.ToUtc(today.AddDays(-179), TimeOnly.MinValue));
    }
}
