namespace Adeni.Infrastructure.Dashboard;

using Adeni.Application.Dashboard;
using Adeni.Application.Reporting;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Adeni.Application.Security;
using Microsoft.EntityFrameworkCore;

public sealed class TenantReportExportQueryService(
    AdeniDbContext dbContext,
    ITenantDashboardService dashboardService) : ITenantReportExportQueryService
{
    public async Task<Result<IReadOnlyList<BookingsRegisterExportRow>>> GetBookingsRegisterAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId,
        Guid? locationId,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            return Result.Failure<IReadOnlyList<BookingsRegisterExportRow>>(
                Error.Validation("'to' must be on or after 'from'."));
        }

        var query = dbContext.Bookings
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.StartAt >= from
                && x.StartAt <= to);

        if (staffMemberId is { } staffId)
        {
            query = query.Where(x => x.StaffMemberId == staffId);
        }

        _ = locationId;

        var bookings = await query
            .Include(x => x.Lines)
            .Include(x => x.ServiceOffering)
            .Include(x => x.StaffMember)
            .Include(x => x.Guests)
            .OrderBy(x => x.StartAt)
            .ToListAsync(cancellationToken);

        var customerIds = bookings.Select(x => x.CustomerId).Distinct().ToArray();
        var customers = customerIds.Length == 0
            ? new Dictionary<Guid, Customer>()
            : await dbContext.Customers.AsNoTracking()
                .Where(x => customerIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        IReadOnlyList<BookingsRegisterExportRow> rows = bookings
            .Select(booking =>
            {
                var serviceName = booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName
                    ?? booking.ServiceOffering?.Name
                    ?? "Service";
                customers.TryGetValue(booking.CustomerId, out var customer);
                var guestName = booking.Guests?
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.DisplayName)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                var customerName = guestName
                    ?? customer?.Name;
                var total = booking.Lines.Sum(x => x.PriceAmount);
                var currency = booking.ServiceOffering?.Currency;
                return new BookingsRegisterExportRow(
                    booking.Id,
                    booking.StartAt,
                    booking.EndAt,
                    booking.Status.ToString(),
                    serviceName,
                    customerName,
                    booking.StaffMember?.DisplayName,
                    total > 0 ? total : null,
                    currency);
            })
            .ToArray();

        return Result.Success(rows);
    }

    public async Task<Result<SalesSummaryExportRow[]>> GetSalesSummaryAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId,
        CancellationToken cancellationToken = default)
    {
        var summary = await dashboardService.GetSummaryAsync(
            tenantId,
            from,
            to,
            staffMemberId,
            cancellationToken);

        if (summary.IsFailure)
        {
            return Result.Failure<SalesSummaryExportRow[]>(summary.Error!);
        }

        var value = summary.Value!;
        var rows = new List<SalesSummaryExportRow>
        {
            new("pending_count", value.PendingCount.ToString(), null),
            new("appointments_count", value.AppointmentsCount.ToString(), null),
        };

        if (value.BookedValueAmount is { } booked)
        {
            rows.Add(new("booked_value_amount", booked.ToString("0.##"), value.Currency));
        }

        if (value.CollectedAmount is { } collected)
        {
            rows.Add(new("collected_amount", collected.ToString("0.##"), value.Currency));
        }

        return Result.Success(rows.ToArray());
    }

    public async Task<Result<CustomerStatementData>> GetCustomerStatementAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            return Result.Failure<CustomerStatementData>(
                Error.Validation("'to' must be on or after 'from'."));
        }

        var customer = await dbContext.TenantCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantCustomerId && x.TenantId == tenantId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerStatementData>(Error.NotFound("Customer"));
        }

        var businessName = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Business";

        var bookingLines = new List<CustomerStatementLine>();
        if (customer.CustomerId is Guid platformCustomerId)
        {
            var bookings = await dbContext.Bookings
                .AsNoTracking()
                .Include(x => x.Lines)
                .Include(x => x.ServiceOffering)
                .Where(x =>
                    x.TenantId == tenantId
                    && x.CustomerId == platformCustomerId
                    && x.StartAt >= from
                    && x.StartAt <= to
                    && x.Status != BookingStatus.Cancelled)
                .OrderBy(x => x.StartAt)
                .ToListAsync(cancellationToken);

            bookingLines.AddRange(bookings.Select(booking =>
            {
                var serviceName = booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName
                    ?? booking.ServiceOffering?.Name
                    ?? "Service";
                var total = booking.Lines.Sum(x => x.PriceAmount);
                return new CustomerStatementLine(
                    booking.StartAt,
                    serviceName,
                    total > 0 ? total : null,
                    booking.ServiceOffering?.Currency);
            }));

            var paymentLines = await dbContext.PaymentIntents
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId
                    && x.Status == PaymentIntentStatus.Completed
                    && x.UpdatedAt >= from
                    && x.UpdatedAt <= to
                    && x.BookingId != null)
                .Join(
                    dbContext.Bookings.AsNoTracking().Where(b => b.CustomerId == platformCustomerId),
                    payment => payment.BookingId,
                    booking => (Guid?)booking.Id,
                    (payment, _) => payment)
                .OrderBy(x => x.UpdatedAt)
                .Select(x => new CustomerStatementLine(
                    x.UpdatedAt,
                    "Payment",
                    x.Amount,
                    x.Currency))
                .ToListAsync(cancellationToken);

            bookingLines.AddRange(paymentLines);
        }

        var lines = bookingLines
            .OrderBy(x => x.OccurredAt)
            .ToArray();

        return Result.Success(new CustomerStatementData(
            businessName,
            customer.DisplayName,
            PiiMasker.MaskPhone(customer.NormalizedPhone),
            from,
            to,
            lines));
    }

    public Task<int> GetActiveLocationCountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        dbContext.BusinessLocations
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

    public async Task<Result<IReadOnlyList<StaffRosterExportRow>>> GetStaffRosterAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var staff = await dbContext.StaffMembers
            .AsNoTracking()
            .Include(x => x.ServiceLinks)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        var staffIds = staff.Select(x => x.Id).ToArray();
        var now = DateTimeOffset.UtcNow;

        var linkedUsers = await dbContext.BusinessUsers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StaffMemberId != null && staffIds.Contains(x.StaffMemberId.Value))
            .Select(x => x.StaffMemberId)
            .ToListAsync(cancellationToken);

        var pendingInvites = await dbContext.StaffPortalInvites
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.StaffMemberId != null
                && staffIds.Contains(x.StaffMemberId.Value)
                && x.Status == StaffPortalInviteStatuses.Pending
                && x.ExpiresAt >= now)
            .Select(x => x.StaffMemberId)
            .ToListAsync(cancellationToken);

        var linkedStaffIds = linkedUsers.Where(x => x is not null).Select(x => x!.Value).ToHashSet();
        var invitedStaffIds = pendingInvites.Where(x => x is not null).Select(x => x!.Value).ToHashSet();

        IReadOnlyList<StaffRosterExportRow> rows = staff
            .Select(member =>
            {
                var accessStatus = linkedStaffIds.Contains(member.Id)
                    ? PortalAccessStatuses.Active
                    : invitedStaffIds.Contains(member.Id)
                        ? PortalAccessStatuses.InvitePending
                        : PortalAccessStatuses.None;

                return new StaffRosterExportRow(
                    member.DisplayName,
                    member.RoleKey,
                    member.IsActive,
                    accessStatus,
                    member.ServiceLinks?.Count ?? 0,
                    member.SortOrder);
            })
            .ToArray();

        return Result.Success(rows);
    }

    public async Task<Result<IReadOnlyList<TenantCustomerExportRow>>> GetTenantCustomersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.TenantCustomers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.DisplayName)
            .Select(x => new TenantCustomerExportRow(
                x.Id,
                x.DisplayName,
                PiiMasker.MaskPhone(x.NormalizedPhone),
                x.Email,
                x.CustomerId != null ? "Discover account" : "Walk-in / queue",
                x.LastSeenAt,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TenantCustomerExportRow>>(rows);
    }

    public async Task<Result<IReadOnlyList<TenantLocationExportRow>>> GetTenantLocationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Name)
            .Select(x => new TenantLocationExportRow(
                x.Slug,
                x.Name,
                x.MarketId,
                x.AddressLine,
                x.Area,
                x.IsPrimary,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TenantLocationExportRow>>(rows);
    }

    public async Task<Result<IReadOnlyList<TenantServiceExportRow>>> GetTenantServicesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new TenantServiceExportRow(
                x.Name,
                x.Description,
                x.PriceAmount,
                x.Currency,
                x.DurationMinutes,
                x.CategorySlug,
                x.IsActive,
                x.IsAddOn,
                x.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TenantServiceExportRow>>(rows);
    }
}
