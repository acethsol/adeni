namespace Adeni.Infrastructure.Events;

using Adeni.Application.Events;
using Microsoft.Extensions.DependencyInjection;

public static class DomainEventServiceCollectionExtensions
{
    public static IServiceCollection AddAdeniDomainEvents(this IServiceCollection services)
    {
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services;
    }
}
