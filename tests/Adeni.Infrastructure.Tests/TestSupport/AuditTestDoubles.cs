namespace Adeni.Infrastructure.Tests.TestSupport;

using Adeni.Application.Abstractions;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;

internal static class AuditTestDoubles
{
    internal static void Register(IServiceCollection services, string? userId = "test-user")
    {
        services.AddSingleton<ICorrelationContext, FixedCorrelationContext>();
        services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
        services.AddSingleton<ICurrentUser>(_ => new CurrentUser
        {
            UserId = userId,
        });
    }

    private sealed class FixedCorrelationContext : ICorrelationContext
    {
        public string CorrelationId { get; } = "test-correlation";
        public void Set(string correlationId) { }
    }
}
