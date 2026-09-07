namespace Adeni.Api.Extensions;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

public static class RateLimitingExtensions
{
    public const string WebhookPolicy = "webhook";
    public const string AuthSyncPolicy = "auth-sync";
    public const string PublicMutationPolicy = "public-mutation";

    public static IServiceCollection AddAdeniRateLimiting(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        var isRelaxed = environment.IsDevelopment() || environment.EnvironmentName == "Testing";

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(WebhookPolicy, context =>
                CreateFixedWindowLimiter(context, isRelaxed ? 10_000 : 120, TimeSpan.FromMinutes(1)));

            options.AddPolicy(AuthSyncPolicy, context =>
                CreateFixedWindowLimiter(context, isRelaxed ? 10_000 : 30, TimeSpan.FromMinutes(1)));

            options.AddPolicy(PublicMutationPolicy, context =>
                CreateFixedWindowLimiter(context, isRelaxed ? 10_000 : 60, TimeSpan.FromMinutes(1)));
        });

        return services;
    }

    public static WebApplication UseAdeniRateLimiting(this WebApplication app)
    {
        app.UseRateLimiter();
        return app;
    }

    private static RateLimitPartition<string> CreateFixedWindowLimiter(
        HttpContext context,
        int permitLimit,
        TimeSpan window)
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            });
    }
}
