namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class VerificationLifecycleNotifier(
    INotificationDispatcher notificationDispatcher,
    INotificationRecipientResolver recipientResolver,
    AdeniDbContext dbContext,
    ILogger<VerificationLifecycleNotifier> logger) : IVerificationLifecycleNotifier
{
    public Task NotifySubmittedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        SendAsync(tenantId, NotificationTemplateIds.VerificationSubmitted, "verification-submitted", null, cancellationToken);

    public Task NotifyApprovedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        SendAsync(tenantId, NotificationTemplateIds.VerificationApproved, "verification-approved", null, cancellationToken);

    public Task NotifyRejectedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        SendAsync(
            tenantId,
            NotificationTemplateIds.VerificationRejected,
            "verification-rejected",
            tenantId,
            cancellationToken);

    private async Task SendAsync(
        Guid tenantId,
        string templateId,
        string dedupePrefix,
        Guid? loadReasonTenantId,
        CancellationToken cancellationToken)
    {
        var contact = await recipientResolver.ResolveTenantBusinessOwnerAsync(tenantId, cancellationToken);
        if (contact.IsFailure || string.IsNullOrWhiteSpace(contact.Value!.Email))
        {
            logger.LogDebug(
                "Skipping verification lifecycle email for tenant {TenantId} — owner email not resolved.",
                tenantId);
            return;
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return;
        }

        var data = new Dictionary<string, string>
        {
            ["businessName"] = tenant.Name,
        };

        if (loadReasonTenantId is not null)
        {
            var reasonRow = await dbContext.Tenants
                .AsNoTracking()
                .Where(x => x.Id == tenantId)
                .Select(x => x.VerificationRejectionReason)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(reasonRow))
            {
                data["reason"] = reasonRow.Trim();
            }
        }

        await notificationDispatcher.SendAsync(
            NotificationMessage.FromTemplate(
                "email",
                contact.Value.Email.Trim(),
                templateId,
                data,
                tenantId,
                $"{dedupePrefix}:{tenantId:D}"),
            cancellationToken);
    }
}
