namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Security;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class TenantCustomerService(AdeniDbContext dbContext) : ITenantCustomerService
{
    public async Task<Result<TenantCustomerUpsertResult>> EnsureFromBookingAsync(
        Guid tenantId,
        Guid platformCustomerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == platformCustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<TenantCustomerUpsertResult>(Error.NotFound("Customer"));
        }

        var phone = PhoneNormalizer.Normalize(customer.Phone);
        var now = DateTimeOffset.UtcNow;
        var (first, last) = SplitDisplayName(customer.Name);

        var row = await FindForBookingAsync(tenantId, platformCustomerId, phone, cancellationToken);
        if (row is null)
        {
            row = new TenantCustomer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = platformCustomerId,
                NormalizedPhone = phone,
                DisplayName = string.IsNullOrWhiteSpace(customer.Name) ? "Customer" : customer.Name.Trim(),
                FirstName = first,
                LastName = last,
                Email = NormalizeEmailOrNull(customer.Email),
                CreatedAt = now,
                UpdatedAt = now,
                LastSeenAt = now,
                LastBookingAt = now,
            };
            dbContext.TenantCustomers.Add(row);
        }
        else
        {
            row.CustomerId ??= platformCustomerId;
            if (phone is not null && string.IsNullOrWhiteSpace(row.NormalizedPhone))
            {
                row.NormalizedPhone = phone;
            }

            ApplyPlatformProfile(row, customer, first, last);
            row.LastBookingAt = now;
            row.LastSeenAt = now;
            row.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new TenantCustomerUpsertResult(row.Id, platformCustomerId));
    }

    public async Task<Result<TenantCustomerUpsertResult>> UpsertFromWalkInAsync(
        Guid tenantId,
        string displayName,
        string phone,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = PhoneNormalizer.Normalize(phone);
        if (string.IsNullOrWhiteSpace(displayName?.Trim()) || normalizedPhone is null)
        {
            return Result.Failure<TenantCustomerUpsertResult>(
                Error.Validation("Name and phone are required."));
        }

        var name = displayName.Trim();
        if (name.Length > 200)
        {
            name = name[..200];
        }

        var normalizedEmail = NormalizeEmailOrNull(email);
        var now = DateTimeOffset.UtcNow;
        var (first, last) = SplitDisplayName(name);

        var platformCustomer = await dbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Phone == normalizedPhone, cancellationToken);

        var row = await FindForWalkInAsync(
            tenantId,
            normalizedPhone,
            platformCustomer?.Id,
            cancellationToken);

        Guid? linkedPlatformCustomerId = platformCustomer?.Id;

        if (row is null)
        {
            row = new TenantCustomer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = linkedPlatformCustomerId,
                NormalizedPhone = normalizedPhone,
                DisplayName = name,
                FirstName = first,
                LastName = last,
                Email = normalizedEmail,
                CreatedAt = now,
                UpdatedAt = now,
                LastSeenAt = now,
                LastQueueJoinAt = now,
            };
            dbContext.TenantCustomers.Add(row);
        }
        else
        {
            row.DisplayName = name;
            row.FirstName = first;
            row.LastName = last;
            if (normalizedEmail is not null && string.IsNullOrWhiteSpace(row.Email))
            {
                row.Email = normalizedEmail;
            }

            row.CustomerId ??= linkedPlatformCustomerId;
            row.LastQueueJoinAt = now;
            row.LastSeenAt = now;
            row.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new TenantCustomerUpsertResult(row.Id, row.CustomerId));
    }

    public async Task<Result<TenantCustomerDetailResponse>> GetByIdAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.TenantCustomers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantCustomerId && x.TenantId == tenantId, cancellationToken);
        if (row is null)
        {
            return Result.Failure<TenantCustomerDetailResponse>(Error.NotFound("Customer"));
        }

        var recentBookings = row.CustomerId is Guid platformCustomerId
            ? await dbContext.Bookings.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.CustomerId == platformCustomerId)
                .OrderByDescending(x => x.StartAt)
                .Select(x => x.Id)
                .Take(5)
                .ToListAsync(cancellationToken)
            : [];

        var recentTickets = await dbContext.QueueTickets.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId)
            .OrderByDescending(x => x.JoinedAt)
            .Select(x => x.Id)
            .Take(5)
            .ToListAsync(cancellationToken);

        return Result.Success(MapDetail(row, recentBookings, recentTickets));
    }

    public async Task<Result<TenantCustomerListResponse>> ListForTenantAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.TenantCustomers.AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var phoneTerm = PhoneNormalizer.Normalize(term) ?? term;
            query = query.Where(x =>
                x.DisplayName.Contains(term)
                || (x.NormalizedPhone != null && x.NormalizedPhone.Contains(phoneTerm))
                || (x.Email != null && x.Email.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.LastSeenAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var mapped = items.Select(MapListItem).ToList();
        return Result.Success(new TenantCustomerListResponse(mapped, total, page, pageSize));
    }

    private async Task<TenantCustomer?> FindForBookingAsync(
        Guid tenantId,
        Guid platformCustomerId,
        string? phone,
        CancellationToken cancellationToken)
    {
        var byCustomer = await dbContext.TenantCustomers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerId == platformCustomerId, cancellationToken);
        if (byCustomer is not null)
        {
            return byCustomer;
        }

        if (phone is null)
        {
            return null;
        }

        return await dbContext.TenantCustomers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.NormalizedPhone == phone, cancellationToken);
    }

    private async Task<TenantCustomer?> FindForWalkInAsync(
        Guid tenantId,
        string normalizedPhone,
        Guid? platformCustomerId,
        CancellationToken cancellationToken)
    {
        var byPhone = await dbContext.TenantCustomers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.NormalizedPhone == normalizedPhone, cancellationToken);
        if (byPhone is not null)
        {
            return byPhone;
        }

        if (platformCustomerId is not Guid linkedId)
        {
            return null;
        }

        return await dbContext.TenantCustomers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerId == linkedId, cancellationToken);
    }

    private static void ApplyPlatformProfile(
        TenantCustomer row,
        Customer customer,
        string? first,
        string? last)
    {
        if (string.IsNullOrWhiteSpace(row.DisplayName) && !string.IsNullOrWhiteSpace(customer.Name))
        {
            row.DisplayName = customer.Name.Trim();
        }

        if (string.IsNullOrWhiteSpace(row.FirstName) && first is not null)
        {
            row.FirstName = first;
        }

        if (string.IsNullOrWhiteSpace(row.LastName) && last is not null)
        {
            row.LastName = last;
        }

        if (string.IsNullOrWhiteSpace(row.Email) && !string.IsNullOrWhiteSpace(customer.Email))
        {
            row.Email = NormalizeEmailOrNull(customer.Email);
        }
    }

    private static TenantCustomerListItemResponse MapListItem(TenantCustomer row) =>
        new(
            row.Id,
            row.DisplayName,
            PiiMasker.MaskPhone(row.NormalizedPhone),
            row.Email,
            row.LastSeenAt,
            row.CustomerId);

    private static TenantCustomerDetailResponse MapDetail(
        TenantCustomer row,
        IReadOnlyList<Guid> recentBookings,
        IReadOnlyList<Guid> recentTickets) =>
        new(
            row.Id,
            row.DisplayName,
            PiiMasker.MaskPhone(row.NormalizedPhone),
            row.Email,
            row.LastSeenAt,
            row.LastBookingAt,
            row.LastQueueJoinAt,
            row.CustomerId,
            recentBookings,
            recentTickets);

    private static (string? First, string? Last) SplitDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return (null, null);
        }

        var parts = displayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (TrimField(parts[0]), null),
            _ => (TrimField(parts[0]), TrimField(parts[1])),
        };
    }

    private static string? TrimField(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.Length > 100 ? trimmed[..100] : trimmed;
    }

    private static string? NormalizeEmailOrNull(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        return trimmed.Length > 320 ? trimmed[..320] : trimmed.ToLowerInvariant();
    }
}
