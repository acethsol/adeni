namespace Adeni.Infrastructure.Tests.Auditing;

using Adeni.Domain.Auditing;
using Adeni.Infrastructure.Auditing;

public sealed class InMemoryAuditLogWriterTests
{
    [Fact]
    public async Task WriteAsync_stores_entry()
    {
        var writer = new InMemoryAuditLogWriter();
        var entry = new AuditEntry(
            Guid.NewGuid(),
            "admin",
            AuditActions.BusinessApproved,
            "admin",
            Guid.NewGuid().ToString(),
            "corr",
            DateTimeOffset.UtcNow);

        await writer.WriteAsync(entry);

        Assert.Single(writer.Entries);
        Assert.Equal(entry, writer.Entries.First());
    }
}
