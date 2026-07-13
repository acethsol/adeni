namespace Adeni.Infrastructure.Translation;

using Adeni.Application.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class TranslationServiceCollectionExtensions
{
    public static IServiceCollection AddAdeniTranslation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<TranslationOptions>()
            .Bind(configuration.GetSection(TranslationOptions.SectionName));

        var translationOptions = configuration
            .GetSection(TranslationOptions.SectionName)
            .Get<TranslationOptions>() ?? new TranslationOptions();

        if (!translationOptions.Enabled && !environment.IsDevelopment() && environment.EnvironmentName != "Testing")
        {
            throw new InvalidOperationException(
                $"Azure Translator is required in '{environment.EnvironmentName}'. Set Translation:Key and Translation:Region.");
        }

        services.AddHttpClient("azure-translator", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddHttpClient("dev-translator", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<ITranslationService, TranslationService>();
        return services;
    }
}
