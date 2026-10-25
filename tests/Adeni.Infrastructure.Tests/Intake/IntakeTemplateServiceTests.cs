namespace Adeni.Infrastructure.Tests.Intake;

using Adeni.Application.Intake;
using Adeni.Domain.Identity;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class IntakeTemplateServiceTests
{
    private static AdeniDbContext Db() => new(new DbContextOptionsBuilder<AdeniDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new TenantContext());

    [Theory]
    [InlineData("owner", true)]
    [InlineData("manager", true)]
    [InlineData("receptionist", false)]
    [InlineData("practitioner", false)]
    [InlineData("ops", false)]
    [InlineData("accountant", false)]
    [InlineData("unknown-role", false)]
    public async Task Management_RequiresExplicitTenantRole(string role, bool allowed)
    {
        await using var db = Db();
        var tenant = Guid.NewGuid();
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = "actor", Role = role });
        await db.SaveChangesAsync();
        var access = new IntakeManagementAccessQuery(db);
        Assert.Equal(allowed, await access.CanManageAsync(tenant, "actor", default));
        Assert.False(await access.CanManageAsync(Guid.NewGuid(), "actor", default));
    }

    [Fact]
    public async Task PublishedDefinition_RemainsUnchangedWhenNewDraftIsEdited()
    {
        await using var db = Db();
        var service = new IntakeTemplateService(db, new Allow());
        var tenant = Guid.NewGuid();
        var created = (await service.CreateAsync(tenant, "actor", "Consultation")).Value!;
        var first = new IntakeDefinition("Original title", null, "Original instructions",
            [new(Guid.NewGuid(), IntakeFieldType.Acknowledgement, "Original exact acknowledgement", null, true, [])]);
        Assert.True((await service.SaveDraftAsync(tenant, "actor", created.Id, created.Revision, first)).IsSuccess);
        var published = (await service.PublishAsync(tenant, "actor", created.Id, created.Revision)).Value!;
        Assert.Null(published.Draft);
        Assert.Equal("Original exact acknowledgement", published.Published!.Fields[0].Label);
        Assert.True((await service.SaveDraftAsync(tenant, "actor", created.Id, published.Revision, first)).IsFailure);
        var cloned = (await service.CreateDraftAsync(tenant, "actor", created.Id, published.Revision)).Value!;
        Assert.NotEqual(first.Fields[0].Id, cloned.Draft!.Fields[0].Id);
        var changed = cloned.Draft with { Title = "New title", Fields = [cloned.Draft.Fields[0] with { Label = "New wording" }] };
        var edited = (await service.SaveDraftAsync(tenant, "actor", created.Id, cloned.Revision, changed)).Value!;
        Assert.Equal("Original title", edited.Published!.Title);
        Assert.Equal("Original exact acknowledgement", edited.Published.Fields[0].Label);
        Assert.Equal("New wording", edited.Draft!.Fields[0].Label);
        Assert.Equal(2, await db.Set<IntakeFormVersion>().CountAsync());
    }

    [Fact]
    public async Task Template_RejectsEmptyPublicationStaleRevisionAndForeignTenant()
    {
        await using var db = Db();
        var service = new IntakeTemplateService(db, new Allow());
        var tenant = Guid.NewGuid();
        var created = (await service.CreateAsync(tenant, "actor", "Consultation")).Value!;
        Assert.True((await service.PublishAsync(tenant, "actor", created.Id, created.Revision)).IsFailure);
        Assert.True((await service.PublishAsync(tenant, "actor", created.Id, "stale")).IsFailure);
        Assert.True((await service.GetAsync(Guid.NewGuid(), "actor", created.Id)).IsFailure);
        Assert.Null((await service.GetAsync(tenant, "actor", created.Id)).Value!.Published);
    }

    private sealed class Allow : IIntakeManagementAccessQuery
    {
        public Task<bool> CanManageAsync(Guid tenantId, string auth0Sub, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
