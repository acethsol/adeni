namespace Adeni.Infrastructure.Persistence;

using Adeni.Application.Abstractions;
using Adeni.Application.Events;
using Adeni.Domain.Auditing;
using Adeni.Domain.Booking;
using Adeni.Domain.Catalog;
using Adeni.Domain.Identity;
using Adeni.Domain.Messaging;
using Adeni.Domain.Notifications;
using Adeni.Domain.Payments;
using Adeni.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

public sealed class AdeniDbContext(
    DbContextOptions<AdeniDbContext> options,
    ITenantContext tenantContext,
    IDomainEventCollector? domainEventCollector = null,
    IDomainEventDispatcher? domainEventDispatcher = null) : DbContext(options)
{
    /// <summary>
    /// When set, global query filters restrict rows to this tenant. Null means no filter.
    /// Synced from <see cref="ITenantContext"/> before each request (and at startup for seeding).
    /// </summary>
    internal Guid? ActiveTenantFilterId { get; private set; }

    public void SyncTenantFilter() =>
        ActiveTenantFilterId = tenantContext.IsTenantFilterActive && tenantContext.CurrentTenantId.HasValue
            ? tenantContext.CurrentTenantId.Value.Value
            : null;

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();

    public DbSet<BusinessProfileCategory> BusinessProfileCategories => Set<BusinessProfileCategory>();

    public DbSet<BusinessLocation> BusinessLocations => Set<BusinessLocation>();

    public DbSet<VerificationDocument> VerificationDocuments => Set<VerificationDocument>();

    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();

    public DbSet<ServiceOffering> ServiceOfferings => Set<ServiceOffering>();

    public DbSet<ServiceMenuGroup> ServiceMenuGroups => Set<ServiceMenuGroup>();

    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();

    public DbSet<StaffServiceLink> StaffServiceLinks => Set<StaffServiceLink>();

    public DbSet<StaffWeeklyAvailability> StaffWeeklyAvailabilities => Set<StaffWeeklyAvailability>();

    public DbSet<StaffLeave> StaffLeaves => Set<StaffLeave>();

    public DbSet<WeeklyAvailability> WeeklyAvailabilities => Set<WeeklyAvailability>();

    public DbSet<BookingRecord> Bookings => Set<BookingRecord>();

    public DbSet<BookingLine> BookingLines => Set<BookingLine>();

    public DbSet<BookingGuest> BookingGuests => Set<BookingGuest>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<CatalogMarket> CatalogMarkets => Set<CatalogMarket>();

    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    public DbSet<QuoteRequestRecord> QuoteRequests => Set<QuoteRequestRecord>();

    public DbSet<TenantVerificationBadge> TenantVerificationBadges => Set<TenantVerificationBadge>();

    public DbSet<PaymentIntentRecord> PaymentIntents => Set<PaymentIntentRecord>();

    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<TenantNotificationPreferences> TenantNotificationPreferences =>
        Set<TenantNotificationPreferences>();

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
            entity.Property(x => x.ErasureRequestedAt);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants", "tenancy");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.SubscriptionTier).HasConversion<int>();
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.Id == ActiveTenantFilterId);
        });

        modelBuilder.Entity<BusinessProfile>(entity =>
        {
            entity.ToTable("business_profiles", "tenancy");
            entity.HasKey(x => x.TenantId);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.CategorySlug).HasMaxLength(64);
            entity.Property(x => x.Phone).HasMaxLength(32);
            entity.Property(x => x.CoverImageKey).HasMaxLength(512);
            entity.Property(x => x.GalleryImageKeysJson).HasMaxLength(4000);
            entity.Property(x => x.LogoImageKey).HasMaxLength(512);
            entity.Property(x => x.PublicPageTemplateId).HasMaxLength(32).HasDefaultValue("studio");
            entity.Property(x => x.PublicPageAccentColor).HasMaxLength(7);
            entity.Property(x => x.PublicPageShowAbout).HasDefaultValue(true);
            entity.Property(x => x.PublicPageShowServices).HasDefaultValue(true);
            entity.Property(x => x.PublicPageShowReviews).HasDefaultValue(true);
            entity.Property(x => x.PublicPageShowVisit).HasDefaultValue(true);
            entity.Property(x => x.PublicPageShowBook).HasDefaultValue(true);
            entity.Property(x => x.PublicPageShowPolicies).HasDefaultValue(false);
            entity.Property(x => x.PolicyBookingText).HasMaxLength(8000);
            entity.Property(x => x.PolicyPaymentText).HasMaxLength(8000);
            entity.Property(x => x.PolicyCancellationText).HasMaxLength(8000);
            entity.Property(x => x.PolicyTermsText).HasMaxLength(8000);
            entity.Property(x => x.BusinessType).HasConversion<int>();
            entity.HasOne(x => x.Tenant).WithOne().HasForeignKey<BusinessProfile>(x => x.TenantId);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<BusinessProfileCategory>(entity =>
        {
            entity.ToTable("business_profile_categories", "tenancy");
            entity.HasKey(x => new { x.TenantId, x.CategorySlug });
            entity.Property(x => x.CategorySlug).HasMaxLength(64);
            entity.HasOne(x => x.Profile)
                .WithMany(x => x.Categories)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<BusinessLocation>(entity =>
        {
            entity.ToTable("business_locations", "tenancy");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsPrimary });
            entity.HasIndex(x => x.MarketId);
            entity.Property(x => x.Slug).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.MarketId).HasMaxLength(32);
            entity.Property(x => x.AddressLine).HasMaxLength(500);
            entity.Property(x => x.Area).HasMaxLength(120);
            entity.Property(x => x.TimeZoneId).HasMaxLength(64);
            // SQL Server rejects multiple CASCADE paths (Tenant → Profile → Location and Tenant → Location).
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<BusinessProfile>()
                .WithMany(x => x.Locations)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<VerificationDocument>(entity =>
        {
            entity.ToTable("verification_documents", "tenancy");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReferenceNumber).HasMaxLength(128);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<BusinessUser>(entity =>
        {
            entity.ToTable("business_users", "identity");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Auth0Sub).IsUnique();
            entity.Property(x => x.Auth0Sub).HasMaxLength(128);
            entity.Property(x => x.Role).HasMaxLength(32);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
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

        modelBuilder.Entity<ServiceMenuGroup>(entity =>
        {
            entity.ToTable("service_menu_groups", "booking");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasIndex(x => new { x.TenantId, x.SortOrder });
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<ServiceOffering>(entity =>
        {
            entity.ToTable("service_offerings", "booking");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.PriceAmount).HasPrecision(12, 2);
            entity.Property(x => x.PricingType).HasConversion<int>();
            entity.Property(x => x.CategorySlug).HasMaxLength(64);
            entity.Property(x => x.CatalogServiceId).HasMaxLength(64);
            entity.Property(x => x.BookingDeliveryType).HasConversion<int>();
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.IsAddOn).HasDefaultValue(false);
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
            entity.HasIndex(x => new { x.TenantId, x.MenuGroupId, x.SortOrder });
            entity.HasOne(x => x.MenuGroup)
                .WithMany()
                .HasForeignKey(x => x.MenuGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<WeeklyAvailability>(entity =>
        {
            entity.ToTable("weekly_availability", "booking");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.DayOfWeek });
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<StaffMember>(entity =>
        {
            entity.ToTable("staff_members", "booking");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(80);
            entity.Property(x => x.LastName).HasMaxLength(80);
            entity.Property(x => x.DisplayName).HasMaxLength(120);
            entity.Property(x => x.RoleKey).HasMaxLength(40).HasDefaultValue(StaffRoleKeys.Other);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.Property(x => x.Bio).HasMaxLength(500);
            entity.Property(x => x.AvatarImageKey).HasMaxLength(512);
            entity.HasIndex(x => new { x.TenantId, x.IsActive, x.SortOrder });
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<StaffServiceLink>(entity =>
        {
            entity.ToTable("staff_service_links", "booking");
            entity.HasKey(x => new { x.StaffMemberId, x.ServiceOfferingId });
            entity.HasIndex(x => new { x.TenantId, x.ServiceOfferingId });
            entity.HasOne(x => x.StaffMember)
                .WithMany(x => x.ServiceLinks)
                .HasForeignKey(x => x.StaffMemberId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ServiceOffering)
                .WithMany()
                .HasForeignKey(x => x.ServiceOfferingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<StaffWeeklyAvailability>(entity =>
        {
            entity.ToTable("staff_weekly_availability", "booking");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.StaffMemberId, x.DayOfWeek });
            entity.HasOne(x => x.StaffMember)
                .WithMany(x => x.WeeklyHours)
                .HasForeignKey(x => x.StaffMemberId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<StaffLeave>(entity =>
        {
            entity.ToTable("staff_leave", "booking");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.StaffMemberId, x.StartAt });
            entity.HasOne(x => x.StaffMember)
                .WithMany(x => x.LeaveEntries)
                .HasForeignKey(x => x.StaffMemberId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<BookingRecord>(entity =>
        {
            entity.ToTable("bookings", "booking");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CustomerNotes).HasMaxLength(1000);
            entity.Property(x => x.BusinessNotes).HasMaxLength(1000);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.StartAt });
            entity.HasIndex(x => new { x.TenantId, x.Status, x.StartAt });
            entity.Property(x => x.GuestCount).HasDefaultValue(1);
            entity.HasIndex(x => new { x.TenantId, x.StaffMemberId, x.StartAt });
            entity.HasOne(x => x.ServiceOffering)
                .WithMany()
                .HasForeignKey(x => x.ServiceOfferingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.StaffMember)
                .WithMany()
                .HasForeignKey(x => x.StaffMemberId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Booking)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Guests)
                .WithOne(x => x.Booking)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<BookingLine>(entity =>
        {
            entity.ToTable("booking_lines", "booking");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ServiceName).HasMaxLength(200);
            entity.Property(x => x.PriceAmount).HasPrecision(12, 2);
            entity.HasIndex(x => new { x.BookingId, x.SortOrder });
            entity.HasOne(x => x.ServiceOffering)
                .WithMany()
                .HasForeignKey(x => x.ServiceOfferingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<BookingGuest>(entity =>
        {
            entity.ToTable("booking_guests", "booking");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(120);
            entity.HasIndex(x => new { x.BookingId, x.SortOrder });
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews", "booking");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.BookingId).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsHidden, x.CreatedAt });
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.Property(x => x.OwnerReply).HasMaxLength(1000);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<CatalogMarket>(entity =>
        {
            entity.ToTable("markets", "catalog");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(32);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.CountryCode).HasMaxLength(2);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.TimeZoneId).HasMaxLength(64);
            entity.Property(x => x.LanguagesJson).HasMaxLength(64);
            entity.Property(x => x.LaunchNote).HasMaxLength(500);
            entity.HasIndex(x => x.IsLive);
        });

        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.ToTable("waitlist_entries", "booking");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.ServiceOfferingId, x.NotifiedAt });
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<QuoteRequestRecord>(entity =>
        {
            entity.ToTable("quote_requests", "booking");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.ServiceAddress).HasMaxLength(500);
            entity.Property(x => x.PhotoKeysJson).HasMaxLength(2000);
            entity.Property(x => x.QuotedCurrency).HasMaxLength(3);
            entity.Property(x => x.QuoteNotes).HasMaxLength(2000);
            entity.Property(x => x.QuotedAmount).HasPrecision(12, 2);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => new { x.CustomerId, x.CreatedAt });
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<TenantVerificationBadge>(entity =>
        {
            entity.ToTable("tenant_verification_badges", "tenancy");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.BadgeType }).IsUnique();
            entity.Property(x => x.ReferenceNumber).HasMaxLength(128);
            entity.Property(x => x.GrantedByAdminId).HasMaxLength(128);
            entity.Property(x => x.BadgeType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<PaymentIntentRecord>(entity =>
        {
            entity.ToTable("payment_intents", "payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.PlatformFeeAmount).HasPrecision(12, 2);
            entity.Property(x => x.ProviderReference).HasMaxLength(128);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.CustomerEmail).HasMaxLength(320);
            entity.Property(x => x.CallbackUrl).HasMaxLength(2048);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
            entity.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
            entity.HasIndex(x => x.ProviderReference).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<MessageThread>(entity =>
        {
            entity.ToTable("message_threads", "messaging");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Subject).HasMaxLength(200);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasIndex(x => new { x.TenantId, x.LastMessageAt });
            entity.HasIndex(x => new { x.TenantId, x.CustomerId, x.BookingId });
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages", "messaging");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SenderAuth0Sub).HasMaxLength(128);
            entity.Property(x => x.Body).HasMaxLength(4000);
            entity.HasIndex(x => new { x.ThreadId, x.CreatedAt });
            entity.HasOne(x => x.Thread)
                .WithMany()
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });

        modelBuilder.Entity<TenantNotificationPreferences>(entity =>
        {
            entity.ToTable("tenant_notification_preferences", "notifications");
            entity.HasKey(x => x.TenantId);
            entity.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);

        if (domainEventCollector is not null && domainEventDispatcher is not null)
        {
            var events = domainEventCollector.TakeAll();
            foreach (var domainEvent in events)
            {
                await domainEventDispatcher.PublishAsync(domainEvent, cancellationToken);
            }
        }

        return result;
    }
}
