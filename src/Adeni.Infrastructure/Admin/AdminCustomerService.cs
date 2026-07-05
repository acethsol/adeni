namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Abstractions;
using Adeni.Application.Admin;
using Adeni.Domain.Auditing;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminCustomerService(
    AdeniDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext) : IAdminCustomerService
{
    public async Task<IReadOnlyList<AdminCustomerSummary>> SearchAsync(
        string? email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Array.Empty<AdminCustomerSummary>();
        }

        var normalized = email.Trim().ToLowerInvariant();
        return await dbContext.Customers
            .AsNoTracking()
            .Where(c => c.Email != null && c.Email.ToLower() == normalized)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new AdminCustomerSummary(
                c.Id,
                c.Auth0Sub,
                c.Name,
                c.Email,
                c.CreatedAt,
                c.ErasureRequestedAt))
            .Take(20)
            .ToListAsync(cancellationToken);
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
            bookings));
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
