namespace Adeni.Api.Extensions;

public static class CorsExtensions
{
    public const string ClientPolicy = "AdeniClients";

    public static IServiceCollection AddAdeniCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var origins = configuration.GetSection(Application.Auth.CorsOptions.SectionName)
            .Get<string[]>() ?? [];

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

                if (environment.IsDevelopment() || environment.IsStaging() || environment.EnvironmentName == "Testing")
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
