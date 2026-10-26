namespace Adeni.Infrastructure.Calendar;

using Adeni.Application.Calendar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class CalendarServiceCollectionExtensions
{
    public static IServiceCollection AddCalendarModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<CalendarOptions>().Bind(configuration.GetSection("Calendar"))
            .Configure(o => o.PortalBaseUrl = configuration["Portal:PublicBaseUrl"] ?? "")
            .Validate(x => x.IsValid(), "Enabled Calendar providers require HTTPS public origins, OAuth credentials and versioned 256-bit encryption keys.").ValidateOnStart();
        services.AddHttpClient<GoogleCalendarProvider>(c => c.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
        services.AddHttpClient<MicrosoftCalendarProvider>(c => c.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
        services.AddTransient<IExternalCalendarProvider>(sp => sp.GetRequiredService<GoogleCalendarProvider>());
        services.AddTransient<IExternalCalendarProvider>(sp => sp.GetRequiredService<MicrosoftCalendarProvider>());
        services.AddScoped<CalendarCredentialProtector>();
        services.AddScoped<CalendarConnectionSession>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IExternalCalendarAvailability, ExternalCalendarAvailability>();
        services.AddScoped<CalendarReconciliationJob>();
        return services;
    }
}
