namespace Adeni.Infrastructure.Messaging;

using Microsoft.Extensions.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services)
    {
        services.AddScoped<Application.Messaging.IMessageThreadService, MessageThreadService>();
        services.AddScoped<Application.Messaging.IFaqAutoResponder, FaqAutoResponder>();
        return services;
    }
}
