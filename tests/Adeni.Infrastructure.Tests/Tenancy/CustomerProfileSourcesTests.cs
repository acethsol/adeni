namespace Adeni.Infrastructure.Tests.Tenancy;

using Adeni.Application.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Domain.Finance;
using Adeni.Domain.Messaging;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Messaging;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerProfileSourcesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private readonly CustomerProfileScope scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);

    private static AdeniDbContext Db() => new(new DbContextOptionsBuilder<AdeniDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new TenantContext());

    private BookingRecord Booking(Guid? tenant = null, Guid? customer = null, Guid? location = null, BookingStatus status = BookingStatus.Completed, int days = -1) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant ?? scope.TenantId, CustomerId = customer ?? scope.CustomerId!.Value,
        BusinessLocationId = location, StartAt = Now.AddDays(days), EndAt = Now.AddDays(days).AddHours(1),
        Status = status, CreatedAt = Now.AddDays(days - 2), UpdatedAt = Now.AddDays(days),
        ServiceOffering = new ServiceOffering { Id = Guid.NewGuid(), TenantId = tenant ?? scope.TenantId, Name = "Hair care", Currency = "NGN" },
    };

    private QueueTicket Queue(Guid? booking = null, Guid? tenant = null, Guid? roster = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant ?? scope.TenantId, TenantCustomerId = roster ?? scope.TenantCustomerId,
        BookingId = booking, Status = QueueTicketStatus.Done, JoinedAt = Now.AddDays(-1), CompletedAt = Now.AddHours(-2),
        PublicToken = Guid.NewGuid().ToString(),
    };

    [Fact]
    public async Task BookingProfile_DeduplicatesAppointmentsAndQueue_ExcludesOtherCustomersAndIncompleteVisits()
    {
        await using var db = Db();
        var completed = Booking();
        db.Bookings.AddRange(completed, Booking(status: BookingStatus.Paid), Booking(status: BookingStatus.Confirmed),
            Booking(customer: Guid.NewGuid()), Booking(tenant: Guid.NewGuid()), Booking(status: BookingStatus.Cancelled));
        db.QueueTickets.AddRange(Queue(completed.Id), Queue(), Queue(tenant: Guid.NewGuid()), Queue(roster: Guid.NewGuid()));
        await db.SaveChangesAsync();
        var source = new CustomerBookingActivityQuery(db);
        var result = await source.GetProfileAsync(scope, Now, true, default);
        Assert.Equal(new CustomerVisitSummary(2, 1), result.Visits);
        Assert.Null(result.NextAppointment);
        Assert.Null((await source.GetProfileAsync(scope, Now, false, default)).Visits);
    }

    [Fact]
    public async Task BookingProfile_NextAppointment_IsEligibleAndScoped()
    {
        await using var db = Db();
        var branch = Guid.NewGuid();
        var next = Booking(location: branch, status: BookingStatus.Pending, days: 2);
        db.Bookings.AddRange(next, Booking(status: BookingStatus.Cancelled, days: 1),
            Booking(status: BookingStatus.Completed, days: 1), Booking(location: Guid.NewGuid(), status: BookingStatus.Confirmed, days: 1),
            Booking(location: branch, status: BookingStatus.Confirmed, days: 3));
        await db.SaveChangesAsync();
        var result = await new CustomerBookingActivityQuery(db).GetProfileAsync(scope with { LocationId = branch }, Now, true, default);
        Assert.Equal(next.Id, result.NextAppointment!.BookingId);
        Assert.Equal("Pending", result.NextAppointment.Status);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(25)]
    public async Task BookingActivity_HistoryCapsAreAppliedBeforeEventsAndNoSyntheticStatusTransitions(int limit)
    {
        await using var db = Db();
        db.Bookings.AddRange(Enumerable.Range(1, 30).Select(i => Booking(days: -i)));
        db.QueueTickets.AddRange(Enumerable.Range(0, 7).Select(_ => Queue()));
        await db.SaveChangesAsync();
        var batch = await new CustomerBookingActivityQuery(db).GetActivityAsync(scope, Now.AddDays(-90), Now, limit, default);
        Assert.Equal(limit, batch.Items.Where(x => x.Source == "booking").Select(x => x.SourceId).Distinct().Count());
        Assert.Equal(5, batch.Items.Where(x => x.Source == "queue").Select(x => x.SourceId).Distinct().Count());
        Assert.True(batch.IsTruncated);
        Assert.DoesNotContain(batch.Items, x => x.Kind is "bookingCompleted" or "bookingCancelled");
        Assert.All(batch.Items.Where(x => x.Kind == "queueCompleted"), x => Assert.Equal(Now.AddHours(-2), x.OccurredAt));
    }

    [Fact]
    public async Task BookingActivity_ExcludesFutureAndOutOfWindowEvents_UsesActualNoShowTime()
    {
        await using var db = Db();
        db.Bookings.AddRange(Booking(days: 2), Booking(days: -100));
        var noShow = Queue();
        noShow.Status = QueueTicketStatus.NoShow;
        noShow.CompletedAt = null;
        noShow.NoShowAt = Now.AddHours(-1);
        db.QueueTickets.Add(noShow);
        await db.SaveChangesAsync();
        var batch = await new CustomerBookingActivityQuery(db).GetActivityAsync(scope, Now.AddDays(-90), Now, 3, default);
        Assert.DoesNotContain(batch.Items, x => x.Source == "booking");
        Assert.Equal(Now.AddHours(-1), Assert.Single(batch.Items, x => x.Kind == "queueNoShow").OccurredAt);
    }

    [Fact]
    public async Task Commerce_TotalsSeparateCurrenciesAndExcludeRefundsUnlinkedOtherTenantAndTips()
    {
        await using var db = Db();
        var branch = Guid.NewGuid();
        var booking = Booking(location: branch);
        var otherCustomer = Booking(customer: Guid.NewGuid());
        var otherBranch = Booking(location: Guid.NewGuid());
        db.Bookings.AddRange(booking, otherCustomer, otherBranch);
        PaymentIntentRecord Payment(Guid? bookingId, PaymentIntentStatus status, decimal amount, string currency = "NGN", Guid? tenant = null) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenant ?? scope.TenantId, BookingId = bookingId, Status = status,
            Amount = amount, TipAmount = 20, Currency = currency, UpdatedAt = Now.AddHours(-1),
        };
        db.PaymentIntents.AddRange(Payment(booking.Id, PaymentIntentStatus.Completed, 100), Payment(booking.Id, PaymentIntentStatus.Completed, 40, "CAD"),
            Payment(booking.Id, PaymentIntentStatus.Refunded, 200), Payment(booking.Id, PaymentIntentStatus.RefundPending, 300),
            Payment(booking.Id, PaymentIntentStatus.Pending, 400), Payment(booking.Id, PaymentIntentStatus.Failed, 500),
            Payment(null, PaymentIntentStatus.Completed, 600), Payment(otherCustomer.Id, PaymentIntentStatus.Completed, 700),
            Payment(booking.Id, PaymentIntentStatus.Completed, 800, tenant: Guid.NewGuid()), Payment(otherBranch.Id, PaymentIntentStatus.Completed, 900));
        await db.SaveChangesAsync();
        var source = new CustomerCommerceQuery(db, new CustomerCommerceBookingQuery(db));
        var scoped = scope with { LocationId = branch };
        var totals = await source.GetTotalsAsync(scoped, Now, default);
        Assert.Equal(100, totals.Single(x => x.Currency == "NGN").Amount);
        Assert.Equal(40, totals.Single(x => x.Currency == "CAD").Amount);
        var items = (await source.GetActivityAsync(scoped, Now.AddDays(-90), Now, default)).Items;
        Assert.Equal(2, items.Count);
        Assert.All(items, x => Assert.Equal(Now.AddHours(-1), x.OccurredAt));
        Assert.Empty(await source.GetTotalsAsync(scope with { CustomerId = null }, Now, default));
    }

    [Fact]
    public async Task Commerce_ActivityHasBoundedCoverage()
    {
        await using var db = Db();
        var booking = Booking();
        db.Bookings.Add(booking);
        db.PaymentIntents.AddRange(Enumerable.Range(0, 102).Select(i => new PaymentIntentRecord
        {
            Id = Guid.NewGuid(), TenantId = scope.TenantId, BookingId = booking.Id, Amount = 1, Status = PaymentIntentStatus.Completed,
            UpdatedAt = Now.AddMinutes(-i),
        }));
        await db.SaveChangesAsync();
        var batch = await new CustomerCommerceQuery(db, new CustomerCommerceBookingQuery(db)).GetActivityAsync(scope, Now.AddDays(-90), Now, default);
        Assert.Equal(100, batch.Items.Count);
        Assert.True(batch.IsTruncated);
    }

    [Fact]
    public async Task Messages_OnlyVisibleCustomerMetadata_NoBodyAndNoReadReceiptMutation()
    {
        await using var db = Db();
        var conversation = new Conversation { Id = Guid.NewGuid(), TenantId = scope.TenantId, CustomerId = scope.CustomerId!.Value };
        var other = new Conversation { Id = Guid.NewGuid(), TenantId = scope.TenantId, CustomerId = Guid.NewGuid() };
        ConversationMessage Message(Conversation c, MessageModerationStatus status = MessageModerationStatus.None, bool deleted = false) => new()
        {
            Id = Guid.NewGuid(), TenantId = c.TenantId, ConversationId = c.Id, Conversation = c,
            Body = "Private body must never be returned", CreatedAt = Now.AddHours(-1), ModerationStatus = status, DeletedAt = deleted ? Now : null,
        };
        db.Conversations.AddRange(conversation, other);
        db.ConversationMessages.AddRange(Message(conversation), Message(conversation, MessageModerationStatus.Hidden),
            Message(conversation, MessageModerationStatus.Flagged), Message(conversation, deleted: true), Message(other));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var source = new CustomerCommunicationActivityQuery(db);
        var result = await source.GetActivityAsync(scope, Now.AddDays(-90), Now, default);
        var item = Assert.Single(result.Items);
        Assert.Equal(conversation.Id, item.SourceId);
        Assert.DoesNotContain("Private", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Empty((await source.GetActivityAsync(scope with { LocationId = Guid.NewGuid() }, Now.AddDays(-90), Now, default)).Items);
        Assert.Empty((await source.GetActivityAsync(scope with { CustomerId = null }, Now.AddDays(-90), Now, default)).Items);
    }

    [Fact]
    public void Cursor_RejectsTamperingAndDifferentProtectionPurpose()
    {
        var provider = new EphemeralDataProtectionProvider();
        var codec = new CustomerActivityCursorCodec(provider);
        var position = new CustomerActivityCursor("tenant/customer/actor/policy", Now, Now.AddMinutes(-1), "id");
        var cursor = codec.Encode(position);
        Assert.Equal(position, codec.Decode(cursor));
        Assert.Null(codec.Decode(cursor[..^8] + "tampered"));
        Assert.Null(codec.Decode(provider.CreateProtector("other").Protect("{}")));
        Assert.Null(codec.Decode("not a cursor"));
    }
}
