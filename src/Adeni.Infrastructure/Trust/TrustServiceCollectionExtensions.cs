namespace Adeni.Infrastructure.Trust;

using Microsoft.Extensions.DependencyInjection;

public static class TrustServiceCollectionExtensions
{
    public static IServiceCollection AddTrustModule(this IServiceCollection services)
    {
        services.AddScoped<Application.Trust.IVerificationBadgeService, VerificationBadgeService>();
        return services;
    }
}
