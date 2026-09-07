namespace Adeni.Api.Extensions;

using Adeni.Application.Auth;
using Serilog;

public static class CorsExtensions
{
    public const string ClientPolicy = "AdeniClients";

    public static IServiceCollection AddAdeniCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
            ?? new CorsOptions();
        var origins = corsOptions.AllowedOrigins;

        if (origins.Length == 0 && (environment.IsStaging() || environment.IsProduction()))
        {
            Log.Warning(
                "Cors:AllowedOrigins is empty in {Environment}. Cross-origin requests will be denied.",
                environment.EnvironmentName);
        }

        services.AddCors(options =>
        {
            options.AddPolicy(ClientPolicy, policy =>
            {
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                    return;
                }

                if (environment.IsDevelopment() || environment.EnvironmentName == "Testing")
                {
                    policy.SetIsOriginAllowed(_ => true)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });

        return services;
    }

    public static WebApplication UseAdeniCors(this WebApplication app)
    {
        app.UseCors(ClientPolicy);
        return app;
    }
}
