namespace Adeni.Infrastructure.Persistence;

using Adeni.Domain.Calendar;
using Adeni.Domain.Booking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed partial class AdeniDbContext
{
    private void ConfigureCalendar(ModelBuilder model)
    {
        model.Entity<Adeni.Domain.Tenancy.BusinessLocation>().HasAlternateKey(x => new { x.TenantId, x.Id });
        var link = model.Entity<Adeni.Domain.Tenancy.PublicBookingLink>();
        link.ToTable("public_booking_links", "tenancy");
        link.HasKey(x => x.Id);
        link.Property(x => x.Slug).HasMaxLength(64);
        link.HasIndex(x => x.Slug).IsUnique();
        link.HasIndex(x => new { x.TenantId, x.LocationId }).IsUnique();
        link.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        link.HasOne<Adeni.Domain.Tenancy.BusinessLocation>().WithMany().HasForeignKey(x => new { x.TenantId, x.LocationId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StaffMember>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<BookingRecord>().HasAlternateKey(x => new { x.TenantId, x.Id });
        CalendarEntity(model.Entity<CalendarConnection>(), "connections");
        var c = model.Entity<CalendarConnection>();
        c.Property(x => x.Provider).HasMaxLength(16);
        c.Property(x => x.AccountHash).HasMaxLength(64);
        c.Property(x => x.ProtectedCredentials).HasMaxLength(16000);
        c.Property(x => x.Status).HasMaxLength(32);
        c.Property(x => x.FailureCategory).HasMaxLength(32);
        c.Property(x => x.FailureCount).HasDefaultValue(0);
        c.Property(x => x.RowVersion).IsRowVersion();
        c.HasIndex(x => new { x.TenantId, x.StaffMemberId, x.Provider }).IsUnique();
        c.HasIndex(x => x.NextSyncAt);
        c.HasOne<StaffMember>().WithMany().HasForeignKey(x => new { x.TenantId, x.StaffMemberId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        CalendarEntity(model.Entity<CalendarSelection>(), "selections");
        var s = model.Entity<CalendarSelection>();
        s.Property(x => x.ExternalId).HasMaxLength(2048).UseCollation("Latin1_General_100_BIN2");
        s.Property(x => x.ExternalIdHash).HasMaxLength(64);
        s.Property(x => x.TimeZoneId).HasMaxLength(128);
        s.HasIndex(x => new { x.TenantId, x.ConnectionId, x.ExternalIdHash }).IsUnique();
        s.HasIndex(x => new { x.TenantId, x.ConnectionId }).IsUnique().HasFilter("[IsWriteTarget] = 1");
        s.HasOne<CalendarConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.ConnectionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        CalendarEntity(model.Entity<CalendarStaffState>(), "staff_state");
        var state = model.Entity<CalendarStaffState>();
        state.Property(x => x.RowVersion).IsRowVersion();
        state.HasOne<StaffMember>().WithMany().HasForeignKey(x => new { x.TenantId, x.Id }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        CalendarEntity(model.Entity<CalendarSnapshot>(), "snapshots");
        var snapshot = model.Entity<CalendarSnapshot>();
        snapshot.ToTable("snapshots", "calendar", t => t.HasCheckConstraint("CK_calendar_snapshots_interval", "[EndAt] > [StartAt]"));
        snapshot.HasIndex(x => new { x.TenantId, x.ConnectionId, x.StartAt, x.EndAt }).IsUnique();
        snapshot.HasOne<CalendarConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.ConnectionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        CalendarEntity(model.Entity<ExternalBusyBlock>(), "busy_blocks");
        var busy = model.Entity<ExternalBusyBlock>();
        busy.ToTable("busy_blocks", "calendar", t => t.HasCheckConstraint("CK_calendar_busy_interval", "[EndAt] > [StartAt]"));
        busy.HasIndex(x => new { x.TenantId, x.SnapshotId, x.StartAt });
        busy.HasOne<CalendarSnapshot>().WithMany().HasForeignKey(x => new { x.TenantId, x.SnapshotId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        CalendarEntity(model.Entity<CalendarOAuthAttempt>(), "oauth_attempts");
        var a = model.Entity<CalendarOAuthAttempt>();
        a.Property(x => x.Provider).HasMaxLength(16);
        a.Property(x => x.Actor).HasMaxLength(128);
        a.Property(x => x.StateHash).HasMaxLength(64);
        a.Property(x => x.CorrelationHash).HasMaxLength(64);
        a.Property(x => x.ProtectedVerifier).HasMaxLength(2048);
        a.Property(x => x.RowVersion).IsRowVersion();
        a.HasIndex(x => x.StateHash).IsUnique();
        a.HasOne<StaffMember>().WithMany().HasForeignKey(x => new { x.TenantId, x.StaffMemberId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        CalendarEntity(model.Entity<CalendarConflict>(), "conflicts");
        var conflict = model.Entity<CalendarConflict>();
        conflict.Property(x => x.Category).HasMaxLength(32);
        conflict.HasIndex(x => new { x.TenantId, x.StaffMemberId, x.BookingId }).IsUnique();
        conflict.HasOne<StaffMember>().WithMany().HasForeignKey(x => new { x.TenantId, x.StaffMemberId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        conflict.HasOne<BookingRecord>().WithMany().HasForeignKey(x => new { x.TenantId, x.BookingId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        CalendarEntity(model.Entity<ExternalBookingEvent>(), "booking_events");
        var mapping = model.Entity<ExternalBookingEvent>();
        mapping.Property(x => x.TargetId).HasMaxLength(2048).UseCollation("Latin1_General_100_BIN2");
        mapping.Property(x => x.ExternalId).HasMaxLength(2048).UseCollation("Latin1_General_100_BIN2");
        mapping.Property(x => x.AppliedFingerprint).HasMaxLength(64);
        mapping.Property(x => x.State).HasMaxLength(32);
        mapping.Property(x => x.RowVersion).IsRowVersion();
        mapping.HasIndex(x => new { x.TenantId, x.ConnectionId, x.BookingId }).IsUnique();
        mapping.HasOne<CalendarConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.ConnectionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        mapping.HasOne<BookingRecord>().WithMany().HasForeignKey(x => new { x.TenantId, x.BookingId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private void CalendarEntity<T>(EntityTypeBuilder<T> entity, string table) where T : class, Adeni.Domain.Tenancy.ITenantEntity
    {
        entity.ToTable(table, "calendar");
        entity.HasKey("Id");
        entity.HasAlternateKey("TenantId", "Id");
        entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
    }
}
