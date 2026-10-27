namespace Adeni.Infrastructure.Tests.Tenancy;

using Adeni.Infrastructure.Tests.SqlServer;
using Adeni.Application.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Domain.Finance;
using Adeni.Domain.Identity;
using Adeni.Domain.Messaging;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Messaging;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

[Trait("Category", "SqlServer")]
[Collection(SqlServerTestCollection.Name)]
public sealed class CustomerProfileSqlServerTests
{
    [CustomerProfileSqlServerFact]
    public async Task SourceQueries_ExecuteAgainstPublishedSqlSchema_WithOrderingScopingAndCurrencyTotals()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__AdeniDb")
            ?? throw new InvalidOperationException("SQL profile tests require ConnectionStrings__AdeniDb.");
        await using var db = new AdeniDbContext(new DbContextOptionsBuilder<AdeniDbContext>().UseSqlServer(connection).Options, new TenantContext());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var tenant = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var roster = Guid.NewGuid();
        var branch = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenant, Name = "SQL profile test", CreatedAt = now });
        db.BusinessProfiles.Add(new BusinessProfile { TenantId = tenant, CategorySlug = "barbers", Phone = "", UpdatedAt = now });
        db.Customers.Add(new Customer { Id = customer, Auth0Sub = $"sql-profile-{customer:N}", Name = "Profile SQL", CreatedAt = now });
        db.TenantCustomers.Add(new TenantCustomer { Id = roster, TenantId = tenant, CustomerId = customer, DisplayName = "Profile SQL", CreatedAt = now, UpdatedAt = now, LastSeenAt = now });
        db.BusinessLocations.Add(new BusinessLocation { Id = branch, TenantId = tenant, Name = "Test", Slug = $"profile-{branch:N}",
            MarketId = "lagos", CreatedAt = now, UpdatedAt = now });
        var service = new ServiceOffering { Id = Guid.NewGuid(), TenantId = tenant, Name = "Hair care", Currency = "NGN", DurationMinutes = 30, CreatedAt = now, UpdatedAt = now };
        db.ServiceOfferings.Add(service);
        var past = new BookingRecord { Id = Guid.NewGuid(), TenantId = tenant, CustomerId = customer, ServiceOfferingId = service.Id,
            BusinessLocationId = branch, StartAt = now.AddDays(-1), EndAt = now.AddDays(-1).AddHours(1), Status = BookingStatus.Paid, CreatedAt = now.AddDays(-2), UpdatedAt = now };
        db.Bookings.AddRange(past, new BookingRecord { Id = Guid.NewGuid(), TenantId = tenant, CustomerId = customer, ServiceOfferingId = service.Id,
            BusinessLocationId = branch, StartAt = now.AddDays(1), EndAt = now.AddDays(1).AddHours(1), Status = BookingStatus.Confirmed, CreatedAt = now, UpdatedAt = now });
        db.PaymentIntents.Add(new PaymentIntentRecord { Id = Guid.NewGuid(), TenantId = tenant, BookingId = past.Id, Status = PaymentIntentStatus.Completed,
            Amount = 12.50m, Currency = "CAD", CreatedAt = now.AddHours(-1), UpdatedAt = now.AddMinutes(-1), ProviderReference = $"sql-{Guid.NewGuid():N}" });
        db.QueueTickets.Add(new QueueTicket { Id = Guid.NewGuid(), TenantId = tenant, TenantCustomerId = roster, BookingId = past.Id,
            BusinessLocationId = branch, PublicToken = Guid.NewGuid().ToString("N"), JoinedAt = now.AddDays(-1), CreatedAt = now.AddDays(-1), UpdatedAt = now, Status = QueueTicketStatus.Done });
        var conversation = new Conversation { Id = Guid.NewGuid(), TenantId = tenant, CustomerId = customer, CreatedAt = now, LastMessageAt = now };
        var participant = new ConversationParticipant { Id = Guid.NewGuid(), TenantId = tenant, ConversationId = conversation.Id,
            ParticipantType = ConversationParticipantType.Customer, CustomerId = customer, JoinedAt = now };
        db.Conversations.Add(conversation);
        db.ConversationParticipants.Add(participant);
        db.ConversationMessages.Add(new ConversationMessage { Id = Guid.NewGuid(), TenantId = tenant, ConversationId = conversation.Id,
            SenderParticipantId = participant.Id, CreatedAt = now.AddMinutes(-1), Body = "not exposed" });
        await db.SaveChangesAsync();

        var scope = new CustomerProfileScope(tenant, roster, customer, branch);
        var booking = new CustomerBookingActivityQuery(db);
        var profile = await booking.GetProfileAsync(scope, now, true, default);
        Assert.Equal(new CustomerVisitSummary(1, 0), profile.Visits);
        Assert.Equal("Hair care", profile.NextAppointment!.ServiceName);
        Assert.Equal(2, (await booking.GetActivityAsync(scope, now.AddDays(-90), now, 3, default)).Items.Count(x => x.Source == "booking"));
        Assert.Empty((await booking.GetActivityAsync(scope with { LocationId = Guid.NewGuid() }, now.AddDays(-90), now, 3, default)).Items);
        var finance = new CustomerCommerceQuery(db, new CustomerCommerceBookingQuery(db));
        Assert.Equal(new CustomerCurrencyTotal("CAD", 12.50m), Assert.Single(await finance.GetTotalsAsync(scope, now, default)));
        Assert.Single((await finance.GetActivityAsync(scope, now.AddDays(-90), now, default)).Items);
        Assert.Single((await new CustomerCommunicationActivityQuery(db).GetActivityAsync(scope with { LocationId = null }, now.AddDays(-90), now, default)).Items);
        await transaction.RollbackAsync();
    }
}

file sealed class CustomerProfileSqlServerFactAttribute : FactAttribute
{
    public CustomerProfileSqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ADENI_SQLSERVER_TESTS") != "1")
            Skip = "Set ADENI_SQLSERVER_TESTS=1 and ConnectionStrings__AdeniDb after publishing the database.";
    }
}
