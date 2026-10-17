namespace Adeni.Infrastructure.Messaging;

using Adeni.Application.Admin;
using Adeni.Application.Common;
using Adeni.Application.Messaging;
using Adeni.Application.Security;
using Adeni.Application.Storage;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Messaging;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public sealed class ConversationService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService,
    IFaqAutoResponder faqAutoResponder,
    IMessagingRealtimePublisher realtime,
    IFileStorage fileStorage,
    IConfiguration configuration) : IConversationService
{
    private const int MaxBodyLength = 4000;
    private const int PreviewLength = 120;
    private const int MaxAttachmentsPerMessage = 5;
    private const long MaxAttachmentBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedAttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    public async Task<Result<ConversationSummaryResponse>> CreateOrGetAsync(
        string customerAuth0Sub,
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<ConversationSummaryResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.TenantId && x.Status == TenantStatus.Verified,
                cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<ConversationSummaryResponse>(Error.NotFound("Business"));
        }

        var customer = await GetOrCreateCustomerAsync(customerAuth0Sub, cancellationToken);
        var contextType = NormalizeContext(request.ContextType, request.ContextId);

        if (contextType == ConversationContextType.Booking && request.ContextId is { } bookingId)
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
                return Result.Failure<ConversationSummaryResponse>(Error.NotFound("Booking"));
            }
        }

        var existing = await FindOpenConversationAsync(
            request.TenantId,
            customer.Id,
            contextType,
            request.ContextId,
            cancellationToken);

        if (existing is not null)
        {
            return Result.Success(await MapSummaryAsync(existing, forCustomer: true, cancellationToken));
        }

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            CustomerId = customer.Id,
            ContextType = contextType,
            ContextId = request.ContextId,
            Status = ConversationStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            LastMessageAt = DateTimeOffset.UtcNow,
        };

        var customerParticipant = new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TenantId = conversation.TenantId,
            ParticipantType = ConversationParticipantType.Customer,
            CustomerId = customer.Id,
            Auth0Sub = customer.Auth0Sub,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        conversation.CreatedByParticipantId = customerParticipant.Id;
        dbContext.Conversations.Add(conversation);
        dbContext.ConversationParticipants.Add(customerParticipant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapSummaryAsync(conversation, forCustomer: true, cancellationToken));
    }

    public async Task<Result<ConversationMessageResponse>> SendAsync(
        string auth0Sub,
        MessagingActorRole role,
        Guid conversationId,
        SendConversationMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<ConversationMessageResponse>(
                role == MessagingActorRole.Customer
                    ? ErrorCodes.CustomerAuthRequiredError()
                    : ErrorCodes.AuthRequiredError());
        }

        var body = request.Body?.Trim() ?? string.Empty;
        var attachmentRequests = request.Attachments ?? [];
        if (body.Length > MaxBodyLength
            || (body.Length == 0 && attachmentRequests.Count == 0))
        {
            return Result.Failure<ConversationMessageResponse>(ErrorCodes.MessageBodyInvalidError());
        }

        if (attachmentRequests.Count > MaxAttachmentsPerMessage)
        {
            return Result.Failure<ConversationMessageResponse>(
                ErrorCodes.MessageAttachmentInvalidError($"At most {MaxAttachmentsPerMessage} attachments are allowed."));
        }

        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            return Result.Failure<ConversationMessageResponse>(ErrorCodes.ConversationNotFoundError());
        }

        if (conversation.Status == ConversationStatus.Closed)
        {
            return Result.Failure<ConversationMessageResponse>(ErrorCodes.ConversationClosedError());
        }

        ConversationParticipant sender;
        if (role == MessagingActorRole.Business)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == conversation.TenantId, cancellationToken);
            if (tenant is null)
            {
                return Result.Failure<ConversationMessageResponse>(Error.NotFound("Business"));
            }

            var entitlement = await entitlementsService.EnsureCanUseMessagingForTenantAsync(
                tenant,
                cancellationToken);
            if (entitlement.IsFailure)
            {
                return Result.Failure<ConversationMessageResponse>(entitlement.Error);
            }

            var businessUser = await dbContext.BusinessUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TenantId == conversation.TenantId && x.Auth0Sub == auth0Sub,
                    cancellationToken);
            if (businessUser is null)
            {
                return Result.Failure<ConversationMessageResponse>(
                    Error.Forbidden("You do not have access to this conversation."));
            }

            sender = await UpsertBusinessParticipantAsync(conversation, businessUser, cancellationToken);
        }
        else
        {
            var customer = await dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Auth0Sub == auth0Sub, cancellationToken);
            if (customer is null || customer.Id != conversation.CustomerId)
            {
                return Result.Failure<ConversationMessageResponse>(
                    Error.Forbidden("You do not have access to this conversation."));
            }

            sender = await UpsertCustomerParticipantAsync(conversation, customer, cancellationToken);
        }

        var clientMessageId = string.IsNullOrWhiteSpace(request.ClientMessageId)
            ? null
            : request.ClientMessageId.Trim();
        if (clientMessageId is { Length: > 64 })
        {
            clientMessageId = clientMessageId[..64];
        }

        if (clientMessageId is not null)
        {
            var existing = await dbContext.ConversationMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.ConversationId == conversation.Id && x.ClientMessageId == clientMessageId,
                    cancellationToken);
            if (existing is not null)
            {
                return Result.Success(await MapMessageAsync(existing, cancellationToken));
            }
        }

        List<MessageAttachment>? attachments = null;
        if (attachmentRequests.Count > 0)
        {
            var prepared = await PrepareAttachmentsAsync(
                conversation.TenantId,
                attachmentRequests,
                cancellationToken);
            if (prepared.IsFailure)
            {
                return Result.Failure<ConversationMessageResponse>(prepared.Error);
            }

            attachments = prepared.Value;
        }

        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TenantId = conversation.TenantId,
            SenderParticipantId = sender.Id,
            Type = body.Length == 0
                ? ConversationMessageType.AttachmentOnly
                : ConversationMessageType.Text,
            Body = body.Length == 0 ? null : body,
            ClientMessageId = clientMessageId,
            CreatedAt = DateTimeOffset.UtcNow,
            ModerationStatus = MessageModerationStatus.None,
        };

        conversation.LastMessageAt = message.CreatedAt;
        sender.LastReadMessageId = message.Id;
        sender.LastReadAt = message.CreatedAt;

        dbContext.ConversationMessages.Add(message);
        if (attachments is { Count: > 0 })
        {
            foreach (var attachment in attachments)
            {
                attachment.MessageId = message.Id;
                dbContext.MessageAttachments.Add(attachment);
            }

            message.Attachments = attachments;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (clientMessageId is not null)
        {
            var raced = await dbContext.ConversationMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.ConversationId == conversation.Id && x.ClientMessageId == clientMessageId,
                    cancellationToken);
            if (raced is not null)
            {
                return Result.Success(await MapMessageAsync(raced, cancellationToken));
            }

            throw;
        }

        var mapped = await MapMessageAsync(message, cancellationToken);
        await realtime.PublishMessageCreatedAsync(
            conversation.Id,
            conversation.TenantId,
            mapped,
            cancellationToken);

        var summary = await MapSummaryAsync(conversation, forCustomer: false, cancellationToken);
        await realtime.PublishConversationUpdatedAsync(conversation.TenantId, summary, cancellationToken);

        if (role == MessagingActorRole.Customer && body.Length > 0)
        {
            await TrySendFaqAutoReplyAsync(conversation, body, cancellationToken);
        }

        return Result.Success(mapped);
    }

    public async Task<IReadOnlyList<ConversationSummaryResponse>> ListForCustomerAsync(
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

        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.LastMessageAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var results = new List<ConversationSummaryResponse>(conversations.Count);
        foreach (var conversation in conversations)
        {
            results.Add(await MapSummaryAsync(conversation, forCustomer: true, cancellationToken));
        }

        return results;
    }

    public async Task<Result<PagedResult<ConversationSummaryResponse>>> ListForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        int page = 1,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<PagedResult<ConversationSummaryResponse>>(Error.NotFound("Business"));
        }

        var entitlement = await entitlementsService.EnsureCanUseMessagingForTenantAsync(tenant, cancellationToken);
        if (entitlement.IsFailure)
        {
            return Result.Failure<PagedResult<ConversationSummaryResponse>>(entitlement.Error);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, Paging.MaxPageSize);

        var query = dbContext.Conversations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.LastMessageAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var mapped = new List<ConversationSummaryResponse>(items.Count);
        foreach (var conversation in items)
        {
            mapped.Add(await MapSummaryAsync(conversation, forCustomer: false, cancellationToken));
        }

        return Result.Success(new PagedResult<ConversationSummaryResponse>(mapped, page, pageSize, total));
    }

    public async Task<Result<ConversationDetailResponse>> GetAsync(
        string auth0Sub,
        MessagingActorRole role,
        Guid conversationId,
        Guid? beforeId = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return Result.Failure<ConversationDetailResponse>(ErrorCodes.ConversationNotFoundError());
        }

        if (role == MessagingActorRole.Business)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == conversation.TenantId, cancellationToken);
            if (tenant is null)
            {
                return Result.Failure<ConversationDetailResponse>(Error.NotFound("Business"));
            }

            var entitlement = await entitlementsService.EnsureCanUseMessagingForTenantAsync(
                tenant,
                cancellationToken);
            if (entitlement.IsFailure)
            {
                return Result.Failure<ConversationDetailResponse>(entitlement.Error);
            }

            var businessUser = await dbContext.BusinessUsers
                .FirstOrDefaultAsync(
                    x => x.TenantId == conversation.TenantId && x.Auth0Sub == auth0Sub,
                    cancellationToken);
            if (businessUser is null)
            {
                return Result.Failure<ConversationDetailResponse>(
                    Error.Forbidden("You do not have access to this conversation."));
            }

            // Lazy upsert on open
            await UpsertBusinessParticipantAsync(conversation, businessUser, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var customer = await dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Auth0Sub == auth0Sub, cancellationToken);
            if (customer is null || customer.Id != conversation.CustomerId)
            {
                return Result.Failure<ConversationDetailResponse>(
                    Error.Forbidden("You do not have access to this conversation."));
            }
        }

        DateTimeOffset? beforeCreatedAt = null;
        if (beforeId is { } bid)
        {
            beforeCreatedAt = await dbContext.ConversationMessages
                .AsNoTracking()
                .Where(x => x.Id == bid && x.ConversationId == conversationId)
                .Select(x => (DateTimeOffset?)x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var messagesQuery = dbContext.ConversationMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.DeletedAt == null);

        if (beforeCreatedAt is { } beforeAt)
        {
            messagesQuery = messagesQuery.Where(x => x.CreatedAt < beforeAt);
        }

        var page = await messagesQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take + 1)
            .ToListAsync(cancellationToken);

        Guid? nextBeforeId = null;
        if (page.Count > take)
        {
            nextBeforeId = page[take - 1].Id;
            page = page.Take(take).ToList();
        }

        page.Reverse();
        var mappedMessages = new List<ConversationMessageResponse>(page.Count);
        foreach (var message in page)
        {
            mappedMessages.Add(await MapMessageAsync(message, cancellationToken));
        }

        var customerName = await ResolveCustomerNameAsync(conversation.CustomerId, cancellationToken);
        var businessName = await ResolveBusinessNameAsync(conversation.TenantId, cancellationToken);

        return Result.Success(new ConversationDetailResponse(
            conversation.Id,
            conversation.TenantId,
            conversation.ContextType,
            conversation.ContextId,
            conversation.Status.ToString().ToLowerInvariant(),
            customerName,
            businessName,
            mappedMessages,
            nextBeforeId));
    }

    public async Task<Result<UnreadCountResponse>> GetTeamUnreadCountForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<UnreadCountResponse>(Error.NotFound("Business"));
        }

        var entitlement = await entitlementsService.EnsureCanUseMessagingForTenantAsync(tenant, cancellationToken);
        if (entitlement.IsFailure)
        {
            return Result.Failure<UnreadCountResponse>(entitlement.Error);
        }

        var conversationIds = await dbContext.Conversations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Status == ConversationStatus.Open)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var id in conversationIds)
        {
            if (await ConversationNeedsAttentionAsync(id, cancellationToken))
            {
                count++;
            }
        }

        return Result.Success(new UnreadCountResponse(count));
    }

    public async Task<Result> MarkReadAsync(
        string auth0Sub,
        MessagingActorRole role,
        Guid conversationId,
        Guid? messageId = null,
        CancellationToken cancellationToken = default)
    {
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return Result.Failure(ErrorCodes.ConversationNotFoundError());
        }

        ConversationParticipant participant;
        if (role == MessagingActorRole.Business)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == conversation.TenantId, cancellationToken);
            if (tenant is null)
            {
                return Result.Failure(Error.NotFound("Business"));
            }

            var entitlement = await entitlementsService.EnsureCanUseMessagingForTenantAsync(
                tenant,
                cancellationToken);
            if (entitlement.IsFailure)
            {
                return Result.Failure(entitlement.Error);
            }

            var businessUser = await dbContext.BusinessUsers
                .FirstOrDefaultAsync(
                    x => x.TenantId == conversation.TenantId && x.Auth0Sub == auth0Sub,
                    cancellationToken);
            if (businessUser is null)
            {
                return Result.Failure(Error.Forbidden("You do not have access to this conversation."));
            }

            participant = await UpsertBusinessParticipantAsync(conversation, businessUser, cancellationToken);
        }
        else
        {
            var customer = await dbContext.Customers
                .FirstOrDefaultAsync(x => x.Auth0Sub == auth0Sub, cancellationToken);
            if (customer is null || customer.Id != conversation.CustomerId)
            {
                return Result.Failure(Error.Forbidden("You do not have access to this conversation."));
            }

            participant = await UpsertCustomerParticipantAsync(conversation, customer, cancellationToken);
        }

        var targetMessageId = messageId;
        if (targetMessageId is null)
        {
            targetMessageId = await dbContext.ConversationMessages
                .AsNoTracking()
                .Where(x => x.ConversationId == conversationId && x.DeletedAt == null)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (targetMessageId is { } mid)
        {
            participant.LastReadMessageId = mid;
            participant.LastReadAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await realtime.PublishReadUpdatedAsync(
                conversationId,
                conversation.TenantId,
                participant.Id,
                mid,
                cancellationToken);
        }

        return Result.Success();
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
        return Result.Success(new WhatsAppLinkResponse(BuildWhatsAppUrl(profile.Phone, text), PiiMasker.MaskPhone(profile.Phone)));
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
        return Result.Success(new WhatsAppLinkResponse(BuildWhatsAppUrl(row.Phone, text), PiiMasker.MaskPhone(row.Phone)));
    }

    public async Task<Result<IReadOnlyList<MessageTemplateResponse>>> GetTemplatesForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<IReadOnlyList<MessageTemplateResponse>>(Error.NotFound("Business"));
        }

        var entitlement = await entitlementsService.EnsureCanUseMessagingForTenantAsync(tenant, cancellationToken);
        if (entitlement.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MessageTemplateResponse>>(entitlement.Error);
        }

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

        var businessName = tenant.Name;
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

    public async Task<Result<MessagingSettingsResponse>> GetMessagingSettingsAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveBusinessAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<MessagingSettingsResponse>(access.Error);
        }

        return Result.Success(new MessagingSettingsResponse(access.Value!.FaqAutoResponderEnabled));
    }

    public async Task<Result<MessagingSettingsResponse>> UpdateMessagingSettingsAsync(
        Guid tenantId,
        UpdateMessagingSettingsRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveBusinessAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<MessagingSettingsResponse>(access.Error);
        }

        var profile = access.Value!;
        profile.FaqAutoResponderEnabled = request.FaqAutoResponderEnabled;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new MessagingSettingsResponse(profile.FaqAutoResponderEnabled));
    }

    private async Task TrySendFaqAutoReplyAsync(
        Conversation conversation,
        string customerBody,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == conversation.TenantId, cancellationToken);
        if (profile is null || !profile.FaqAutoResponderEnabled)
        {
            return;
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversation.TenantId, cancellationToken);
        if (tenant is null)
        {
            return;
        }

        var entitlement = await entitlementsService.EnsureCanUseMessagingForTenantAsync(tenant, cancellationToken);
        if (entitlement.IsFailure)
        {
            return;
        }

        var reply = await faqAutoResponder.TryBuildReplyAsync(
            conversation.TenantId,
            customerBody,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(reply))
        {
            return;
        }

        var bot = await UpsertBotParticipantAsync(conversation, MessagingBotKeys.Faq, cancellationToken);
        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TenantId = conversation.TenantId,
            SenderParticipantId = bot.Id,
            Type = ConversationMessageType.Text,
            Body = reply,
            CreatedAt = DateTimeOffset.UtcNow,
            ModerationStatus = MessageModerationStatus.None,
        };
        conversation.LastMessageAt = message.CreatedAt;
        dbContext.ConversationMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        var mapped = await MapMessageAsync(message, cancellationToken);
        await realtime.PublishMessageCreatedAsync(
            conversation.Id,
            conversation.TenantId,
            mapped,
            cancellationToken);
    }

    private async Task<bool> ConversationNeedsAttentionAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var lastCustomerMessage = await (
            from m in dbContext.ConversationMessages.AsNoTracking()
            join p in dbContext.ConversationParticipants.AsNoTracking() on m.SenderParticipantId equals p.Id
            where m.ConversationId == conversationId
                && m.DeletedAt == null
                && p.ParticipantType == ConversationParticipantType.Customer
            orderby m.CreatedAt descending, m.Id descending
            select m)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastCustomerMessage is null)
        {
            return false;
        }

        var businessParticipants = await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(x =>
                x.ConversationId == conversationId
                && x.ParticipantType == ConversationParticipantType.BusinessUser)
            .ToListAsync(cancellationToken);

        if (businessParticipants.Count == 0
            || businessParticipants.All(x => x.LastReadMessageId is null))
        {
            return true;
        }

        var readIds = businessParticipants
            .Where(x => x.LastReadMessageId is not null)
            .Select(x => x.LastReadMessageId!.Value)
            .ToArray();

        var maxReadAt = await dbContext.ConversationMessages
            .AsNoTracking()
            .Where(x => readIds.Contains(x.Id))
            .Select(x => (DateTimeOffset?)x.CreatedAt)
            .MaxAsync(cancellationToken);

        return maxReadAt is null || lastCustomerMessage.CreatedAt > maxReadAt;
    }

    private async Task<ConversationSummaryResponse> MapSummaryAsync(
        Conversation conversation,
        bool forCustomer,
        CancellationToken cancellationToken)
    {
        var lastMessage = await dbContext.ConversationMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversation.Id && x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        string? preview = null;
        string? lastBusinessReplier = null;
        if (lastMessage is not null)
        {
            preview = Truncate(lastMessage.Body);
            if (string.IsNullOrWhiteSpace(preview))
            {
                var hasAttachment = await dbContext.MessageAttachments
                    .AsNoTracking()
                    .AnyAsync(x => x.MessageId == lastMessage.Id, cancellationToken);
                if (hasAttachment)
                {
                    preview = "Attachment";
                }
            }

            var sender = await dbContext.ConversationParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == lastMessage.SenderParticipantId, cancellationToken);
            if (sender?.ParticipantType == ConversationParticipantType.BusinessUser
                && sender.BusinessUserId is { } businessUserId)
            {
                lastBusinessReplier = await ResolveBusinessUserDisplayNameAsync(
                    businessUserId,
                    cancellationToken);
            }
        }

        var needsAttention = forCustomer
            ? false
            : await ConversationNeedsAttentionAsync(conversation.Id, cancellationToken);

        return new ConversationSummaryResponse(
            conversation.Id,
            conversation.TenantId,
            conversation.ContextType,
            conversation.ContextId,
            await ResolveCustomerNameAsync(conversation.CustomerId, cancellationToken),
            await ResolveBusinessNameAsync(conversation.TenantId, cancellationToken),
            preview,
            lastBusinessReplier,
            conversation.LastMessageAt,
            needsAttention);
    }

    private async Task<Result<List<MessageAttachment>>> PrepareAttachmentsAsync(
        Guid tenantId,
        IReadOnlyList<SendMessageAttachmentRequest> requests,
        CancellationToken cancellationToken)
    {
        var expectedPrefix = $"tenants/{tenantId:N}/messages/";
        var attachments = new List<MessageAttachment>(requests.Count);
        foreach (var request in requests)
        {
            var storageKey = request.StorageKey?.Trim() ?? string.Empty;
            var contentType = request.ContentType?.Trim() ?? string.Empty;
            var fileName = request.FileName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(storageKey)
                || !storageKey.StartsWith(expectedPrefix, StringComparison.Ordinal)
                || storageKey.Length > 512)
            {
                return Result.Failure<List<MessageAttachment>>(
                    ErrorCodes.MessageAttachmentInvalidError("Attachment key is not valid for this business."));
            }

            if (!AllowedAttachmentContentTypes.Contains(contentType))
            {
                return Result.Failure<List<MessageAttachment>>(
                    ErrorCodes.MessageAttachmentInvalidError("Attachment type is not supported."));
            }

            if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 256)
            {
                return Result.Failure<List<MessageAttachment>>(
                    ErrorCodes.MessageAttachmentInvalidError("Attachment file name is invalid."));
            }

            if (request.FileSize <= 0 || request.FileSize > MaxAttachmentBytes)
            {
                return Result.Failure<List<MessageAttachment>>(
                    ErrorCodes.MessageAttachmentInvalidError("Attachment must be between 1 byte and 5 MB."));
            }

            if (!await fileStorage.ExistsAsync(storageKey, cancellationToken))
            {
                return Result.Failure<List<MessageAttachment>>(
                    ErrorCodes.MessageAttachmentInvalidError(
                        "Attachment upload was not found. Upload the file before sending."));
            }

            attachments.Add(new MessageAttachment
            {
                Id = Guid.NewGuid(),
                BlobKey = storageKey,
                ContentType = contentType,
                FileName = fileName,
                FileSize = request.FileSize,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        return Result.Success(attachments);
    }

    private async Task<ConversationMessageResponse> MapMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken)
    {
        var sender = await dbContext.ConversationParticipants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == message.SenderParticipantId, cancellationToken);

        var senderType = sender?.ParticipantType switch
        {
            ConversationParticipantType.Customer => "customer",
            ConversationParticipantType.BusinessUser => "business",
            ConversationParticipantType.Bot => "bot",
            _ => "unknown",
        };

        string? displayName = sender?.ParticipantType switch
        {
            ConversationParticipantType.Customer when sender.CustomerId is { } cid =>
                await ResolveCustomerNameAsync(cid, cancellationToken),
            ConversationParticipantType.BusinessUser when sender.BusinessUserId is { } bid =>
                await ResolveBusinessUserDisplayNameAsync(bid, cancellationToken),
            ConversationParticipantType.Bot => sender.BotKey == MessagingBotKeys.Faq
                ? "FAQ Assistant"
                : "Adeni Assistant",
            _ => null,
        };

        var attachmentEntities = message.Attachments.Count > 0
            ? message.Attachments.ToList()
            : await dbContext.MessageAttachments
                .AsNoTracking()
                .Where(x => x.MessageId == message.Id)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        var attachments = new List<MessageAttachmentResponse>(attachmentEntities.Count);
        foreach (var attachment in attachmentEntities)
        {
            string? url = null;
            try
            {
                if (await fileStorage.ExistsAsync(attachment.BlobKey, cancellationToken))
                {
                    url = await fileStorage.GetDownloadUrlAsync(attachment.BlobKey, cancellationToken);
                }
            }
            catch
            {
                url = null;
            }

            attachments.Add(new MessageAttachmentResponse(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.FileSize,
                url));
        }

        return new ConversationMessageResponse(
            message.Id,
            message.ConversationId,
            message.SenderParticipantId,
            senderType,
            displayName,
            message.Body,
            message.ClientMessageId,
            message.CreatedAt,
            attachments);
    }

    private async Task<ConversationParticipant> UpsertCustomerParticipantAsync(
        Conversation conversation,
        Customer customer,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ConversationParticipants
            .FirstOrDefaultAsync(
                x => x.ConversationId == conversation.Id
                    && x.ParticipantType == ConversationParticipantType.Customer
                    && x.CustomerId == customer.Id,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var participant = new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TenantId = conversation.TenantId,
            ParticipantType = ConversationParticipantType.Customer,
            CustomerId = customer.Id,
            Auth0Sub = customer.Auth0Sub,
            JoinedAt = DateTimeOffset.UtcNow,
        };
        dbContext.ConversationParticipants.Add(participant);
        return participant;
    }

    private async Task<ConversationParticipant> UpsertBusinessParticipantAsync(
        Conversation conversation,
        BusinessUser businessUser,
        CancellationToken cancellationToken)
    {
        var trackedConversation = await dbContext.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversation.Id, cancellationToken)
            ?? conversation;

        var existing = await dbContext.ConversationParticipants
            .FirstOrDefaultAsync(
                x => x.ConversationId == trackedConversation.Id
                    && x.ParticipantType == ConversationParticipantType.BusinessUser
                    && x.BusinessUserId == businessUser.Id,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var participant = new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = trackedConversation.Id,
            TenantId = trackedConversation.TenantId,
            ParticipantType = ConversationParticipantType.BusinessUser,
            BusinessUserId = businessUser.Id,
            Auth0Sub = businessUser.Auth0Sub,
            JoinedAt = DateTimeOffset.UtcNow,
        };
        dbContext.ConversationParticipants.Add(participant);
        return participant;
    }

    private async Task<ConversationParticipant> UpsertBotParticipantAsync(
        Conversation conversation,
        string botKey,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ConversationParticipants
            .FirstOrDefaultAsync(
                x => x.ConversationId == conversation.Id
                    && x.ParticipantType == ConversationParticipantType.Bot
                    && x.BotKey == botKey,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var participant = new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TenantId = conversation.TenantId,
            ParticipantType = ConversationParticipantType.Bot,
            BotKey = botKey,
            JoinedAt = DateTimeOffset.UtcNow,
        };
        dbContext.ConversationParticipants.Add(participant);
        return participant;
    }

    private async Task<Conversation?> FindOpenConversationAsync(
        Guid tenantId,
        Guid customerId,
        ConversationContextType contextType,
        Guid? contextId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Conversations
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.CustomerId == customerId
                    && x.ContextType == contextType
                    && x.ContextId == contextId
                    && x.Status == ConversationStatus.Open,
                cancellationToken);
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        string auth0Sub,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(x => x.Auth0Sub == auth0Sub, cancellationToken);
        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer
        {
            Id = Guid.NewGuid(),
            Auth0Sub = auth0Sub,
            Name = "Customer",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private async Task<Result<BusinessProfile>> ResolveBusinessAccessAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken)
    {
        var member = await dbContext.BusinessUsers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, cancellationToken);
        if (!member)
        {
            return Result.Failure<BusinessProfile>(ErrorCodes.BusinessAccessDeniedError());
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<BusinessProfile>(Error.NotFound("Business"));
        }

        var entitlement = await entitlementsService.EnsureCanUseMessagingForTenantAsync(
            tenant,
            cancellationToken);
        if (entitlement.IsFailure)
        {
            return Result.Failure<BusinessProfile>(entitlement.Error);
        }

        var profile = await dbContext.BusinessProfiles
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<BusinessProfile>(Error.NotFound("Business"));
        }

        return Result.Success(profile);
    }

    private async Task<string> ResolveCustomerNameAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var name = await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(name) ? "Customer" : name;
    }

    private async Task<string?> ResolveBusinessNameAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> ResolveBusinessUserDisplayNameAsync(
        Guid businessUserId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from user in dbContext.BusinessUsers.AsNoTracking()
            where user.Id == businessUserId
            join staff in dbContext.StaffMembers.AsNoTracking()
                on user.StaffMemberId equals staff.Id into staffJoin
            from staff in staffJoin.DefaultIfEmpty()
            select new { user.Role, StaffName = staff != null ? staff.DisplayName : null })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return "Team";
        }

        if (!string.IsNullOrWhiteSpace(row.StaffName))
        {
            return row.StaffName;
        }

        return string.IsNullOrWhiteSpace(row.Role) ? "Team" : row.Role;
    }

    private static ConversationContextType NormalizeContext(
        ConversationContextType contextType,
        Guid? contextId)
    {
        if (contextType == ConversationContextType.None && contextId is null)
        {
            return ConversationContextType.Profile;
        }

        if (contextType == ConversationContextType.Booking && contextId is null)
        {
            return ConversationContextType.Profile;
        }

        return contextType == ConversationContextType.None
            ? ConversationContextType.Profile
            : contextType;
    }

    private static string? Truncate(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        var trimmed = body.Trim();
        return trimmed.Length <= PreviewLength ? trimmed : trimmed[..PreviewLength] + "…";
    }

    private string ResolvePublicWebBaseUrl()
    {
        var configured = configuration["Portal:PublicBaseUrl"]
            ?? configuration["Discover:PublicBaseUrl"]
            ?? "http://localhost:5190";
        return configured.TrimEnd('/');
    }

    private static string BuildWhatsAppUrl(string phone, string text)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return $"https://wa.me/{digits}?text={Uri.EscapeDataString(text)}";
    }
}
