namespace Adeni.Infrastructure.Events;

using Adeni.Application.Events;
using Adeni.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = serviceProvider.GetServices(handlerType).Cast<object>().ToArray();

        if (handlers.Length == 0)
        {
            return Task.CompletedTask;
        }

        var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

        var tasks = handlers
            .Select(handler => (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!)
            .ToArray();

        return Task.WhenAll(tasks);
    }

    public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
        where T : IDomainEvent =>
        PublishAsync((IDomainEvent)domainEvent, cancellationToken);
}
