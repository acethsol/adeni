namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Notifications;

internal sealed class NoopVerificationLifecycleNotifier : IVerificationLifecycleNotifier
{
    public Task NotifySubmittedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyApprovedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyRejectedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
