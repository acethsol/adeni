namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class WaitlistService(AdeniDbContext dbContext) : IWaitlistService
{
    public async Task<Result<WaitlistEntryResponse>> JoinAsync(
        string customerAuth0Sub,
        JoinWaitlistRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<WaitlistEntryResponse>(Error.Forbidden("Customer authentication is required."));
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.TenantId && x.Status == TenantStatus.Verified,
                cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<WaitlistEntryResponse>(Error.NotFound("Business"));
        }

        var service = await dbContext.ServiceOfferings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.ServiceOfferingId
                    && x.TenantId == request.TenantId
                    && x.IsActive,
                cancellationToken);

        if (service is null)
        {
            return Result.Failure<WaitlistEntryResponse>(Error.NotFound("Service"));
        }

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(x => x.Auth0Sub == customerAuth0Sub, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Auth0Sub = customerAuth0Sub,
                Name = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var entry = new WaitlistEntry
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ServiceOfferingId = request.ServiceOfferingId,
            CustomerId = customer.Id,
            PreferredFrom = request.PreferredFrom,
            PreferredTo = request.PreferredTo,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.WaitlistEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new WaitlistEntryResponse(
            entry.Id,
            entry.TenantId,
            entry.ServiceOfferingId,
            entry.PreferredFrom,
            entry.PreferredTo,
            entry.CreatedAt));
    }
}
