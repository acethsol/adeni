namespace Adeni.Infrastructure.Tests.Events;

using Adeni.Application.Events;
using Adeni.Domain.Events;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PostSaveFailureTests
{
    [Fact]
    public async Task SaveChangesAsync_HandlerFailure_LeavesPersistedDataAndCanFlushPartialSideEffects()
    {
        // Characterizes the current limitation; revisit when the durable outbox is implemented.
        var options = new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenantId = Guid.NewGuid();
        var collector = new DomainEventCollector();
        var queue = new AfterCommitWorkQueue();
        var dispatcher = new FailingDispatcher(queue);
        await using (var db = new AdeniDbContext(options, new TenantContext(), collector, dispatcher, queue))
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Persisted before handler failure" });
            collector.Add(new ProbeEvent());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        await using var read = new AdeniDbContext(options, new TenantContext());
        Assert.True(await read.Tenants.AnyAsync(t => t.Id == tenantId));
        Assert.True(dispatcher.WorkRan);
        Assert.Empty(collector.TakeAll());
    }

    private sealed record ProbeEvent : IDomainEvent;

    private sealed class FailingDispatcher(AfterCommitWorkQueue queue) : IDomainEventDispatcher
    {
        public bool WorkRan { get; private set; }

        public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            await queue.EnqueueAsync(_ => { WorkRan = true; return Task.CompletedTask; }, cancellationToken);
            throw new InvalidOperationException("Post-save handler failed.");
        }

        public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent =>
            PublishAsync((IDomainEvent)domainEvent, cancellationToken);
    }
}
