namespace Adeni.Application.Messaging;

using Adeni.Application.Admin;
using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;

public sealed record CreateThreadRequest(Guid TenantId, Guid? BookingId);

public sealed record SendMessageRequest(string Body);

public sealed record ThreadSummaryResponse(
    Guid Id,
    Guid TenantId,
    Guid? BookingId,
    string CustomerDisplayName,
    string? BusinessName,
    string? Preview,
    DateTimeOffset LastMessageAt,
    int UnreadCount);

public sealed record MessageResponse(
    Guid Id,
    Guid ThreadId,
    string SenderType,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record ThreadDetailResponse(
    Guid Id,
    Guid TenantId,
    Guid? BookingId,
    string Status,
    string CustomerDisplayName,
    string? BusinessName,
    IReadOnlyList<MessageResponse> Messages);

public sealed record UnreadCountResponse(int Count);

public sealed record WhatsAppLinkResponse(string Url, string? PhoneMasked);

public sealed record MessageTemplateResponse(string Key, string Label, string Body);

public enum MessageParticipantRole
{
    Customer,
    Business,
}

public interface IMessageThreadService
{
    Task<Result<ThreadSummaryResponse>> CreateOrGetThreadAsync(
        string customerAuth0Sub,
        CreateThreadRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<MessageResponse>> SendMessageAsync(
        string auth0Sub,
        MessageParticipantRole role,
        Guid threadId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ThreadSummaryResponse>> ListForCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ThreadSummaryResponse>>> ListForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);

    Task<Result<ThreadDetailResponse>> GetThreadAsync(
        string auth0Sub,
        MessageParticipantRole role,
        Guid threadId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<Result<UnreadCountResponse>> GetUnreadCountForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> MarkReadAsync(
        string auth0Sub,
        MessageParticipantRole role,
        Guid threadId,
        CancellationToken cancellationToken = default);

    Task<Result<WhatsAppLinkResponse>> BuildBusinessWhatsAppLinkAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task<Result<WhatsAppLinkResponse>> BuildBookingWhatsAppLinkAsync(
        string customerAuth0Sub,
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<MessageTemplateResponse>>> GetTemplatesForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);
}
