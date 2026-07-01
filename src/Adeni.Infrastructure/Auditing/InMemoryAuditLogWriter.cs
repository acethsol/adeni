namespace Adeni.Infrastructure.Auditing;

using System.Collections.Concurrent;
using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;

public sealed class InMemoryAuditLogWriter : IAuditLogWriter
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    public IReadOnlyCollection<AuditEntry> Entries =>
        _entries.ToArray();

    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }
}
