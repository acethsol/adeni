namespace Adeni.Application.Notifications;

public interface IVerificationLifecycleNotifier
{
    Task NotifySubmittedAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task NotifyApprovedAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task NotifyRejectedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
