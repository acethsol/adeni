namespace Adeni.Infrastructure.Messaging;

using Adeni.Application.Messaging;
using Adeni.Application.Security;
using Adeni.Application.Subscriptions;
using Adeni.Application.Admin;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Messaging;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public sealed class MessageThreadService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService,
    IConfiguration configuration) : IMessageThreadService
{
    private const int MaxBodyLength = 4000;
    private const int PreviewLength = 120;

    public async Task<Result<ThreadSummaryResponse>> CreateOrGetThreadAsync(
        string customerAuth0Sub,
        CreateThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<ThreadSummaryResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.TenantId && x.Status == TenantStatus.Verified,
                cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<ThreadSummaryResponse>(Error.NotFound("Business"));
        }

        var customer = await GetOrCreateCustomerAsync(customerAuth0Sub, cancellationToken);

        if (request.BookingId is { } bookingId)
        {
            var bookingValid = await dbContext.Bookings
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == bookingId
                        && x.TenantId == request.TenantId
                        && x.CustomerId == customer.Id,
                    cancellationToken);

            if (!bookingValid)
            {
                return Result.Failure<ThreadSummaryResponse>(Error.NotFound("Booking"));
            }
        }

        var existing = await FindThreadAsync(
            request.TenantId,
            customer.Id,
            request.BookingId,
            cancellationToken);

        if (existing is not null)
        {
            return Result.Success(await MapThreadSummaryAsync(existing, MessageParticipantRole.Customer, cancellationToken));
        }

        var thread = new MessageThread
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            CustomerId = customer.Id,
            BookingId = request.BookingId,
            Status = ThreadStatus.Open,
            LastMessageAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.MessageThreads.Add(thread);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapThreadSummaryAsync(thread, MessageParticipantRole.Customer, cancellationToken));
    }

    public async Task<Result<MessageResponse>> SendMessageAsync(
        string auth0Sub,
        MessageParticipantRole role,
        Guid threadId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<MessageResponse>(
                role == MessageParticipantRole.Customer
                    ? ErrorCodes.CustomerAuthRequiredError()
                    : ErrorCodes.AuthRequiredError());
        }

        var body = request.Body?.Trim() ?? string.Empty;
        if (body.Length is 0 or > MaxBodyLength)
        {
            return Result.Failure<MessageResponse>(ErrorCodes.MessageBodyInvalidError());
        }

        var thread = await dbContext.MessageThreads
            .FirstOrDefaultAsync(x => x.Id == threadId, cancellationToken);

        if (thread is null)
        {
            return Result.Failure<MessageResponse>(ErrorCodes.ThreadNotFoundError());
        }

        if (thread.Status == ThreadStatus.Closed)
        {
            return Result.Failure<MessageResponse>(ErrorCodes.ThreadClosedError());
        }

        if (role == MessageParticipantRole.Business)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == thread.TenantId, cancellationToken);

            if (tenant is null)
            {
                return Result.Failure<MessageResponse>(Error.NotFound("Business"));
            }

            var entitlement = await entitlementsService.EnsureCanUseMessagingAsync(
                thread.TenantId,
                tenant.SubscriptionTier,
                cancellationToken);

            if (entitlement.IsFailure)
            {
                return Result.Failure<MessageResponse>(entitlement.Error);
            }
        }
        else
        {
            var customer = await dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Auth0Sub == auth0Sub, cancellationToken);

            if (customer is null || customer.Id != thread.CustomerId)
            {
                return Result.Failure<MessageResponse>(Error.Forbidden("You do not have access to this thread."));
            }
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            TenantId = thread.TenantId,
            SenderType = role == MessageParticipantRole.Customer
                ? MessageSenderType.Customer
                : MessageSenderType.Business,
            SenderAuth0Sub = auth0Sub,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        thread.LastMessageAt = message.CreatedAt;
        if (role == MessageParticipantRole.Customer)
        {
            thread.BusinessUnreadCount += 1;
        }
        else
        {
            thread.CustomerUnreadCount += 1;
        }

        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(MapMessage(message));
    }

    public async Task<IReadOnlyList<ThreadSummaryResponse>> ListForCustomerAsync(
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

        var threads = await dbContext.MessageThreads
            .AsNoTracking()
            .Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.LastMessageAt)
            .ToListAsync(cancellationToken);

        var results = new List<ThreadSummaryResponse>(threads.Count);
        foreach (var thread in threads)
        {
            results.Add(await MapThreadSummaryAsync(thread, MessageParticipantRole.Customer, cancellationToken));
        }

        return results;
    }

    public async Task<Result<IReadOnlyList<ThreadSummaryResponse>>> ListForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default)
    {
        var entitlement = await entitlementsService.EnsureCanUseMessagingAsync(tenantId, tier, cancellationToken);
        if (entitlement.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ThreadSummaryResponse>>(entitlement.Error);
        }

        var threads = await dbContext.MessageThreads
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.LastMessageAt)
            .ToListAsync(cancellationToken);

        var results = new List<ThreadSummaryResponse>(threads.Count);
        foreach (var thread in threads)
        {
            results.Add(await MapThreadSummaryAsync(thread, MessageParticipantRole.Business, cancellationToken));
        }

        return Result.Success<IReadOnlyList<ThreadSummaryResponse>>(results);
    }

    public async Task<Result<ThreadDetailResponse>> GetThreadAsync(
        string auth0Sub,
        MessageParticipantRole role,
        Guid threadId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var thread = await dbContext.MessageThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == threadId, cancellationToken);

        if (thread is null)
        {
            return Result.Failure<ThreadDetailResponse>(ErrorCodes.ThreadNotFoundError());
        }

        if (role == MessageParticipantRole.Business)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == thread.TenantId, cancellationToken);

            if (tenant is null)
            {
                return Result.Failure<ThreadDetailResponse>(Error.NotFound("Business"));
            }

            var entitlement = await entitlementsService.EnsureCanUseMessagingAsync(
                thread.TenantId,
                tenant.SubscriptionTier,
                cancellationToken);

            if (entitlement.IsFailure)
            {
                return Result.Failure<ThreadDetailResponse>(entitlement.Error);
            }
        }
        else
        {
            var customer = await dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Auth0Sub == auth0Sub, cancellationToken);

            if (customer is null || customer.Id != thread.CustomerId)
            {
                return Result.Failure<ThreadDetailResponse>(Error.Forbidden("You do not have access to this thread."));
            }
        }

        var cappedLimit = Math.Clamp(limit, 1, 100);
        var messages = await dbContext.Messages
            .AsNoTracking()
            .Where(x => x.ThreadId == threadId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(cappedLimit)
            .ToListAsync(cancellationToken);

        messages.Reverse();

        var customerName = await ResolveCustomerDisplayNameAsync(thread.CustomerId, cancellationToken);
        var businessName = await ResolveBusinessNameAsync(thread.TenantId, cancellationToken);

        return Result.Success(new ThreadDetailResponse(
            thread.Id,
            thread.TenantId,
            thread.BookingId,
            thread.Status.ToString().ToLowerInvariant(),
            customerName,
            businessName,
            messages.Select(MapMessage).ToList()));
    }

    public async Task<Result<UnreadCountResponse>> GetUnreadCountForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default)
    {
        var entitlement = await entitlementsService.EnsureCanUseMessagingAsync(tenantId, tier, cancellationToken);
        if (entitlement.IsFailure)
        {
            return Result.Failure<UnreadCountResponse>(entitlement.Error);
        }

        var count = await dbContext.MessageThreads
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BusinessUnreadCount > 0)
            .SumAsync(x => x.BusinessUnreadCount, cancellationToken);

        return Result.Success(new UnreadCountResponse(count));
    }

    public async Task<Result<Unit>> MarkReadAsync(
        string auth0Sub,
        MessageParticipantRole role,
        Guid threadId,
        CancellationToken cancellationToken = default)
    {
        var thread = await dbContext.MessageThreads
            .FirstOrDefaultAsync(x => x.Id == threadId, cancellationToken);

        if (thread is null)
        {
            return Result.Failure<Unit>(ErrorCodes.ThreadNotFoundError());
        }

        if (role == MessageParticipantRole.Business)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == thread.TenantId, cancellationToken);

            if (tenant is null)
            {
                return Result.Failure<Unit>(Error.NotFound("Business"));
            }

            var entitlement = await entitlementsService.EnsureCanUseMessagingAsync(
                thread.TenantId,
                tenant.SubscriptionTier,
                cancellationToken);

            if (entitlement.IsFailure)
            {
                return Result.Failure<Unit>(entitlement.Error);
            }

            thread.BusinessUnreadCount = 0;
        }
        else
        {
            var customer = await dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Auth0Sub == auth0Sub, cancellationToken);

            if (customer is null || customer.Id != thread.CustomerId)
            {
                return Result.Failure<Unit>(Error.Forbidden("You do not have access to this thread."));
            }

            thread.CustomerUnreadCount = 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }

    public async Task<Result<WhatsAppLinkResponse>> BuildBusinessWhatsAppLinkAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var location = await dbContext.BusinessLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive, cancellationToken);

        if (location is null)
        {
            return Result.Failure<WhatsAppLinkResponse>(Error.NotFound("Business"));
        }

        var profile = await dbContext.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == location.TenantId, cancellationToken);

        if (profile is null || string.IsNullOrWhiteSpace(profile.Phone))
        {
            return Result.Failure<WhatsAppLinkResponse>(Error.Validation("Business phone is not available."));
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == location.TenantId, cancellationToken);

        var businessName = tenant?.Name ?? location.Name;
        var publicUrl = $"{ResolvePublicWebBaseUrl()}/businesses/{slug}";
        var text = $"Hi {businessName}, I'd like to book on Adeni: {publicUrl}";
        var url = BuildWhatsAppUrl(profile.Phone, text);

        return Result.Success(new WhatsAppLinkResponse(url, PiiMasker.MaskPhone(profile.Phone)));
    }

    public async Task<Result<WhatsAppLinkResponse>> BuildBookingWhatsAppLinkAsync(
        string customerAuth0Sub,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<WhatsAppLinkResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var row = await (
            from booking in dbContext.Bookings.AsNoTracking()
            join customer in dbContext.Customers.AsNoTracking() on booking.CustomerId equals customer.Id
            join service in dbContext.ServiceOfferings.AsNoTracking() on booking.ServiceOfferingId equals service.Id
            join profile in dbContext.BusinessProfiles.AsNoTracking() on booking.TenantId equals profile.TenantId
            join tenant in dbContext.Tenants.AsNoTracking() on booking.TenantId equals tenant.Id
            where booking.Id == bookingId && customer.Auth0Sub == customerAuth0Sub
            select new { booking, ServiceName = service.Name, BusinessName = tenant.Name, profile.Phone })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return Result.Failure<WhatsAppLinkResponse>(Error.NotFound("Booking"));
        }

        if (string.IsNullOrWhiteSpace(row.Phone))
        {
            return Result.Failure<WhatsAppLinkResponse>(Error.Validation("Business phone is not available."));
        }

        var when = row.booking.StartAt.ToString("f");
        var text =
            $"Hi {row.BusinessName}, I have a booking on Adeni — {row.ServiceName} on {when}. Can we confirm details?";
        var url = BuildWhatsAppUrl(row.Phone, text);

        return Result.Success(new WhatsAppLinkResponse(url, PiiMasker.MaskPhone(row.Phone)));
    }

    public async Task<Result<IReadOnlyList<MessageTemplateResponse>>> GetTemplatesForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default)
    {
        var entitlement = await entitlementsService.EnsureCanUseMessagingAsync(tenantId, tier, cancellationToken);
        if (entitlement.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MessageTemplateResponse>>(entitlement.Error);
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        var profile = await dbContext.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        var location = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .FirstOrDefaultAsync(cancellationToken);

        var services = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .Take(5)
            .ToListAsync(cancellationToken);

        var businessName = tenant?.Name ?? "our business";
        var templates = new List<MessageTemplateResponse>
        {
            new(
                "hours",
                "Share hours",
                "Thanks for reaching out! Check our live availability and book online on Adeni — we confirm slots in real time."),
            new(
                "location",
                "Share location",
                location is null
                    ? $"You can find {businessName} on Adeni and book a visit."
                    : $"We're at {location.AddressLine}, {location.Area}. Book your visit on Adeni: {ResolvePublicWebBaseUrl()}/businesses/{location.Slug}"),
        };

        if (services.Count > 0)
        {
            var pricing = string.Join(
                ", ",
                services.Select(s => $"{s.Name} from {s.Currency} {s.PriceAmount:N0}"));
            templates.Add(new("pricing", "Share pricing", $"Our services: {pricing}. Book online anytime on Adeni."));
        }

        if (!string.IsNullOrWhiteSpace(profile?.Phone))
        {
            templates.Add(new(
                "contact",
                "Share contact",
                $"Call or WhatsApp us at {PiiMasker.MaskPhone(profile.Phone)} — or book directly on Adeni."));
        }

        return Result.Success<IReadOnlyList<MessageTemplateResponse>>(templates);
    }

    private async Task<MessageThread?> FindThreadAsync(
        Guid tenantId,
        Guid customerId,
        Guid? bookingId,
        CancellationToken cancellationToken)
    {
        if (bookingId is { } id)
        {
            return await dbContext.MessageThreads
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.CustomerId == customerId && x.BookingId == id,
                    cancellationToken);
        }

        return await dbContext.MessageThreads
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.CustomerId == customerId && x.BookingId == null,
                cancellationToken);
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

    private async Task<ThreadSummaryResponse> MapThreadSummaryAsync(
        MessageThread thread,
        MessageParticipantRole role,
        CancellationToken cancellationToken)
    {
        var preview = await dbContext.Messages
            .AsNoTracking()
            .Where(x => x.ThreadId == thread.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Body)
            .FirstOrDefaultAsync(cancellationToken);

        var customerName = await ResolveCustomerDisplayNameAsync(thread.CustomerId, cancellationToken);
        var businessName = await ResolveBusinessNameAsync(thread.TenantId, cancellationToken);
        var unread = role == MessageParticipantRole.Customer
            ? thread.CustomerUnreadCount
            : thread.BusinessUnreadCount;

        return new ThreadSummaryResponse(
            thread.Id,
            thread.TenantId,
            thread.BookingId,
            customerName,
            businessName,
            TruncatePreview(preview),
            thread.LastMessageAt,
            unread);
    }

    private async Task<string> ResolveCustomerDisplayNameAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return "Customer";
        }

        return string.IsNullOrWhiteSpace(customer.Name) ? "Customer" : customer.Name;
    }

    private async Task<string?> ResolveBusinessNameAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        return tenant?.Name;
    }

    private static MessageResponse MapMessage(Message message) =>
        new(
            message.Id,
            message.ThreadId,
            message.SenderType == MessageSenderType.Customer ? "customer" : "business",
            message.Body,
            message.CreatedAt);

    private static string? TruncatePreview(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return body.Length <= PreviewLength ? body : body[..PreviewLength] + "…";
    }

    private string ResolvePublicWebBaseUrl()
    {
        var configured = configuration["App:PublicWebBaseUrl"]?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? "https://adeni.io" : configured.TrimEnd('/');
    }

    private static string BuildWhatsAppUrl(string phone, string message)
    {
        var digits = WhatsAppPhoneNormalizer.Normalize(phone);
        var encoded = Uri.EscapeDataString(message);
        return $"https://wa.me/{digits}?text={encoded}";
    }
}

internal static class WhatsAppPhoneNormalizer
{
    public static string Normalize(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("234", StringComparison.Ordinal))
        {
            return digits;
        }

        if (digits.StartsWith('0') && digits.Length >= 10)
        {
            return "234" + digits[1..];
        }

        return digits;
    }
}
