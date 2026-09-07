namespace Adeni.Infrastructure.Booking;

using System.Text.Json;
using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class QuoteRequestService(AdeniDbContext dbContext) : IQuoteRequestService
{
    private const int MaxPhotoKeys = 5;

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

        var customer = await GetOrCreateCustomerAsync(customerAuth0Sub, cancellationToken);

        var record = new QuoteRequestRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customer.Id,
            Description = request.Description.Trim(),
            ServiceAddress = string.IsNullOrWhiteSpace(request.ServiceAddress)
                ? null
                : request.ServiceAddress.Trim(),
            PhotoKeysJson = SerializePhotoKeys(request.PhotoKeys),
            Status = QuoteRequestStatus.Submitted,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.QuoteRequests.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapAsync(record, cancellationToken));
    }

    public async Task<IReadOnlyList<QuoteRequestResponse>> ListForCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return [];
        }

        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Auth0Sub == customerAuth0Sub, cancellationToken);

        if (customer is null)
        {
            return [];
        }

        var records = await dbContext.QuoteRequests
            .AsNoTracking()
            .Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var results = new List<QuoteRequestResponse>(records.Count);
        foreach (var record in records)
        {
            results.Add(await MapAsync(record, cancellationToken));
        }

        return results;
    }

    public async Task<IReadOnlyList<QuoteRequestResponse>> ListForTenantAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (!await HasBusinessAccessAsync(tenantId, auth0Sub, cancellationToken))
        {
            return [];
        }

        var records = await dbContext.QuoteRequests
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var results = new List<QuoteRequestResponse>(records.Count);
        foreach (var record in records)
        {
            results.Add(await MapAsync(record, cancellationToken));
        }

        return results;
    }

    public async Task<Result<QuoteRequestResponse>> SubmitOfferAsync(
        Guid tenantId,
        string auth0Sub,
        Guid quoteRequestId,
        SubmitQuoteOfferRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await HasBusinessAccessAsync(tenantId, auth0Sub, cancellationToken))
        {
            return Result.Failure<QuoteRequestResponse>(Error.Forbidden("You do not have access to this business."));
        }

        if (request.Amount <= 0)
        {
            return Result.Failure<QuoteRequestResponse>(Error.Validation("Quote amount must be greater than zero."));
        }

        if (request.ProposedEndAt <= request.ProposedStartAt)
        {
            return Result.Failure<QuoteRequestResponse>(Error.Validation("Proposed end time must be after start time."));
        }

        var record = await dbContext.QuoteRequests
            .FirstOrDefaultAsync(x => x.Id == quoteRequestId && x.TenantId == tenantId, cancellationToken);

        if (record is null)
        {
            return Result.Failure<QuoteRequestResponse>(Error.NotFound("Quote request"));
        }

        if (record.Status is not QuoteRequestStatus.Submitted)
        {
            return Result.Failure<QuoteRequestResponse>(Error.Validation("Only submitted quote requests can receive an offer."));
        }

        var serviceExists = await dbContext.ServiceOfferings
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == request.ServiceOfferingId && x.TenantId == tenantId && x.IsActive,
                cancellationToken);

        if (!serviceExists)
        {
            return Result.Failure<QuoteRequestResponse>(Error.NotFound("Service"));
        }

        record.Status = QuoteRequestStatus.Quoted;
        record.QuotedAmount = request.Amount;
        record.QuotedCurrency = request.Currency.Trim().ToUpperInvariant();
        record.QuoteNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        record.ServiceOfferingId = request.ServiceOfferingId;
        record.ProposedStartAt = request.ProposedStartAt;
        record.ProposedEndAt = request.ProposedEndAt;
        record.QuotedAt = DateTimeOffset.UtcNow;
        record.ExpiresAt = request.ExpiresAt ?? DateTimeOffset.UtcNow.AddDays(7);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(record, cancellationToken));
    }

    public async Task<Result<QuoteRequestResponse>> AcceptAsync(
        string customerAuth0Sub,
        Guid quoteRequestId,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Auth0Sub == customerAuth0Sub, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<QuoteRequestResponse>(Error.Forbidden("Customer authentication is required."));
        }

        var record = await dbContext.QuoteRequests
            .FirstOrDefaultAsync(x => x.Id == quoteRequestId && x.CustomerId == customer.Id, cancellationToken);

        if (record is null)
        {
            return Result.Failure<QuoteRequestResponse>(Error.NotFound("Quote request"));
        }

        if (record.Status != QuoteRequestStatus.Quoted)
        {
            return Result.Failure<QuoteRequestResponse>(Error.Validation("Only quoted requests can be accepted."));
        }

        if (record.ExpiresAt is { } expiresAt && expiresAt < DateTimeOffset.UtcNow)
        {
            record.Status = QuoteRequestStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<QuoteRequestResponse>(Error.Validation("This quote has expired."));
        }

        if (record.ServiceOfferingId is null
            || record.ProposedStartAt is null
            || record.ProposedEndAt is null)
        {
            return Result.Failure<QuoteRequestResponse>(Error.Validation("Quote is missing booking details."));
        }

        var booking = new BookingRecord
        {
            Id = Guid.NewGuid(),
            TenantId = record.TenantId,
            CustomerId = record.CustomerId,
            ServiceOfferingId = record.ServiceOfferingId.Value,
            StartAt = record.ProposedStartAt.Value,
            EndAt = record.ProposedEndAt.Value,
            Status = BookingStatus.Pending,
            CustomerNotes = $"Accepted quote {record.Id}",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.Bookings.Add(booking);
        record.Status = QuoteRequestStatus.Accepted;
        record.BookingId = booking.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapAsync(record, cancellationToken));
    }

    public async Task<Result<QuoteRequestResponse>> DeclineAsync(
        string customerAuth0Sub,
        Guid quoteRequestId,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Auth0Sub == customerAuth0Sub, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<QuoteRequestResponse>(Error.Forbidden("Customer authentication is required."));
        }

        var record = await dbContext.QuoteRequests
            .FirstOrDefaultAsync(x => x.Id == quoteRequestId && x.CustomerId == customer.Id, cancellationToken);

        if (record is null)
        {
            return Result.Failure<QuoteRequestResponse>(Error.NotFound("Quote request"));
        }

        if (record.Status != QuoteRequestStatus.Quoted)
        {
            return Result.Failure<QuoteRequestResponse>(Error.Validation("Only quoted requests can be declined."));
        }

        record.Status = QuoteRequestStatus.Declined;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(record, cancellationToken));
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(x => x.Auth0Sub == customerAuth0Sub, cancellationToken);

        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer
        {
            Id = Guid.NewGuid(),
            Auth0Sub = customerAuth0Sub,
            Name = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private async Task<bool> HasBusinessAccessAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken) =>
        await dbContext.BusinessUsers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, cancellationToken);

    private Task<QuoteRequestResponse> MapAsync(
        QuoteRequestRecord record,
        CancellationToken cancellationToken) =>
        Task.FromResult(new QuoteRequestResponse(
            record.Id,
            record.TenantId,
            record.Description,
            record.ServiceAddress,
            DeserializePhotoKeys(record.PhotoKeysJson),
            record.Status.ToString().ToLowerInvariant(),
            record.QuotedAmount,
            record.QuotedCurrency,
            record.QuoteNotes,
            record.ServiceOfferingId,
            record.ProposedStartAt,
            record.ProposedEndAt,
            record.ExpiresAt,
            record.BookingId,
            record.CreatedAt));

    private static string? SerializePhotoKeys(IReadOnlyList<string>? photoKeys)
    {
        if (photoKeys is null || photoKeys.Count == 0)
        {
            return null;
        }

        var keys = photoKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Take(MaxPhotoKeys)
            .ToList();

        return keys.Count == 0 ? null : JsonSerializer.Serialize(keys);
    }

    private static IReadOnlyList<string> DeserializePhotoKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
