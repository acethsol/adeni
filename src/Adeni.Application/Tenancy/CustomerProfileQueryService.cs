namespace Adeni.Application.Tenancy;

using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Application.Finance;
using Adeni.Application.Identity;
using Adeni.Application.Messaging;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;

public sealed class CustomerProfileQueryService(
    IBusinessPermissionService permissions,
    ICustomerProfileContextQuery contexts,
    ICustomerProfileTrustQuery profileTrust,
    ITenantLocationScopeService locations,
    ICustomerBookingActivityQuery booking,
    ICustomerCommerceQuery finance,
    ICustomerCommunicationActivityQuery messaging,
    ICustomerActivityCursorCodec cursors,
    TimeProvider clock,
    Adeni.Application.Retention.ICustomerPackageSummaryQuery? packages = null) : ICustomerProfileQueryService
{
    public async Task<Result<CustomerProfileResponse>> GetProfileAsync(Guid tenantId, Guid tenantCustomerId, string auth0Sub,
        Guid? locationId = null, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(tenantId, tenantCustomerId, auth0Sub, locationId, cancellationToken);
        if (resolved.IsFailure) return Result.Failure<CustomerProfileResponse>(resolved.Error);
        var access = resolved.Value!;
        var now = clock.GetUtcNow();
        // Source adapters share the scoped DbContext; do not run these concurrently.
        var profile = await booking.GetProfileAsync(access.Scope, now, access.Context.Entitlements.BasicAnalytics, cancellationToken);
        var totals = access.Availability.Finance == "available"
            ? await finance.GetTotalsAsync(access.Scope, now, cancellationToken) : null;
        // Retention-owned read port; no fake zeros when the plan does not include packages.
        var packageSummary = access.Availability.Retention == "available" && packages is not null
            ? await packages.GetSummaryAsync(tenantId, tenantCustomerId, cancellationToken) : null;
        var trust = await profileTrust.GetForPlatformCustomerAsync(
            access.Context.CustomerId,
            cancellationToken);

        return Result.Success(new CustomerProfileResponse(tenantCustomerId, now, access.Scope.LocationId,
            access.Availability, profile.Visits, profile.NextAppointment, totals, packageSummary, trust));
    }

    public async Task<Result<CustomerActivityResponse>> GetActivityAsync(Guid tenantId, Guid tenantCustomerId, string auth0Sub,
        Guid? locationId = null, int pageSize = 25, string? cursor = null, CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 50 || cursor?.Length > 4096)
            return Result.Failure<CustomerActivityResponse>(Error.Validation("Invalid activity page size or cursor."));
        var resolved = await ResolveAsync(tenantId, tenantCustomerId, auth0Sub, locationId, cancellationToken);
        if (resolved.IsFailure) return Result.Failure<CustomerActivityResponse>(resolved.Error);
        var access = resolved.Value!;
        var now = clock.GetUtcNow();
        var position = cursor is null ? null : cursors.Decode(cursor);
        if (cursor is not null && (position is null || position.Binding != access.Binding
            || position.AsOf > now || position.AsOf < now.AddHours(-1)))
            return Result.Failure<CustomerActivityResponse>(Error.Validation("Invalid or expired activity cursor. Refresh activity."));
        var asOf = position?.AsOf ?? now;
        var from = asOf.AddDays(-90);
        var batch = await booking.GetActivityAsync(access.Scope, from, asOf,
            access.Context.Entitlements.CustomerRetentionAnalytics ? 25 : 3, cancellationToken);
        var items = new List<CustomerActivityItem>(batch.Items);
        var truncated = batch.IsTruncated;
        if (access.Availability.Finance == "available")
        {
            var payments = await finance.GetActivityAsync(access.Scope, from, asOf, cancellationToken);
            items.AddRange(payments.Items);
            truncated |= payments.IsTruncated;
        }
        if (access.Availability.Messaging == "available")
        {
            var messages = await messaging.GetActivityAsync(access.Scope, from, asOf, cancellationToken);
            items.AddRange(messages.Items);
            truncated |= messages.IsTruncated;
        }
        var page = items.Where(x => position is null || x.OccurredAt < position.LastOccurredAt
                || (x.OccurredAt == position.LastOccurredAt && string.CompareOrdinal(x.Id, position.LastId) < 0))
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id, StringComparer.Ordinal)
            .Take(pageSize + 1).ToArray();
        var visible = page.Take(pageSize).ToArray();
        var next = page.Length > pageSize
            ? cursors.Encode(new CustomerActivityCursor(access.Binding, asOf, visible[^1].OccurredAt, visible[^1].Id)) : null;
        return Result.Success(new CustomerActivityResponse(visible, next, asOf, from, truncated, access.Availability));
    }

    private sealed record Access(CustomerProfileScope Scope, CustomerProfileContext Context, CustomerSourceAvailability Availability, string Binding);

    private async Task<Result<Access>> ResolveAsync(Guid tenantId, Guid customerId, string auth0Sub, Guid? locationId, CancellationToken cancellationToken)
    {
        var actor = await permissions.ResolveAsync(auth0Sub, cancellationToken);
        if (actor.IsFailure) return Result.Failure<Access>(actor.Error);
        var user = actor.Value!;
        // Self-only customer aggregates are deferred: legacy customer and booking scopes differ.
        if (user.TenantId != tenantId || !user.Permissions.Contains(BusinessPermissions.Bookings))
            return Result.Failure<Access>(ErrorCodes.PermissionDeniedError());
        var context = await contexts.GetAsync(tenantId, customerId, cancellationToken);
        if (context.IsFailure) return Result.Failure<Access>(context.Error);
        var location = await locations.ResolveListLocationAsync(tenantId, locationId, cancellationToken);
        if (location.IsFailure) return Result.Failure<Access>(location.Error);
        var data = context.Value!;
        var entitlements = data.Entitlements;
        var availability = new CustomerSourceAvailability(
            "available",
            !user.Permissions.Contains(BusinessPermissions.Finance) ? "restricted"
                : !entitlements.BasicAnalytics ? "notEntitled" : data.CustomerId is null ? "notLinked" : "available",
            !user.Permissions.Contains(BusinessPermissions.Messages) ? "restricted"
                : !entitlements.Messaging ? "notEntitled" : data.CustomerId is null ? "notLinked"
                : location.Value is not null ? "unscoped" : "available",
            Retention: packages is null ? "notImplemented" : !entitlements.Packages ? "notEntitled" : "available");
        var scope = new CustomerProfileScope(tenantId, customerId, data.CustomerId, location.Value);
        var binding = $"v1:{tenantId}:{customerId}:{data.CustomerId}:{location.Value}:{user.BusinessUserId}:{user.PermissionRole}:{entitlements.CustomerRetentionAnalytics}:{entitlements.BasicAnalytics}:{entitlements.MultiLocationReporting}:{availability.Finance}:{availability.Messaging}";
        return Result.Success(new Access(scope, data, availability, binding));
    }
}
