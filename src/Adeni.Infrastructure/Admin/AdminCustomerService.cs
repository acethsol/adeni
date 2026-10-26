namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Abstractions;
using Adeni.Application.Admin;
using Adeni.Application.Intake;
using Adeni.Domain.Auditing;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminCustomerService(
    AdeniDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext,
    IIntakeRetentionService? intakeRetention = null,
    Adeni.Application.Tenancy.ICustomerSegmentsQueryService? segments = null) : IAdminCustomerService
{
    public async Task<Result<AdminPagedResult<AdminCustomerSummary>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paging = AdminPaging.Normalize(page, pageSize);
        if (paging.IsFailure)
        {
            return Result.Failure<AdminPagedResult<AdminCustomerSummary>>(paging.Error);
        }

        var (effectivePage, effectivePageSize) = paging.Value;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result.Success(new AdminPagedResult<AdminCustomerSummary>(
                [],
                effectivePage,
                effectivePageSize,
                0));
        }

        var normalized = query.Trim().ToLowerInvariant();
        var baseQuery = dbContext.Customers
            .AsNoTracking()
            .Where(c =>
                c.Name.ToLower().Contains(normalized)
                || c.Auth0Sub.ToLower().Contains(normalized)
                || (c.Email != null && c.Email.ToLower().Contains(normalized))
                || (c.Phone != null && c.Phone.ToLower().Contains(normalized)));

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderByDescending(c => c.CreatedAt)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(c => new AdminCustomerSummary(
                c.Id,
                c.Auth0Sub,
                c.Name,
                c.Email,
                c.CreatedAt,
                c.ErasureRequestedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new AdminPagedResult<AdminCustomerSummary>(
            items,
            effectivePage,
            effectivePageSize,
            totalCount));
    }

    public async Task<Result<CustomerDataExportResponse>> ExportAsync(
        Guid customerId,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerDataExportResponse>(Error.NotFound("Customer"));
        }

        var bookings = await (
            from booking in dbContext.Bookings.AsNoTracking()
            join service in dbContext.ServiceOfferings.AsNoTracking() on booking.ServiceOfferingId equals service.Id
            where booking.CustomerId == customerId
            orderby booking.StartAt descending
            select new CustomerBookingExportItem(
                booking.Id,
                booking.TenantId,
                service.Name,
                booking.StartAt,
                booking.EndAt,
                booking.Status.ToString(),
                booking.CustomerNotes,
                booking.CreatedAt))
            .ToListAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            AuditActions.CustomerExported,
            "customer",
            customerId.ToString(),
            correlationContext.CorrelationId,
            DateTimeOffset.UtcNow,
            null),
            cancellationToken);

        return Result.Success(new CustomerDataExportResponse(
            customer.Id,
            customer.Auth0Sub,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.CreatedAt,
            customer.ErasureRequestedAt,
            bookings,
            segments is null ? [] : await segments.ExportAsync(customerId, cancellationToken)));
    }

    public async Task<Result<Unit>> InitiateErasureAsync(
        Guid customerId,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Customer"));
        }

        if (customer.ErasureRequestedAt is not null)
        {
            return Result.Failure<Unit>(Error.Conflict("Erasure has already been requested for this customer."));
        }

        var now = DateTimeOffset.UtcNow;
        customer.ErasureRequestedAt = now;
        customer.Name = "[erased]";
        customer.Email = null;
        customer.Phone = null;
        if (intakeRetention is not null) await intakeRetention.StagePrivacyPurgeAsync(customerId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            AuditActions.CustomerDeleted,
            "customer",
            customerId.ToString(),
            correlationContext.CorrelationId,
            now,
            null),
            cancellationToken);

        return Result.Success(Unit.Value);
    }
}
