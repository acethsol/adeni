namespace Adeni.Infrastructure.Persistence;

using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

public sealed class AdeniDbContext(
    DbContextOptions<AdeniDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers", "identity");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Auth0Sub).IsUnique();
            entity.Property(x => x.Auth0Sub).HasMaxLength(128);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(32);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants", "tenancy");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.HasQueryFilter(x => TenantMatches(x.Id));
        });

        modelBuilder.Entity<BusinessUser>(entity =>
        {
            entity.ToTable("business_users", "identity");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Auth0Sub).IsUnique();
            entity.Property(x => x.Auth0Sub).HasMaxLength(128);
            entity.Property(x => x.Role).HasMaxLength(32);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            entity.HasQueryFilter(x => TenantMatches(x.TenantId));
        });

        modelBuilder.Entity<AuditLogRecord>(entity =>
        {
            entity.ToTable("audit_logs", "admin");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorId).HasMaxLength(128);
            entity.Property(x => x.Action).HasMaxLength(128);
            entity.Property(x => x.EntityType).HasMaxLength(64);
            entity.Property(x => x.EntityId).HasMaxLength(64);
            entity.Property(x => x.CorrelationId).HasMaxLength(64);
        });
    }

    internal bool TenantMatches(Guid entityTenantId) =>
        !tenantContext.IsTenantFilterActive
        || (tenantContext.CurrentTenantId?.Value == entityTenantId);
}
