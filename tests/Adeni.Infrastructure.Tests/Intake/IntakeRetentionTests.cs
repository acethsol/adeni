namespace Adeni.Infrastructure.Tests.Intake;

using Adeni.Application.Intake;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class IntakeRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Bookings : IIntakeBookingQuery
    {
        public IntakeBookingFacts Facts = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, Now.AddMonths(-25), Now.AddMonths(-25).AddHours(1), 5, Now.AddMonths(-25));
        public Task<IntakeBookingFacts?> GetAsync(Guid tenant, Guid id, CancellationToken ct) => Task.FromResult<IntakeBookingFacts?>(Facts.TenantId == tenant && Facts.Id == id ? Facts : null);
        public Task<IntakeBookingFacts?> GetOwnedAsync(string sub, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListForCustomerAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(id == Facts.CustomerId ? [Facts.Id] : []);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public readonly AdeniDbContext Db = new(new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new TenantContext());
        public readonly Bookings Bookings = new();
        public IntakeRetentionService Service => new(Db, Bookings, new Clock());
        public Guid Tenant => Bookings.Facts.TenantId;
        public IntakeFormSubmission Submission = null!;
        public BookingIntakeAssignment Assignment = null!;
        public IntakeFormVersion Version = null!;
        public async Task Seed(bool submitted = true)
        {
            var template = new IntakeFormTemplate { Id = Guid.NewGuid(), TenantId = Tenant, Name = "Consultation" };
            Version = new() { Id = Guid.NewGuid(), TenantId = Tenant, TemplateId = template.Id, VersionNumber = 1, PublishedAt = Now.AddYears(-3), Title = "Consultation" };
            Assignment = new() { Id = Guid.NewGuid(), TenantId = Tenant, BookingId = Bookings.Facts.Id, TemplateId = template.Id, FormVersionId = Version.Id, AssignedAt = Now.AddYears(-3) };
            Db.AddRange(template, Version, Assignment);
            if (submitted)
            {
                Submission = new() { Id = Guid.NewGuid(), TenantId = Tenant, AssignmentId = Assignment.Id, FormVersionId = Version.Id, SubmittedAt = Now.AddMonths(-25), IdempotencyKey = "retry" };
                var field = new IntakeFormField { Id = Guid.NewGuid(), TenantId = Tenant, VersionId = Version.Id, Label = "Preference", FieldType = IntakeFieldType.LongText };
                var option = new IntakeFieldChoice { Id = Guid.NewGuid(), TenantId = Tenant, VersionId = Version.Id, FieldId = field.Id, Label = "Quiet" };
                var answer = new IntakeFormAnswer { Id = Guid.NewGuid(), TenantId = Tenant, SubmissionId = Submission.Id, VersionId = Version.Id, FieldId = field.Id, TextValue = "sensitive-sentinel" };
                Db.AddRange(Submission, field, option, answer);
                Db.Add(new IntakeAnswerChoice { TenantId = Tenant, AnswerId = answer.Id, VersionId = Version.Id, FieldId = field.Id, ChoiceId = option.Id });
            }
            await Db.SaveChangesAsync();
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    [Theory]
    [InlineData(-23, 0)]
    [InlineData(-24, 1)]
    [InlineData(-25, 1)]
    public async Task Completed_UsesTwentyFourCalendarMonths(int months, int expected)
    {
        await using var f = new Fixture(); await f.Seed();
        f.Bookings.Facts = f.Bookings.Facts with { StartAt = Now.AddMonths(months) };
        Assert.Equal(expected, (await f.Service.PurgeTenantAsync(f.Tenant)).SubmissionsPurged);
        Assert.Equal(expected == 0 ? 1 : 0, await f.Db.Set<IntakeFormAnswer>().CountAsync());
    }
    [Theory]
    [InlineData(-89, 0)]
    [InlineData(-90, 1)]
    [InlineData(-91, 1)]
    public async Task Pending_ClosedAssignmentsExpireAtNinetyDays(int days, int expected)
    {
        await using var f = new Fixture(); await f.Seed(false);
        f.Bookings.Facts = f.Bookings.Facts with { StartAt = Now.AddDays(days), Status = 0 };
        Assert.Equal(expected, (await f.Service.PurgeTenantAsync(f.Tenant)).AssignmentsRemoved);
    }
    [Fact]
    public async Task Hold_BlocksNormalPurge_ReleaseImmediatelyPurgesExpiredContent()
    {
        await using var f = new Fixture(); await f.Seed();
        Assert.True((await f.Service.PlaceHoldAsync(f.Tenant, f.Submission.Id, "admin", "legalProceeding")).IsSuccess);
        Assert.Equal(1, (await f.Service.PurgeTenantAsync(f.Tenant)).Held);
        Assert.Single(await f.Db.Set<IntakeFormAnswer>().ToArrayAsync());
        Assert.True((await f.Service.ReleaseHoldAsync(f.Tenant, f.Submission.Id, "other-admin")).IsSuccess);
        Assert.Empty(await f.Db.Set<IntakeFormAnswer>().ToArrayAsync());
        var hold = await f.Db.Set<IntakeSubmissionHold>().SingleAsync();
        Assert.Equal("admin", hold.PlacedBy); Assert.Equal("other-admin", hold.ReleasedBy); Assert.Equal(Now, hold.ReleasedAt);
        Assert.NotNull(f.Submission.PurgedAt);
    }
    [Fact]
    public async Task Privacy_HoldDefersDeletion_ReleaseHonorsPendingPrivacyRequestBeforeExpiry()
    {
        await using var f = new Fixture(); await f.Seed();
        f.Bookings.Facts = f.Bookings.Facts with { StartAt = Now.AddMonths(-1) };
        await f.Service.PlaceHoldAsync(f.Tenant, f.Submission.Id, "admin", "regulatory");
        await f.Service.StagePrivacyPurgeAsync(f.Bookings.Facts.CustomerId); await f.Db.SaveChangesAsync();
        Assert.Single(await f.Db.Set<IntakeFormAnswer>().ToArrayAsync());
        Assert.NotNull(f.Submission.PrivacyDeletionRequestedAt);
        await f.Service.ReleaseHoldAsync(f.Tenant, f.Submission.Id, "admin");
        Assert.Empty(await f.Db.Set<IntakeFormAnswer>().ToArrayAsync());
    }
    [Fact]
    public async Task Purge_RemovesAllResponseContent_RetainsMinimalReceiptAndReusableDefinition_Idempotently()
    {
        await using var f = new Fixture(); await f.Seed();
        await f.Service.PurgeTenantAsync(f.Tenant);
        Assert.Empty(await f.Db.Set<IntakeFormAnswer>().ToArrayAsync());
        Assert.Empty(await f.Db.Set<IntakeAnswerChoice>().ToArrayAsync());
        Assert.Equal("", f.Submission.IdempotencyKey);
        Assert.DoesNotContain("sensitive-sentinel", System.Text.Json.JsonSerializer.Serialize(f.Submission));
        Assert.Single(await f.Db.Set<IntakeFormVersion>().ToArrayAsync());
        Assert.Single(await f.Db.Set<IntakeFormField>().ToArrayAsync());
        Assert.Equal(0, (await f.Service.PurgeTenantAsync(f.Tenant)).SubmissionsPurged);
    }
    [Fact]
    public async Task TenantIsolationAndInvalidHold_DoNotDeleteOrLeakContent()
    {
        await using var f = new Fixture(); await f.Seed();
        Assert.Equal(0, (await f.Service.PurgeTenantAsync(Guid.NewGuid())).SubmissionsPurged);
        Assert.True((await f.Service.PlaceHoldAsync(Guid.NewGuid(), f.Submission.Id, "admin", "regulatory")).IsFailure);
        var invalid = await f.Service.PlaceHoldAsync(f.Tenant, f.Submission.Id, "admin", "sensitive-sentinel");
        Assert.True(invalid.IsFailure);
        Assert.DoesNotContain("sensitive-sentinel", System.Text.Json.JsonSerializer.Serialize(invalid.Error));
        Assert.Single(await f.Db.Set<IntakeFormAnswer>().ToArrayAsync());
    }
    [Fact]
    public async Task PrivacyWithoutHold_DeletesBeforeOrdinaryExpiry()
    {
        await using var f = new Fixture(); await f.Seed();
        f.Bookings.Facts = f.Bookings.Facts with { StartAt = Now.AddDays(-1) };
        await f.Service.StagePrivacyPurgeAsync(f.Bookings.Facts.CustomerId); await f.Db.SaveChangesAsync();
        Assert.Empty(await f.Db.Set<IntakeFormAnswer>().ToArrayAsync());
        Assert.NotNull(f.Submission.PurgedAt);
    }
}
