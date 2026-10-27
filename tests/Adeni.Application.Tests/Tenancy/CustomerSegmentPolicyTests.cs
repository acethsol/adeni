namespace Adeni.Application.Tests.Tenancy;

using Adeni.Application.Booking;
using Adeni.Application.Tenancy;

public sealed class CustomerSegmentPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-26T12:00:00Z");
    private sealed class Zone(string id) : ISchedulingTimeZone
    {
        private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(id);
        public string TimeZoneId => id;
        public DateTimeOffset ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, zone);
        public DateTimeOffset ToUtc(DateTime date, TimeOnly time) => new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Date + time.ToTimeSpan(), DateTimeKind.Unspecified), zone));
    }
    private static CustomerSegmentFacts Facts(int days = 1, int count = 1, int frequency = 1, bool upcoming = false, int noShows = 0) =>
        new(Guid.NewGuid(), count, days < 30 ? count : 0, frequency, Now.AddDays(-days), noShows, upcoming);

    [Theory]
    [InlineData(29, true, false, false)]
    [InlineData(30, false, true, false)]
    [InlineData(89, false, true, false)]
    [InlineData(90, false, false, true)]
    public void Recency_UsesExplicitNonOverlappingEdges(int days, bool recent, bool risk, bool lapsed)
    {
        var labels = CustomerSegmentPolicy.Evaluate(Facts(days, 3, 3), Now, new Zone("Africa/Lagos")).Select(x => x.Id).ToArray();
        Assert.Equal(recent, labels.Contains("recentlyActive"));
        Assert.Equal(risk, labels.Contains("atRisk"));
        Assert.Equal(lapsed, labels.Contains("lapsed"));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void AtRisk_RequiresRepeatVisits(int count, bool expected) =>
        Assert.Equal(expected, CustomerSegmentPolicy.Evaluate(Facts(45, count, count), Now, new Zone("Africa/Lagos")).Any(x => x.Id == "atRisk"));

    [Theory]
    [InlineData(1, true, false)]
    [InlineData(2, false, false)]
    [InlineData(5, false, false)]
    [InlineData(6, false, true)]
    public void Frequency_NewAndVipHaveDistinctThresholds(int count, bool newCustomer, bool vip)
    {
        var labels = CustomerSegmentPolicy.Evaluate(Facts(count: count, frequency: count), Now, new Zone("Africa/Lagos"));
        Assert.Equal(newCustomer, labels.Any(x => x.Id == "newCustomer"));
        Assert.Equal(vip, labels.Any(x => x.Id == "vip"));
    }

    [Theory]
    [InlineData(45)]
    [InlineData(100)]
    public void UpcomingBooking_SuppressesRiskAndLapsed(int days) =>
        Assert.DoesNotContain(CustomerSegmentPolicy.Evaluate(Facts(days, 3, 3, true), Now, new Zone("Africa/Lagos")), x => x.Id is "atRisk" or "lapsed");

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void NoShow_IsExplicitlyQueueOnly(int count, bool expected) =>
        Assert.Equal(expected, CustomerSegmentPolicy.Evaluate(Facts(noShows: count), Now, new Zone("Africa/Lagos")).Any(x => x.Id == "frequentQueueNoShow"));

    [Fact]
    public void PartialEvidence_DoesNotFabricateClassifications() =>
        Assert.Empty(CustomerSegmentPolicy.Evaluate(Facts() with { Partial = true }, Now, new Zone("Africa/Lagos")));

    [Fact]
    public void EmptyEvidence_DoesNotCreateLapsedOrNewCustomer() =>
        Assert.Empty(CustomerSegmentPolicy.Evaluate(new(Guid.NewGuid(), 0, 0, 0, null, 0, false), Now, new Zone("Africa/Lagos")));

    [Theory]
    [InlineData("America/Toronto", "2026-03-09T12:00:00Z", "2026-02-08T05:00:00Z")]
    [InlineData("America/Toronto", "2026-11-02T12:00:00Z", "2026-10-04T04:00:00Z")]
    [InlineData("Africa/Lagos", "2026-03-09T12:00:00Z", "2026-02-07T23:00:00Z")]
    public void Windows_UseBusinessMidnightAcrossDst(string zone, string instant, string expected)
    {
        var window = CustomerSegmentPolicy.Window(DateTimeOffset.Parse(instant), new Zone(zone));
        Assert.Equal(DateTimeOffset.Parse(expected), window.RecentFrom);
        Assert.Equal(DateTimeOffset.Parse(instant), window.AsOf);
    }

    [Fact]
    public void Definitions_AreVersionedAndContainNoUnsupportedOrFinancialClassifications()
    {
        Assert.Equal("v1", CustomerSegmentPolicy.Version);
        Assert.Equal(6, CustomerSegmentPolicy.Definitions.Count);
        Assert.DoesNotContain(CustomerSegmentPolicy.Definitions, x => x.Id is "highSpender" or "activeMember" or "packageHolder");
    }
}
