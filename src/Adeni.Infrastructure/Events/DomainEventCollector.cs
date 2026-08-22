namespace Adeni.Infrastructure.Events;

using Adeni.Application.Events;
using Adeni.Domain.Events;

public sealed class DomainEventCollector : IDomainEventCollector
{
    private readonly List<IDomainEvent> _events = [];

    public void Add(IDomainEvent domainEvent) => _events.Add(domainEvent);

    public IReadOnlyList<IDomainEvent> TakeAll()
    {
        var snapshot = _events.ToArray();
        _events.Clear();
        return snapshot;
    }
}
