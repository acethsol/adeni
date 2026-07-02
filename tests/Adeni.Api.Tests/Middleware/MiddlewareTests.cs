namespace Adeni.Api.Tests.Middleware;

using Adeni.Api.Middleware;
using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;
using Adeni.Infrastructure.Auditing;
using Microsoft.AspNetCore.Http;
using Adeni.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Context;
using Serilog.Events;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Uses_incoming_correlation_header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "incoming-id";
        var invoked = false;

        var middleware = new CorrelationIdMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(invoked);
        Assert.Equal("incoming-id", context.Items[CorrelationIdMiddleware.ItemKey]);
        Assert.Equal("incoming-id", context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task Generates_correlation_id_when_missing()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrWhiteSpace(context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()));
    }

    [Fact]
    public async Task Enriches_serilog_log_context_with_correlation_id()
    {
        LogEvent? captured = null;
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "ctx-id";

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new CaptureSink(e => captured = e))
            .CreateLogger();

        try
        {
            var middleware = new CorrelationIdMiddleware(_ =>
            {
                Log.Information("correlation probe");
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context);

            Assert.NotNull(captured);
            Assert.True(captured!.Properties.ContainsKey(CorrelationIdMiddleware.LogContextPropertyName));
            Assert.Equal("\"ctx-id\"", captured.Properties[CorrelationIdMiddleware.LogContextPropertyName].ToString());
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

internal sealed class CaptureSink(Action<LogEvent> capture) : Serilog.Core.ILogEventSink
{
    public void Emit(LogEvent logEvent) => capture(logEvent);
}

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Adds_security_headers()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Contains("max-age", context.Response.Headers["Strict-Transport-Security"].ToString());
    }

    [Fact]
    public async Task Omits_hsts_on_http()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Uses_strict_csp_for_api_routes()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/categories";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'",
            context.Response.Headers["Content-Security-Policy"].ToString());
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger")]
    public async Task Uses_documentation_csp_for_openapi_ui_routes(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Contains("script-src 'self' 'unsafe-inline'", context.Response.Headers["Content-Security-Policy"].ToString());
    }
}

public sealed class AuditMiddlewareTests
{
    [Theory]
    [InlineData("/api/v1/admin/businesses/123/approve", "POST", true)]
    [InlineData("/api/v1/admin/businesses/123/approve", "GET", false)]
    [InlineData("/api/v1/customers/me", "POST", false)]
    public void IsAuditableAdminMutation_detects_mutations(string path, string method, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;

        Assert.Equal(expected, AdminAuditRules.IsAuditableAdminMutation(context));
    }

    [Theory]
    [InlineData("/api/v1/admin/businesses/123/approve", AuditActions.BusinessApproved)]
    [InlineData("/api/v1/admin/businesses/123/reject", AuditActions.BusinessRejected)]
    [InlineData("/api/v1/admin/businesses/123/suspend", AuditActions.BusinessSuspended)]
    [InlineData("/api/v1/admin/reviews/123", AuditActions.ReviewHidden)]
    [InlineData("/api/v1/admin/customers/123/export", AuditActions.CustomerExported)]
    [InlineData("/api/v1/admin/customers/123/delete", AuditActions.CustomerDeleted)]
    public void MapAction_maps_known_routes(string path, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = path.Contains("/reviews/", StringComparison.Ordinal) ? "DELETE" : "POST";

        Assert.Equal(expected, AdminAuditRules.MapAction(context));
    }

    [Fact]
    public void MapAction_falls_back_to_method_name()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/admin/custom";
        context.Request.Method = "PATCH";

        Assert.Equal("admin.patch", AdminAuditRules.MapAction(context));
    }

    [Fact]
    public void ExtractEntityId_finds_guid_segment()
    {
        var id = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            Request = { Path = $"/api/v1/admin/businesses/{id}/approve" }
        };

        Assert.Equal(id.ToString(), AdminAuditRules.ExtractEntityId(context));
    }

    [Fact]
    public void ExtractEntityId_returns_null_when_missing()
    {
        var context = new DefaultHttpContext
        {
            Request = { Path = "/api/v1/admin/custom" }
        };

        Assert.Null(AdminAuditRules.ExtractEntityId(context));
    }

    [Fact]
    public async Task InvokeAsync_writes_audit_log_for_admin_mutation()
    {
        var writer = new InMemoryAuditLogWriter();
        var context = new DefaultHttpContext();
        context.Request.Path = $"/api/v1/admin/businesses/{Guid.NewGuid()}/approve";
        context.Request.Method = "POST";
        context.Items[CorrelationIdMiddleware.ItemKey] = "corr-1";
        context.Response.StatusCode = StatusCodes.Status200OK;

        var middleware = new AuditMiddleware(_ => Task.CompletedTask, NullLogger<AuditMiddleware>.Instance);

        await middleware.InvokeAsync(
            context,
            writer,
            new StubCurrentUser("admin-1"));

        Assert.Single(writer.Entries);
        Assert.Equal(AuditActions.BusinessApproved, writer.Entries.First().Action);
    }
}

public sealed class TenantAccessMiddlewareInvokeTests
{
    [Fact]
    public async Task Allows_matching_tenant()
    {
        var tenant = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/tenant/profile";
        context.Request.Headers[TenantAccessMiddleware.TenantHeaderName] = tenant.ToString();
        var nextCalled = false;

        var tenantContext = new TenantContext();
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(tenantContext);
        context.RequestServices = services.BuildServiceProvider();

        var middleware = new TenantAccessMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantAccessMiddleware>.Instance);

        await middleware.InvokeAsync(
            context,
            new StubCurrentUser("user", tenant),
            new InMemoryAuditLogWriter());

        Assert.True(nextCalled);
        Assert.True(tenantContext.IsTenantFilterActive);
    }

    [Fact]
    public async Task Rejects_missing_tenant_header()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/tenant/profile";
        context.Response.Body = new MemoryStream();

        var middleware = new TenantAccessMiddleware(_ => Task.CompletedTask, NullLogger<TenantAccessMiddleware>.Instance);

        await middleware.InvokeAsync(
            context,
            new StubCurrentUser("user", Guid.NewGuid()),
            new InMemoryAuditLogWriter());

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }
}

internal sealed record StubCurrentUser(string UserId, Guid? TenantGuid = null, bool HasMfa = false) : ICurrentUser
{
    public IReadOnlyCollection<string> Roles { get; } = ["business"];

    public Domain.Tenancy.TenantId? TenantId =>
        TenantGuid is null
            ? null
            : Domain.Tenancy.TenantId.Create(TenantGuid.Value).Value;
}
