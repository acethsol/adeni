namespace Adeni.Application.Notifications;

using Adeni.Domain.Common;

public sealed record RenderedNotificationTemplate(string Subject, string Body);

public interface INotificationTemplateRenderer
{
    Task<Result<RenderedNotificationTemplate>> RenderAsync(
        string templateId,
        Guid? tenantId,
        string locale,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);
}
