namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Common;
using Adeni.Application.Security;
using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class TenantCustomerService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService) : ITenantCustomerService
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

        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        var entitlements = tenant is null
            ? SubscriptionEntitlements.ForSku(SubscriptionSku.Starter)
            : entitlementsService.GetEntitlementsForTenant(tenant);
        var historyLimit = entitlements.CustomerRetentionAnalytics ? 25 : 3;

        var recentBookingRows = row.CustomerId is Guid platformCustomerId
            ? await LoadRecentBookingsAsync(tenantId, platformCustomerId, historyLimit, cancellationToken)
            : [];

        var recentQueueRows = await LoadRecentQueueVisitsAsync(
            tenantId,
            tenantCustomerId,
            cancellationToken);

        return Result.Success(MapDetail(row, recentBookingRows, recentQueueRows));
    }

    public async Task<Result<TenantCustomerListResponse>> ListForTenantAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        string? discoverFilter = null,
        string? sortBy = null,
        ListSortDirection? sortDirection = null,
        Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var scoped = await TenantLocationScope.ValidateOptionalLocationIdAsync(
            dbContext,
            tenantId,
            locationId,
            cancellationToken);
        if (scoped.IsFailure)
        {
            return Result.Failure<TenantCustomerListResponse>(scoped.Error);
        }

        locationId = scoped.Value;

        if (!string.IsNullOrWhiteSpace(discoverFilter))
        {
            var tenant = await dbContext.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
            if (tenant is not null)
            {
                var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
                if (!entitlements.CustomerRetentionAnalytics)
                {
                    return Result.Failure<TenantCustomerListResponse>(
                        ErrorCodes.CustomerSegmentationNotEntitledError());
                }
            }
        }

        var query = dbContext.TenantCustomers.AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (locationId is Guid branchId)
        {
            query = query.Where(tc =>
                dbContext.Bookings.Any(b =>
                    b.TenantId == tenantId
                    && b.BusinessLocationId == branchId
                    && tc.CustomerId != null
                    && b.CustomerId == tc.CustomerId)
                || dbContext.QueueTickets.Any(q =>
                    q.TenantId == tenantId
                    && q.BusinessLocationId == branchId
                    && q.TenantCustomerId == tc.Id));
        }

        if (string.Equals(discoverFilter, "discover", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.CustomerId != null);
        }
        else if (string.Equals(discoverFilter, "walk_in", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.CustomerId == null);
        }

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
        var field = sortBy ?? TenantCustomerListSort.DefaultField;
        var direction = sortDirection ?? TenantCustomerListSort.DefaultDirection;
        var sorted = ApplyListSort(query, field, direction);
        var items = await sorted
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

    private async Task<IReadOnlyList<TenantCustomerRecentBookingResponse>> LoadRecentBookingsAsync(
        Guid tenantId,
        Guid platformCustomerId,
        int limit,
        CancellationToken cancellationToken)
    {
        var bookings = await dbContext.Bookings.AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.ServiceOffering)
            .Include(x => x.StaffMember)
            .Where(x => x.TenantId == tenantId && x.CustomerId == platformCustomerId)
            .OrderByDescending(x => x.StartAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return bookings
            .Select(booking =>
            {
                var serviceName = booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName
                    ?? booking.ServiceOffering?.Name
                    ?? "Service";
                return new TenantCustomerRecentBookingResponse(
                    booking.Id,
                    serviceName,
                    booking.StartAt,
                    (int)booking.Status,
                    booking.StaffMember?.DisplayName);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<TenantCustomerRecentQueueVisitResponse>> LoadRecentQueueVisitsAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        CancellationToken cancellationToken)
    {
        var tickets = await dbContext.QueueTickets.AsNoTracking()
            .Include(x => x.ServiceOffering)
            .Include(x => x.StaffMember)
            .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId)
            .OrderByDescending(x => x.JoinedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        return tickets
            .Select(ticket => new TenantCustomerRecentQueueVisitResponse(
                ticket.Id,
                ticket.JoinedAt,
                (int)ticket.Status,
                ticket.ServiceOffering?.Name,
                ticket.StaffMember?.DisplayName))
            .ToList();
    }

    private static TenantCustomerDetailResponse MapDetail(
        TenantCustomer row,
        IReadOnlyList<TenantCustomerRecentBookingResponse> recentBookings,
        IReadOnlyList<TenantCustomerRecentQueueVisitResponse> recentQueueVisits) =>
        new(
            row.Id,
            row.DisplayName,
            PiiMasker.MaskPhone(row.NormalizedPhone),
            row.Email,
            row.LastSeenAt,
            row.LastBookingAt,
            row.LastQueueJoinAt,
            row.CustomerId,
            recentBookings.Select(x => x.Id).ToList(),
            recentQueueVisits.Select(x => x.Id).ToList(),
            recentBookings,
            recentQueueVisits);

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

    private static IOrderedQueryable<TenantCustomer> ApplyListSort(
        IQueryable<TenantCustomer> query,
        string field,
        ListSortDirection direction)
    {
        var desc = ListSort.IsDescending(direction);
        return field switch
        {
            "name" => desc
                ? query.OrderByDescending(x => x.DisplayName)
                : query.OrderBy(x => x.DisplayName),
            "email" => desc
                ? query.OrderByDescending(x => x.Email ?? string.Empty)
                : query.OrderBy(x => x.Email ?? string.Empty),
            "account" => desc
                ? query.OrderByDescending(x => x.CustomerId != null)
                : query.OrderBy(x => x.CustomerId != null),
            _ => desc
                ? query.OrderByDescending(x => x.LastSeenAt)
                : query.OrderBy(x => x.LastSeenAt),
        };
    }
}
