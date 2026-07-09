namespace Adeni.Infrastructure.Tests.Persistence;

using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class TenantEntityArchitectureTests
{
    [Fact]
    public void All_ITenantEntity_types_have_global_query_filters()
    {
        var tenantEntityTypes = typeof(ITenantEntity).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && typeof(ITenantEntity).IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(tenantEntityTypes);

        using var db = new AdeniDbContext(
            new DbContextOptionsBuilder<AdeniDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new NoopTenantContext());

        var model = db.Model;
        foreach (var entityType in tenantEntityTypes)
        {
            var entity = model.FindEntityType(entityType);
            Assert.NotNull(entity);
            Assert.NotEmpty(entity!.GetDeclaredQueryFilters());
        }
    }

    private sealed class NoopTenantContext : Application.Abstractions.ITenantContext
    {
        public Domain.Tenancy.TenantId? CurrentTenantId => null;
        public bool IsTenantFilterActive => false;
        public void EnableTenantFilter(Domain.Tenancy.TenantId tenantId) { }
        public void DisableTenantFilter() { }
        public void Set(Domain.Tenancy.TenantId tenantId) { }
        public void Clear() { }
    }
}
