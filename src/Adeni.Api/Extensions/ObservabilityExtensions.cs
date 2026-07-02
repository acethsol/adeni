namespace Adeni.Api.Extensions;

using Adeni.Application.Observability;
using Azure.Monitor.OpenTelemetry.AspNetCore;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddAdeniObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        if (!options.Enabled)
        {
            return services;
        }

        var connectionString = options.ConnectionString
            ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        services.AddOpenTelemetry()
            .UseAzureMonitor(monitor => monitor.ConnectionString = connectionString);

        return services;
    }
}
