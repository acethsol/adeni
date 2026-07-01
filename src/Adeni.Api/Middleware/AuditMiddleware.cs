namespace Adeni.Api.Middleware;

using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;

public sealed class AuditMiddleware(
    RequestDelegate next,
    ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IAuditLogWriter auditLogWriter,
        ICurrentUser currentUser)
    {
        await next(context);

        if (!AdminAuditRules.IsAuditableAdminMutation(context))
        {
            return;
        }

        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.TraceIdentifier;

        var actorId = currentUser.UserId ?? "anonymous";
        var action = AdminAuditRules.MapAction(context);
        var entityId = AdminAuditRules.ExtractEntityId(context) ?? "unknown";

        var entry = new AuditEntry(
            Guid.NewGuid(),
            actorId,
            action,
            "admin",
            entityId,
            correlationId,
            DateTimeOffset.UtcNow);

        await auditLogWriter.WriteAsync(entry);

        logger.LogInformation(
            "Admin audit {Action} on {EntityId} by {ActorId} correlation {CorrelationId}",
            action,
            entityId,
            actorId,
            correlationId);
    }
}
