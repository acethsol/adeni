namespace Adeni.Infrastructure.Messaging;

using Adeni.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services)
    {
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<ICustomerCommunicationActivityQuery, CustomerCommunicationActivityQuery>();
        services.AddScoped<IFaqAutoResponder, FaqAutoResponder>();
        services.AddSingleton<IMessagingRealtimePublisher, NoOpMessagingRealtimePublisher>();
        return services;
    }
}
