namespace Adeni.Application.Events;

using Adeni.Domain.Events;

public interface IDomainEventCollector
{
    void Add(IDomainEvent domainEvent);

    IReadOnlyList<IDomainEvent> TakeAll();
}
