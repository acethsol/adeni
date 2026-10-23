namespace Adeni.Infrastructure.Dashboard;

using System.Text.Json;
using Adeni.Application.Dashboard;
using Adeni.Application.Reporting;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Finance;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class TenantDashboardService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService) : ITenantDashboardService, ITenantReportingQueryService
{
    private static readonly JsonSerializerOptions LayoutJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<Result<TenantDashboardSummaryResponse>> GetSummaryAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId = null,
        Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<TenantDashboardSummaryResponse>(Error.NotFound("Tenant"));
        }

        var multiLocationReporting = entitlementsService.GetEntitlementsForTenant(tenant).MultiLocationReporting;
        var locationCheck = await TenantLocationScope.ResolveListLocationAsync(
            dbContext,
            tenantId,
            locationId,
            multiLocationReporting,
            cancellationToken);
        if (locationCheck.IsFailure)
        {
            return Result.Failure<TenantDashboardSummaryResponse>(locationCheck.Error!);
        }

        locationId = locationCheck.Value;
        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        var bookingQuery = ScopedBookings(tenantId, staffMemberId, locationId);

        var pendingCount = await bookingQuery
            .CountAsync(x => x.Status == BookingStatus.Pending, cancellationToken);

        var inRange = bookingQuery.Where(x =>
            x.StartAt >= from && x.StartAt < to
            && x.Status != BookingStatus.Cancelled
            && x.Status != BookingStatus.Rejected);

        var appointmentsCount = await inRange.CountAsync(cancellationToken);

        string? currency = await dbContext.ServiceOfferings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Currency)
            .FirstOrDefaultAsync(cancellationToken);

        decimal? collected = null;
        decimal? bookedValue = null;

        if (entitlements.AdvancedAnalytics)
        {
            var paymentQuery = dbContext.PaymentIntents.AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId
                    && x.Status == PaymentIntentStatus.Completed
                    && x.UpdatedAt >= from
                    && x.UpdatedAt < to);

            if (locationId is Guid branchId)
            {
                paymentQuery = paymentQuery.Where(x =>
                    x.BookingId != null
                    && dbContext.Bookings.Any(b =>
                        b.Id == x.BookingId
                        && b.TenantId == tenantId
                        && b.BusinessLocationId == branchId));
            }

            collected = await paymentQuery.SumAsync(x => (decimal?)x.Amount, cancellationToken);

            if (collected is > 0)
            {
                currency = await paymentQuery
                    .Select(x => x.Currency)
                    .FirstOrDefaultAsync(cancellationToken) ?? currency;
            }

            var bookedIds = await inRange
                .Where(x =>
                    x.Status == BookingStatus.Confirmed
                    || x.Status == BookingStatus.CheckedIn
                    || x.Status == BookingStatus.Completed
                    || x.Status == BookingStatus.Paid)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (bookedIds.Count > 0)
            {
                bookedValue = await dbContext.BookingLines.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && bookedIds.Contains(x.BookingId))
                    .SumAsync(x => (decimal?)x.PriceAmount, cancellationToken);
            }
            else
            {
                bookedValue = 0m;
            }
        }

        return Result.Success(new TenantDashboardSummaryResponse(
            from,
            to,
            pendingCount,
            appointmentsCount,
            collected,
            bookedValue,
            currency,
            entitlements.AdvancedAnalytics));
    }

    public async Task<Result<TenantDashboardSeriesResponse>> GetSeriesAsync(
        Guid tenantId,
        string metric,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId = null,
        Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<TenantDashboardSeriesResponse>(Error.NotFound("Tenant"));
        }

        var multiLocationReporting = entitlementsService.GetEntitlementsForTenant(tenant).MultiLocationReporting;
        var locationCheck = await TenantLocationScope.ResolveListLocationAsync(
            dbContext,
            tenantId,
            locationId,
            multiLocationReporting,
            cancellationToken);
        if (locationCheck.IsFailure)
        {
            return Result.Failure<TenantDashboardSeriesResponse>(locationCheck.Error!);
        }

        locationId = locationCheck.Value;
        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        var normalized = metric.Trim().ToLowerInvariant();
        if (!entitlements.AdvancedAnalytics && normalized is not "appointments")
        {
            return Result.Failure<TenantDashboardSeriesResponse>(ErrorCodes.AnalyticsNotEntitledError());
        }

        IReadOnlyList<TenantDashboardSeriesPoint> points;
        string? currency = null;

        switch (normalized)
        {
            case "appointments":
                points = await BuildAppointmentSeriesAsync(tenantId, from, to, staffMemberId, locationId, cancellationToken);
                break;
            case "collected":
                (points, currency) = await BuildCollectedSeriesAsync(tenantId, from, to, locationId, cancellationToken);
                break;
            case "booked_value":
                points = await BuildBookedValueSeriesAsync(tenantId, from, to, staffMemberId, locationId, cancellationToken);
                currency = await dbContext.ServiceOfferings.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.IsActive)
                    .Select(x => x.Currency)
                    .FirstOrDefaultAsync(cancellationToken);
                break;
            default:
                return Result.Failure<TenantDashboardSeriesResponse>(
                    Error.Validation("Invalid metric. Use appointments, collected, or booked_value."));
        }

        return Result.Success(new TenantDashboardSeriesResponse(
            normalized,
            from,
            to,
            currency,
            points));
    }

    public async Task<Result<TenantDashboardBreakdownResponse>> GetBreakdownAsync(
        Guid tenantId,
        string dimension,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId = null,
        Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<TenantDashboardBreakdownResponse>(Error.NotFound("Tenant"));
        }

        var multiLocationReporting = entitlementsService.GetEntitlementsForTenant(tenant).MultiLocationReporting;
        var locationCheck = await TenantLocationScope.ResolveListLocationAsync(
            dbContext,
            tenantId,
            locationId,
            multiLocationReporting,
            cancellationToken);
        if (locationCheck.IsFailure)
        {
            return Result.Failure<TenantDashboardBreakdownResponse>(locationCheck.Error!);
        }

        locationId = locationCheck.Value;
        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        var normalized = dimension.Trim().ToLowerInvariant();

        if (!entitlements.AdvancedAnalytics)
        {
            return Result.Failure<TenantDashboardBreakdownResponse>(ErrorCodes.AnalyticsNotEntitledError());
        }

        if (normalized == "staff" && !entitlements.StaffPerformanceAnalytics)
        {
            return Result.Failure<TenantDashboardBreakdownResponse>(ErrorCodes.AnalyticsNotEntitledError());
        }

        if (normalized == "service" && !entitlements.CustomerRetentionAnalytics)
        {
            return Result.Failure<TenantDashboardBreakdownResponse>(ErrorCodes.AnalyticsNotEntitledError());
        }
        var bookingQuery = ScopedBookings(tenantId, staffMemberId, locationId)
            .Where(x =>
                x.StartAt >= from
                && x.StartAt < to
                && x.Status != BookingStatus.Cancelled
                && x.Status != BookingStatus.Rejected);

        IReadOnlyList<TenantDashboardBreakdownItem> items = normalized switch
        {
            "status" => await bookingQuery
                .GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Select(x => new TenantDashboardBreakdownItem(
                    ((int)x.Status).ToString(),
                    x.Status.ToString(),
                    x.Count))
                .ToListAsync(cancellationToken),
            "service" => await BuildServiceBreakdownAsync(bookingQuery, tenantId, cancellationToken),
            "staff" => await BuildStaffBreakdownAsync(bookingQuery, cancellationToken),
            _ => Array.Empty<TenantDashboardBreakdownItem>(),
        };

        if (items.Count == 0 && normalized is not ("status" or "service" or "staff"))
        {
            return Result.Failure<TenantDashboardBreakdownResponse>(
                Error.Validation("Invalid dimension. Use status, service, or staff."));
        }

        return Result.Success(new TenantDashboardBreakdownResponse(normalized, from, to, items));
    }

    private IQueryable<BookingRecord> ScopedBookings(Guid tenantId, Guid? staffMemberId, Guid? locationId = null)
    {
        var query = dbContext.Bookings.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (staffMemberId is Guid staffId)
        {
            query = query.Where(x => x.StaffMemberId == staffId);
        }

        return query.FilterBookingsByLocation(locationId);
    }

    private async Task<IReadOnlyList<TenantDashboardSeriesPoint>> BuildAppointmentSeriesAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        var startTimes = await ScopedBookings(tenantId, staffMemberId, locationId)
            .Where(x =>
                x.StartAt >= from
                && x.StartAt < to
                && x.Status != BookingStatus.Cancelled
                && x.Status != BookingStatus.Rejected)
            .Select(x => x.StartAt)
            .ToListAsync(cancellationToken);

        var byDay = startTimes
            .GroupBy(x => DateOnly.FromDateTime(x.UtcDateTime))
            .ToDictionary(g => g.Key, g => (decimal)g.Count());

        return FillDailyPoints(from, to, byDay);
    }

    private async Task<(IReadOnlyList<TenantDashboardSeriesPoint> Points, string? Currency)> BuildCollectedSeriesAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        var paymentQuery = dbContext.PaymentIntents.AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.Status == PaymentIntentStatus.Completed
                && x.UpdatedAt >= from
                && x.UpdatedAt < to);

        if (locationId is Guid branchId)
        {
            paymentQuery = paymentQuery.Where(x =>
                x.BookingId != null
                && dbContext.Bookings.Any(b =>
                    b.Id == x.BookingId
                    && b.TenantId == tenantId
                    && b.BusinessLocationId == branchId));
        }

        var payments = await paymentQuery
            .Select(x => new { x.UpdatedAt, x.Amount })
            .ToListAsync(cancellationToken);

        var rows = payments
            .GroupBy(x => DateOnly.FromDateTime(x.UpdatedAt.UtcDateTime))
            .Select(g => new { Date = g.Key, Total = g.Sum(x => x.Amount) })
            .ToList();

        var currency = await paymentQuery
            .Select(x => x.Currency)
            .FirstOrDefaultAsync(cancellationToken);

        return (
            FillDailyPoints(from, to, rows.ToDictionary(x => x.Date, x => x.Total)),
            currency);
    }

    private async Task<IReadOnlyList<TenantDashboardSeriesPoint>> BuildBookedValueSeriesAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        var bookingsInRange = await ScopedBookings(tenantId, staffMemberId, locationId)
            .Where(x =>
                x.StartAt >= from
                && x.StartAt < to
                && (x.Status == BookingStatus.Confirmed
                    || x.Status == BookingStatus.CheckedIn
                    || x.Status == BookingStatus.Completed
                    || x.Status == BookingStatus.Paid))
            .Select(x => new { x.Id, x.StartAt })
            .ToListAsync(cancellationToken);

        var bookingIds = bookingsInRange
            .Select(x => new { x.Id, Date = DateOnly.FromDateTime(x.StartAt.UtcDateTime) })
            .ToList();

        if (bookingIds.Count == 0)
        {
            return FillDailyPoints(from, to, new Dictionary<DateOnly, decimal>());
        }

        var idSet = bookingIds.Select(x => x.Id).ToHashSet();
        var lineTotals = await dbContext.BookingLines.AsNoTracking()
            .Where(x => x.TenantId == tenantId && idSet.Contains(x.BookingId))
            .GroupBy(x => x.BookingId)
            .Select(g => new { BookingId = g.Key, Total = g.Sum(x => x.PriceAmount) })
            .ToListAsync(cancellationToken);

        var dateByBooking = bookingIds.ToDictionary(x => x.Id, x => x.Date);
        var byDate = new Dictionary<DateOnly, decimal>();
        foreach (var row in lineTotals)
        {
            if (!dateByBooking.TryGetValue(row.BookingId, out var date))
            {
                continue;
            }

            byDate[date] = byDate.GetValueOrDefault(date) + row.Total;
        }

        return FillDailyPoints(from, to, byDate);
    }

    private static IReadOnlyList<TenantDashboardSeriesPoint> FillDailyPoints(
        DateTimeOffset from,
        DateTimeOffset to,
        Dictionary<DateOnly, decimal> values)
    {
        var start = DateOnly.FromDateTime(from.UtcDateTime);
        var end = DateOnly.FromDateTime(to.UtcDateTime.AddDays(-1));
        if (end < start)
        {
            end = start;
        }

        var points = new List<TenantDashboardSeriesPoint>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            points.Add(new TenantDashboardSeriesPoint(day, values.GetValueOrDefault(day)));
        }

        return points;
    }

    private async Task<IReadOnlyList<TenantDashboardBreakdownItem>> BuildServiceBreakdownAsync(
        IQueryable<BookingRecord> bookingQuery,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await bookingQuery
            .GroupBy(x => x.ServiceOfferingId)
            .Select(g => new { ServiceId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<TenantDashboardBreakdownItem>();
        }

        var serviceIds = rows.Select(x => x.ServiceId).ToArray();
        var names = await dbContext.ServiceOfferings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return rows
            .Select(row => new TenantDashboardBreakdownItem(
                row.ServiceId.ToString(),
                names.GetValueOrDefault(row.ServiceId, "Service"),
                row.Count))
            .ToArray();
    }

    private async Task<IReadOnlyList<TenantDashboardBreakdownItem>> BuildStaffBreakdownAsync(
        IQueryable<BookingRecord> bookingQuery,
        CancellationToken cancellationToken)
    {
        var rows = await bookingQuery
            .Where(x => x.StaffMemberId != null)
            .GroupBy(x => x.StaffMemberId!.Value)
            .Select(g => new { StaffId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<TenantDashboardBreakdownItem>();
        }

        var staffIds = rows.Select(x => x.StaffId).ToArray();
        var names = await dbContext.StaffMembers.AsNoTracking()
            .Where(x => staffIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        return rows
            .Select(row => new TenantDashboardBreakdownItem(
                row.StaffId.ToString(),
                names.GetValueOrDefault(row.StaffId, "Staff"),
                row.Count))
            .ToArray();
    }

    public async Task<Result<TenantDashboardLayoutDto>> GetLayoutAsync(
        Guid tenantId,
        Guid businessUserId,
        bool practitionerView,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<TenantDashboardLayoutDto>(Error.NotFound("Tenant"));
        }

        var membership = await dbContext.BusinessUsers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == businessUserId && x.TenantId == tenantId, cancellationToken);
        if (membership is null)
        {
            return Result.Failure<TenantDashboardLayoutDto>(Error.NotFound("Business user"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        var fallback = DashboardLayoutDefaults.ForPractitionerView(practitionerView);

        var stored = await dbContext.BusinessUserDashboardLayouts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessUserId == businessUserId, cancellationToken);

        var parsed = stored is null ? null : ParseLayout(stored.LayoutJson);
        var normalized = DashboardLayoutNormalizer.Normalize(parsed, fallback, entitlements, practitionerView);
        return Result.Success(normalized);
    }

    public async Task<Result<TenantDashboardLayoutDto>> SaveLayoutAsync(
        Guid tenantId,
        Guid businessUserId,
        TenantDashboardLayoutDto layout,
        bool practitionerView,
        CancellationToken cancellationToken = default)
    {
        if (layout.Version != 1 || layout.Widgets.Count == 0)
        {
            return Result.Failure<TenantDashboardLayoutDto>(
                Error.Validation("Invalid dashboard layout payload."));
        }

        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<TenantDashboardLayoutDto>(Error.NotFound("Tenant"));
        }

        var membership = await dbContext.BusinessUsers
            .FirstOrDefaultAsync(x => x.Id == businessUserId && x.TenantId == tenantId, cancellationToken);
        if (membership is null)
        {
            return Result.Failure<TenantDashboardLayoutDto>(Error.NotFound("Business user"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        var fallback = DashboardLayoutDefaults.ForPractitionerView(practitionerView);
        var normalized = DashboardLayoutNormalizer.Normalize(layout, fallback, entitlements, practitionerView);

        var json = JsonSerializer.Serialize(normalized, LayoutJsonOptions);
        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.BusinessUserDashboardLayouts
            .FirstOrDefaultAsync(x => x.BusinessUserId == businessUserId, cancellationToken);

        if (existing is null)
        {
            dbContext.BusinessUserDashboardLayouts.Add(new BusinessUserDashboardLayout
            {
                BusinessUserId = businessUserId,
                TenantId = tenantId,
                LayoutJson = json,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.LayoutJson = json;
            existing.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(normalized);
    }

    private static TenantDashboardLayoutDto? ParseLayout(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<TenantDashboardLayoutDto>(json, LayoutJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

}
