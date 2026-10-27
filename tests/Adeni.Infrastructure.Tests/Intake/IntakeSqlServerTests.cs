namespace Adeni.Infrastructure.Tests.Intake;

using Adeni.Infrastructure.Tests.SqlServer;
using Adeni.Application.Intake;
using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Intake;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

[Trait("Category","SqlServer")]
[Collection(SqlServerTestCollection.Name)]
public sealed class IntakeSqlServerTests
{
    private static readonly DateTimeOffset Now = new(2026,10,25,12,0,0,TimeSpan.Zero);
    private sealed class Clock:TimeProvider {public override DateTimeOffset GetUtcNow()=>Now;}
    private static AdeniDbContext Db(IInterceptor? interceptor=null)
    {
        var builder=new DbContextOptionsBuilder<AdeniDbContext>().UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__AdeniDb")!);
        if(interceptor is not null)builder.AddInterceptors(interceptor);
        return new(builder.Options,new TenantContext());
    }
    private sealed class Access:IIntakeManagementAccessQuery{public Task<bool> CanManageAsync(Guid t,string a,CancellationToken ct)=>Task.FromResult(true);}
    private sealed class Facts(Guid tenant,Guid booking):IIntakeBookingQuery
    {
        public Task<IntakeBookingFacts?> GetAsync(Guid t,Guid b,CancellationToken ct)=>Task.FromResult<IntakeBookingFacts?>(t==tenant&&b==booking?new(b,t,Guid.Empty,null,null,Now.AddMonths(-25),Now.AddMonths(-25).AddHours(1),5,Now.AddMonths(-25)):null);
        public Task<IntakeBookingFacts?> GetOwnedAsync(string a,Guid b,CancellationToken ct)=>throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListForCustomerAsync(Guid c,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>([booking]);
    }
    private sealed record Seed(Guid Tenant,Guid Booking,Guid Service,Guid Template,Guid Version,Guid Assignment,Guid Submission,Guid Field);
    private static async Task<Seed> SeedAsync(AdeniDbContext db)
    {
        var tenant=Guid.NewGuid();var booking=Guid.NewGuid();var service=Guid.NewGuid();
        db.Add(new Tenant{Id=tenant,Name="Intake SQL",CreatedAt=Now});
        db.Add(new ServiceOffering{Id=service,TenantId=tenant,Name="Consultation service",Currency="NGN",CreatedAt=Now,UpdatedAt=Now});
        db.Add(new BookingRecord{Id=booking,TenantId=tenant,ServiceOfferingId=service,CustomerId=Guid.NewGuid(),StartAt=Now.AddMonths(-25),EndAt=Now.AddMonths(-25).AddHours(1),CreatedAt=Now.AddMonths(-26),UpdatedAt=Now.AddMonths(-25)});
        await db.SaveChangesAsync();
        var templates=new IntakeTemplateService(db,new Access());
        var created=(await templates.CreateAsync(tenant,"owner","Consultation")).Value!;var field=Guid.NewGuid();
        var saved=(await templates.SaveDraftAsync(tenant,"owner",created.Id,created.Revision,new("Consultation",null,null,[new(field,IntakeFieldType.LongText,"Preference",null,true,[]) ]))).Value!;
        var published=(await templates.PublishAsync(tenant,"owner",created.Id,saved.Revision)).Value!;
        var assignment=Guid.NewGuid();var submission=Guid.NewGuid();
        db.Add(new BookingIntakeAssignment{Id=assignment,TenantId=tenant,BookingId=booking,TemplateId=created.Id,FormVersionId=published.PublishedVersionId!.Value,AssignedAt=Now.AddMonths(-26)});
        db.Add(new IntakeFormSubmission{Id=submission,TenantId=tenant,AssignmentId=assignment,FormVersionId=published.PublishedVersionId.Value,SubmittedAt=Now.AddMonths(-25),IdempotencyKey="retry"});
        db.Add(new IntakeFormAnswer{Id=Guid.NewGuid(),TenantId=tenant,SubmissionId=submission,VersionId=published.PublishedVersionId.Value,FieldId=field,TextValue="sensitive-sql-sentinel"});
        await db.SaveChangesAsync();
        return new(tenant,booking,service,created.Id,published.PublishedVersionId.Value,assignment,submission,field);
    }
    [IntakeSqlFact]
    public async Task Sql_PurgeAndHoldsPreserveRelationalIntegrityAndRemoveActualContent()
    {
        await using var db=Db();await using var transaction=await db.Database.BeginTransactionAsync();var s=await SeedAsync(db);
        var retention=new IntakeRetentionService(db,new Facts(s.Tenant,s.Booking),new Clock());
        Assert.True((await retention.PlaceHoldAsync(s.Tenant,s.Submission,"admin","regulatory")).IsSuccess);
        Assert.Equal(1,(await retention.PurgeTenantAsync(s.Tenant)).Held);
        Assert.Single(await db.Set<IntakeFormAnswer>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
        Assert.True((await retention.ReleaseHoldAsync(s.Tenant,s.Submission,"admin-2")).IsSuccess);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Set<IntakeFormAnswer>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
        Assert.Empty(await db.Set<IntakeAnswerChoice>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
        var receipt=await db.Set<IntakeFormSubmission>().SingleAsync(x=>x.Id==s.Submission);
        Assert.NotNull(receipt.PurgedAt);Assert.Equal("",receipt.IdempotencyKey);
        Assert.DoesNotContain("sensitive-sql-sentinel",System.Text.Json.JsonSerializer.Serialize(receipt));
        Assert.Single(await db.Set<IntakeFormVersion>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
        Assert.Equal(0,(await retention.PurgeTenantAsync(s.Tenant)).SubmissionsPurged);
        await transaction.RollbackAsync();
    }
    [IntakeSqlFact]
    public async Task Sql_RejectsCrossVersionAnswersAndDuplicateSubmission_WithSavepointRollback()
    {
        await using var db=Db();await using var transaction=await db.Database.BeginTransactionAsync();var s=await SeedAsync(db);
        db.ChangeTracker.Clear();
        db.Add(new IntakeFormAnswer{Id=Guid.NewGuid(),TenantId=s.Tenant,SubmissionId=s.Submission,VersionId=Guid.NewGuid(),FieldId=s.Field,TextValue="never logged"});
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        db.Add(new IntakeFormSubmission{Id=Guid.NewGuid(),TenantId=s.Tenant,AssignmentId=s.Assignment,FormVersionId=s.Version,SubmittedAt=Now,IdempotencyKey="other"});
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        Assert.Single(await db.Set<IntakeFormSubmission>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
        await transaction.RollbackAsync();
    }
    [IntakeSqlFact]
    public async Task Sql_BookingAndPinnedRequirementsRollBackTogetherOnFailure()
    {
        await using var db=Db();await using var transaction=await db.Database.BeginTransactionAsync();var s=await SeedAsync(db);
        db.Add(new ServiceIntakeForm{TenantId=s.Tenant,ServiceOfferingId=s.Service,TemplateId=s.Template,UpdatedAt=Now});await db.SaveChangesAsync();
        var writer=new BookingIntakeAssignmentWriter(db);var prepared=await writer.PrepareAsync(s.Tenant,[s.Service,s.Service],default);
        Assert.Single(prepared.Value!);var booking=Guid.NewGuid();
        db.Add(new BookingRecord{Id=booking,TenantId=s.Tenant,ServiceOfferingId=s.Service,CustomerId=Guid.NewGuid(),StartAt=Now.AddDays(1),EndAt=Now.AddDays(1).AddHours(1),CreatedAt=Now,UpdatedAt=Now});
        writer.Stage(s.Tenant,booking,prepared.Value!,Now);
        db.Add(new IntakeFormAnswer{Id=Guid.NewGuid(),TenantId=s.Tenant,SubmissionId=s.Submission,VersionId=s.Version,FieldId=Guid.NewGuid(),TextValue="not persisted"});
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        Assert.False(await db.Bookings.AnyAsync(x=>x.Id==booking));
        Assert.False(await db.Set<BookingIntakeAssignment>().AnyAsync(x=>x.BookingId==booking));
        await transaction.RollbackAsync();
    }

    private sealed class BeforeSave(Func<Task> action):SaveChangesInterceptor
    {
        private bool ran;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct=default)
        { if(!ran){ran=true;await action();}return result; }
    }
    [IntakeSqlFact]
    public async Task Sql_ConcurrentHoldWinsAgainstPurgeAndRollsBackAnswerDeletion()
    {
        Seed seed;
        await using(var setup=Db()) seed=await SeedAsync(setup);
        try
        {
            await using var holdDb=Db();
            var interceptor=new BeforeSave(async()=>{
                var hold=await new IntakeRetentionService(holdDb,new Facts(seed.Tenant,seed.Booking),new Clock()).PlaceHoldAsync(seed.Tenant,seed.Submission,"admin","regulatory");
                Assert.True(hold.IsSuccess);
            });
            await using var purgeDb=Db(interceptor);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>new IntakeRetentionService(purgeDb,new Facts(seed.Tenant,seed.Booking),new Clock()).PurgeTenantAsync(seed.Tenant));
            await using var check=Db();
            Assert.Single(await check.Set<IntakeFormAnswer>().Where(x=>x.TenantId==seed.Tenant).ToArrayAsync());
            Assert.Null((await check.Set<IntakeFormSubmission>().SingleAsync(x=>x.Id==seed.Submission)).PurgedAt);
            Assert.Single(await check.Set<IntakeSubmissionHold>().Where(x=>x.TenantId==seed.Tenant&&x.ReleasedAt==null).ToArrayAsync());
        }
        finally
        {
            await using var cleanup=Db();
            await cleanup.Set<IntakeSubmissionHold>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Set<IntakeAnswerChoice>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Set<IntakeFormAnswer>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Set<IntakeFormSubmission>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Set<BookingIntakeAssignment>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Set<ServiceIntakeForm>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Set<IntakeFieldChoice>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Set<IntakeFormField>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Set<IntakeFormTemplate>().Where(x=>x.TenantId==seed.Tenant).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.CurrentPublishedVersionId,(Guid?)null));
            await cleanup.Set<IntakeFormVersion>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Set<IntakeFormTemplate>().Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Bookings.Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.ServiceOfferings.Where(x=>x.TenantId==seed.Tenant).ExecuteDeleteAsync();
            await cleanup.Tenants.Where(x=>x.Id==seed.Tenant).ExecuteDeleteAsync();
        }
    }

    [IntakeSqlFact]
    public async Task Sql_StaleTemplateRowversionCannotPublishOverConcurrentDraftChange()
    {
        await using var db=Db();await using var transaction=await db.Database.BeginTransactionAsync();var s=await SeedAsync(db);
        var template=await db.Set<IntakeFormTemplate>().SingleAsync(x=>x.Id==s.Template);var stale=Convert.ToBase64String(template.RowVersion);
        template.Name="Updated management name";await db.SaveChangesAsync();
        var result=await new IntakeTemplateService(db,new Access()).CreateDraftAsync(s.Tenant,"owner",s.Template,stale);
        Assert.True(result.IsFailure);Assert.Equal("intake.conflict",result.Error.Code);
        await transaction.RollbackAsync();
    }
}
file sealed class IntakeSqlFactAttribute:FactAttribute
{
    public IntakeSqlFactAttribute(){if(Environment.GetEnvironmentVariable("ADENI_SQLSERVER_TESTS")!="1")Skip="Requires published SQL Server database and ADENI_SQLSERVER_TESTS=1.";}
}
