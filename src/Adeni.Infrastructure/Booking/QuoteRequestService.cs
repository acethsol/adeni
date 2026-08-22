namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class QuoteRequestService(AdeniDbContext dbContext) : IQuoteRequestService
{
    public async Task<Result<QuoteRequestResponse>> CreateBySlugAsync(
        string customerAuth0Sub,
        string slug,
        CreateQuoteRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<QuoteRequestResponse>(Error.Forbidden("Customer authentication is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 10)
        {
            return Result.Failure<QuoteRequestResponse>(
                Error.Validation("Please describe the job in at least 10 characters."));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var tenantId = await VerifiedLocationQueries.ResolveTenantIdBySlugAsync(
            dbContext,
            normalizedSlug,
            cancellationToken);

        if (tenantId == Guid.Empty)
        {
            return Result.Failure<QuoteRequestResponse>(Error.NotFound("Business"));
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

        var record = new QuoteRequestRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customer.Id,
            Description = request.Description.Trim(),
            ServiceAddress = string.IsNullOrWhiteSpace(request.ServiceAddress)
                ? null
                : request.ServiceAddress.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.QuoteRequests.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new QuoteRequestResponse(
            record.Id,
            record.TenantId,
            record.Description,
            record.ServiceAddress,
            record.CreatedAt));
    }
}
