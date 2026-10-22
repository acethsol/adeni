namespace Adeni.Infrastructure.Finance;

using System.Globalization;
using System.Text;
using System.Text.Json;
using Adeni.Application.Finance;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class StaffCompensationService(AdeniDbContext dbContext) : IStaffCompensationService
{
    public async Task<Result> RecordTipForTillPaymentAsync(
        Guid tenantId,
        Guid bookingId,
        decimal tipAmount,
        Guid? staffMemberId,
        string currency,
        CancellationToken cancellationToken = default)
    {
        if (tipAmount <= 0)
        {
            return Result.Success();
        }

        var booking = await dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tenantId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(Error.NotFound("Booking"));
        }

        var staffId = staffMemberId ?? booking.StaffMemberId;
        if (staffId is null)
        {
            return Result.Failure(Error.Validation("Tip requires a staff member on the booking or in the request."));
        }

        if (!await StaffBelongsToTenantAsync(tenantId, staffId.Value, cancellationToken))
        {
            return Result.Failure(Error.Validation("Staff member is not on this tenant roster."));
        }

        await AddAllocationAsync(
            tenantId,
            staffId.Value,
            bookingId,
            null,
            StaffCompensationType.Tip,
            tipAmount,
            NormalizeCurrency(currency),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RecordTipForPaymentIntentAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var intent = await dbContext.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentIntentId, cancellationToken);

        if (intent is null || intent.TipAmount is not > 0)
        {
            return Result.Success();
        }

        var staffId = intent.TipStaffMemberId;
        if (staffId is null && intent.BookingId is Guid bookingId)
        {
            staffId = await dbContext.Bookings
                .AsNoTracking()
                .Where(x => x.Id == bookingId && x.TenantId == intent.TenantId)
                .Select(x => x.StaffMemberId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (staffId is null)
        {
            return Result.Success();
        }

        var exists = await dbContext.StaffCompensationAllocations
            .AnyAsync(
                x => x.PaymentIntentId == paymentIntentId && x.Type == StaffCompensationType.Tip,
                cancellationToken);

        if (exists)
        {
            return Result.Success();
        }

        await AddAllocationAsync(
            intent.TenantId,
            staffId.Value,
            intent.BookingId,
            paymentIntentId,
            StaffCompensationType.Tip,
            intent.TipAmount.Value,
            intent.Currency,
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> AccrueCommissionForPaidBookingAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.StaffCompensationAllocations
            .AnyAsync(
                x => x.TenantId == tenantId
                     && x.BookingId == bookingId
                     && x.Type == StaffCompensationType.Commission,
                cancellationToken);

        if (exists)
        {
            return Result.Success();
        }

        var booking = await dbContext.Bookings
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tenantId, cancellationToken);

        if (booking is null || booking.StaffMemberId is not Guid staffId)
        {
            return Result.Success();
        }

        var profile = await dbContext.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        var percent = profile?.StaffCommissionPercent ?? 0;
        if (percent <= 0)
        {
            return Result.Success();
        }

        var serviceTotal = booking.Lines.Sum(x => x.PriceAmount);
        if (serviceTotal <= 0)
        {
            var servicePrice = await dbContext.ServiceOfferings
                .AsNoTracking()
                .Where(x => x.Id == booking.ServiceOfferingId)
                .Select(x => (decimal?)x.PriceAmount)
                .FirstOrDefaultAsync(cancellationToken);
            serviceTotal = servicePrice ?? 0m;
        }

        if (serviceTotal <= 0)
        {
            return Result.Success();
        }

        var commission = Math.Round(serviceTotal * percent / 100m, 2, MidpointRounding.AwayFromZero);
        if (commission <= 0)
        {
            return Result.Success();
        }

        var currency = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.Id == booking.ServiceOfferingId)
            .Select(x => x.Currency)
            .FirstOrDefaultAsync(cancellationToken) ?? "NGN";

        await AddAllocationAsync(
            tenantId,
            staffId,
            bookingId,
            null,
            StaffCompensationType.Commission,
            commission,
            NormalizeCurrency(currency),
            cancellationToken);

        return Result.Success();
    }

    public Task<Result<IReadOnlyList<StaffCompensationExportRow>>> ListExportRowsAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? locationId = null,
        CancellationToken cancellationToken = default) =>
        LoadExportRowsAsync(tenantId, from, to, locationId, cancellationToken);

    public async Task<Result<StaffCompensationExportPayload>> ExportAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CompensationExportFormat format,
        CancellationToken cancellationToken = default)
    {
        var rowsResult = await LoadExportRowsAsync(tenantId, from, to, locationId: null, cancellationToken);
        if (rowsResult.IsFailure)
        {
            return Result.Failure<StaffCompensationExportPayload>(rowsResult.Error!);
        }

        var mapped = rowsResult.Value!;

        if (format == CompensationExportFormat.Json)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(mapped, new JsonSerializerOptions { WriteIndented = true });
            return Result.Success(new StaffCompensationExportPayload(
                "application/json",
                $"compensation-{from:yyyyMMdd}-{to:yyyyMMdd}.json",
                json));
        }

        var csv = BuildCsv(mapped);
        return Result.Success(new StaffCompensationExportPayload(
            "text/csv",
            $"compensation-{from:yyyyMMdd}-{to:yyyyMMdd}.csv",
            Encoding.UTF8.GetBytes(csv)));
    }

    private async Task<Result<IReadOnlyList<StaffCompensationExportRow>>> LoadExportRowsAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return Result.Failure<IReadOnlyList<StaffCompensationExportRow>>(
                Error.Validation("'to' must be on or after 'from'."));
        }

        var allocationQuery = dbContext.StaffCompensationAllocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CreatedAt >= from && x.CreatedAt <= to);

        if (locationId is Guid branchId)
        {
            allocationQuery = allocationQuery.Where(x =>
                x.BookingId != null
                && dbContext.Bookings.Any(b =>
                    b.Id == x.BookingId
                    && b.TenantId == tenantId
                    && b.BusinessLocationId == branchId));
        }

        var rows = await allocationQuery
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var staffIds = rows.Select(x => x.StaffMemberId).Distinct().ToArray();
        var staffNames = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && staffIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        IReadOnlyList<StaffCompensationExportRow> mapped = rows.Select(row => new StaffCompensationExportRow(
            row.CreatedAt,
            row.StaffMemberId,
            staffNames.TryGetValue(row.StaffMemberId, out var name) ? name : row.StaffMemberId.ToString(),
            row.Type == StaffCompensationType.Tip ? "tip" : "commission",
            row.Amount,
            row.Currency,
            row.BookingId,
            row.PaymentIntentId)).ToArray();

        return Result.Success(mapped);
    }

    private async Task AddAllocationAsync(
        Guid tenantId,
        Guid staffMemberId,
        Guid? bookingId,
        Guid? paymentIntentId,
        StaffCompensationType type,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        dbContext.StaffCompensationAllocations.Add(new StaffCompensationAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StaffMemberId = staffMemberId,
            BookingId = bookingId,
            PaymentIntentId = paymentIntentId,
            Type = type,
            Amount = amount,
            Currency = currency,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> StaffBelongsToTenantAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        await dbContext.StaffMembers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == staffMemberId, cancellationToken);

    private static string NormalizeCurrency(string currency) =>
        string.IsNullOrWhiteSpace(currency) ? "NGN" : currency.Trim().ToUpperInvariant();

    private static string BuildCsv(IReadOnlyList<StaffCompensationExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("created_at,staff_id,staff_name,type,amount,currency,booking_id,payment_intent_id");
        foreach (var row in rows)
        {
            builder.Append(CsvEscape(row.CreatedAt.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(CsvEscape(row.StaffMemberId.ToString()));
            builder.Append(',');
            builder.Append(CsvEscape(row.StaffDisplayName));
            builder.Append(',');
            builder.Append(CsvEscape(row.Type));
            builder.Append(',');
            builder.Append(row.Amount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvEscape(row.Currency));
            builder.Append(',');
            builder.Append(CsvEscape(row.BookingId?.ToString() ?? ""));
            builder.Append(',');
            builder.AppendLine(CsvEscape(row.PaymentIntentId?.ToString() ?? ""));
        }

        return builder.ToString();
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
