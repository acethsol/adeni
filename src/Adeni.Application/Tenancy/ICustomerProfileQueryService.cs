namespace Adeni.Application.Tenancy;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;

public sealed record CustomerProfileScope(Guid TenantId, Guid TenantCustomerId, Guid? CustomerId, Guid? LocationId);
public sealed record CustomerProfileContext(Guid TenantCustomerId, Guid? CustomerId, TenantEntitlements Entitlements);
public sealed record CustomerSourceAvailability(string Booking, string Finance, string Messaging, string Preferences = "notImplemented", string Retention = "notImplemented");
public sealed record CustomerVisitSummary(int CompletedBookings, int CompletedWalkIns);
public sealed record CustomerNextAppointment(Guid BookingId, DateTimeOffset StartAt, string ServiceName, string Status);
public sealed record CustomerCurrencyTotal(string Currency, decimal Amount);
public sealed record CustomerActivityItem(string Id, string Kind, DateTimeOffset OccurredAt, string Source, Guid SourceId,
    Guid? LocationId = null, string? ServiceName = null, string? CurrentStatus = null, decimal? Amount = null, string? Currency = null);
public sealed record CustomerActivityBatch(IReadOnlyList<CustomerActivityItem> Items, bool IsTruncated = false);
public sealed record CustomerBookingProfile(CustomerVisitSummary? Visits, CustomerNextAppointment? NextAppointment);
public sealed record CustomerProfileResponse(Guid TenantCustomerId, DateTimeOffset AsOf, Guid? LocationId,
    CustomerSourceAvailability Availability, CustomerVisitSummary? VisitSummary, CustomerNextAppointment? NextAppointment,
    IReadOnlyList<CustomerCurrencyTotal>? RecordedOnlinePaymentsByCurrency,
    Adeni.Application.Retention.CustomerPackageSummary? Packages = null);
public sealed record CustomerActivityResponse(IReadOnlyList<CustomerActivityItem> Items, string? NextCursor,
    DateTimeOffset AsOf, DateTimeOffset From, bool IsTruncated, CustomerSourceAvailability Availability);

public interface ICustomerProfileContextQuery
{
    Task<Result<CustomerProfileContext>> GetAsync(Guid tenantId, Guid tenantCustomerId, CancellationToken cancellationToken);
}

public sealed record CustomerActivityCursor(string Binding, DateTimeOffset AsOf, DateTimeOffset LastOccurredAt, string LastId);

public interface ICustomerActivityCursorCodec
{
    string Encode(CustomerActivityCursor cursor);
    CustomerActivityCursor? Decode(string cursor);
}

public interface ICustomerProfileQueryService
{
    Task<Result<CustomerProfileResponse>> GetProfileAsync(Guid tenantId, Guid tenantCustomerId, string auth0Sub,
        Guid? locationId = null, CancellationToken cancellationToken = default);
    Task<Result<CustomerActivityResponse>> GetActivityAsync(Guid tenantId, Guid tenantCustomerId, string auth0Sub,
        Guid? locationId = null, int pageSize = 25, string? cursor = null, CancellationToken cancellationToken = default);
}
