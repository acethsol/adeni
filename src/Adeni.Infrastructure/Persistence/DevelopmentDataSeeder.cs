namespace Adeni.Infrastructure.Persistence;

using System.Security.Cryptography;
using Adeni.Application.Catalog;
using Adeni.Application.Storage;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Messaging;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Catalog;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Markets;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Idempotent dev seed for the Beauty &amp; Wellness wedge (Lagos + Ottawa, enabled categories).
/// Skips slugs that already exist. Drop tenant data and restart the API to reseed from scratch.
/// </summary>
public static class DevelopmentDataSeeder
{
    public const string SeedMarkerSlug = "lekki-cuts";

    /// <summary>Second Lekki Cuts branch for portal multi-location dev (branch switch).</summary>
    public const string DevLekkiPhase2Slug = "lekki-cuts-phase2";

    public const string DevBusinessAuth0Sub = "auth0|local-business";

    /// <summary>Second business login for invite accept tests (no tenant until invite accepted).</summary>
    public const string DevStaffAuth0Sub = "auth0|local-staff";

    /// <summary>Raw invite token seeded for Lekki Cuts — accept via /accept-invite?token=…</summary>
    public const string DevStaffInviteToken = "dev-lekki-staff-invite-token";

    private const string DevQueueFloorSeedNote = "dev-unified-floor-v1";

    private const string DevStaffRosterSeedMarker = "dev-lekki-staff-roster-v1";

    /// <summary>Square crop for dev logo on kiosk, discover queue, and public page.</summary>
    private static readonly string DevLekkiLogoImageKey =
        "https://images.unsplash.com/photo-1622286342621-4bd786c2447c?w=256&h=256&auto=format&fit=crop&q=80";
    public const string DevCustomerAuth0Sub = "auth0|local-customer";
    public const string DevAdminAuth0Sub = "auth0|local-admin";

    private static readonly IReadOnlyDictionary<string, (string Currency, string TimeZoneId)> MarketDefaults =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["lagos"] = ("NGN", "Africa/Lagos"),
            ["ottawa"] = ("CAD", "America/Toronto"),
        };

    private static async Task SeedSamplesAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var existingSlugs = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);

        var slugSet = existingSlugs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var added = 0;
        const int saveBatchSize = 100;
        var catalog = WellnessCategoryCatalogJson.ReadFromFile(new SeedHostEnvironment());

        foreach (var sample in DevelopmentSeedCatalog.All)
        {
            if (slugSet.Contains(sample.Slug))
            {
                continue;
            }

            if (!MarketDefaults.TryGetValue(sample.MarketId, out var marketDefaults))
            {
                continue;
            }

            var tenantId = Guid.NewGuid();
            var tenant = new Tenant
            {
                Id = tenantId,
                Name = sample.Name,
                Status = TenantStatus.Verified,
                CreatedAt = now,
                VerifiedAt = now,
                SubscriptionSku = SubscriptionSku.Professional,
                BillingStatus = TenantBillingStatus.Active,
            };
            db.Tenants.Add(tenant);

            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = sample.CategorySlug,
                Phone = sample.Phone,
                Description = sample.Description,
                UpdatedAt = now,
                Categories =
                [
                    new BusinessProfileCategory
                    {
                        TenantId = tenantId,
                        CategorySlug = sample.CategorySlug,
                        IsPrimary = true,
                    },
                ],
            });

            var locationId = Guid.NewGuid();
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = locationId,
                TenantId = tenantId,
                Slug = sample.Slug,
                Name = sample.LocationName,
                MarketId = sample.MarketId,
                AddressLine = sample.AddressLine,
                Area = sample.Area,
                Latitude = sample.Latitude,
                Longitude = sample.Longitude,
                TimeZoneId = marketDefaults.TimeZoneId,
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });

            AddServiceMenu(
                db,
                tenantId,
                sample.CategorySlug,
                marketDefaults.Currency,
                sample,
                catalog.GetServiceTemplates(sample.CategorySlug),
                [],
                now,
                added);

            foreach (var day in new[]
                     {
                         DayOfWeek.Monday,
                         DayOfWeek.Tuesday,
                         DayOfWeek.Wednesday,
                         DayOfWeek.Thursday,
                         DayOfWeek.Friday,
                         DayOfWeek.Saturday,
                     })
            {
                db.WeeklyAvailabilities.Add(new WeeklyAvailability
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BusinessLocationId = locationId,
                    DayOfWeek = day,
                    OpenTime = new TimeOnly(9, 0),
                    CloseTime = new TimeOnly(17, 0),
                });
            }

            slugSet.Add(sample.Slug);
            added++;

            if (added % saveBatchSize == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (added % saveBatchSize != 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task SeedAsync(AdeniDbContext db, CancellationToken cancellationToken = default)
    {
        var environment = new SeedHostEnvironment();
        await MarketCatalogSeeder.SeedIfEmptyAsync(db, environment, cancellationToken);
        await SeedSamplesAsync(db, cancellationToken);
        await EnsureServiceMenusAsync(db, environment, cancellationToken);
        await EnsureGalleryPhotosAsync(db, cancellationToken);
        await EnsureDevBusinessLogoAsync(db, cancellationToken);
        await SeedDevBusinessOwnerAsync(db, cancellationToken);
        await EnsureDevPortalTenantBillingAsync(db, cancellationToken);
        await SeedDevPortalSubscriptionChargesAsync(db, cancellationToken);
        await EnsureDevLekkiMultiBranchAsync(db, cancellationToken);
        await EnsureDevLekkiStaffRosterAsync(db, cancellationToken);
        await SeedDevStaffInviteAsync(db, cancellationToken);
        await SeedDevDepositSettingsAsync(db, cancellationToken);
        await SeedDevReviewFixtureAsync(db, cancellationToken);
        await SeedDevQueueFloorAsync(db, cancellationToken);
        await SeedDevMessagingAsync(db, cancellationToken);
        await SeedDevTenantCustomersAsync(db, cancellationToken);
        await SeedDevAdminPrivacyCustomersAsync(db, cancellationToken);
    }

    /// <summary>
    /// CRM-style tenant customers for Lekki Cuts (portal Customers list). Idempotent via seed email domain.
    /// </summary>
    private static async Task SeedDevTenantCustomersAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        const string seedEmailSuffix = "@seed.lekki.local";

        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);
        if (location is null)
        {
            return;
        }

        var tenantId = location.TenantId;
        if (await db.TenantCustomers
                .IgnoreQueryFilters()
                .AnyAsync(
                    x => x.TenantId == tenantId && x.Email != null && x.Email.EndsWith(seedEmailSuffix),
                    cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var seeds = new TenantCustomerSeed[]
        {
            new("Amaka Okoro", "Amaka", "Okoro", "2348012399001", $"amaka{seedEmailSuffix}", "auth0|dev-messaging-walkin", 1),
            new("Tunde Bakare", "Tunde", "Bakare", "2348012399002", $"tunde{seedEmailSuffix}", "auth0|dev-messaging-booking", 3),
            new("Chioma Nwosu", "Chioma", "Nwosu", "2348015550101", $"chioma{seedEmailSuffix}", null, 5),
            new("Emeka Adeyemi", "Emeka", "Adeyemi", "2348015550102", $"emeka{seedEmailSuffix}", null, 8),
            new("Fatima Bello", "Fatima", "Bello", "2348015550103", $"fatima{seedEmailSuffix}", null, 12),
            new("Jordan Expected", "Jordan", "Expected", "2348012300003", $"jordan{seedEmailSuffix}", "dev-queue-expected", 2),
            new("Sam Checked-in", "Sam", "Checked-in", "2348012300004", $"sam{seedEmailSuffix}", "dev-queue-checked-in", 4),
            new("Local Customer", "Local", "Customer", "2348015550199", $"local{seedEmailSuffix}", DevCustomerAuth0Sub, 0),
        };

        foreach (var seed in seeds)
        {
            Guid? platformCustomerId = null;
            if (!string.IsNullOrWhiteSpace(seed.Auth0Sub))
            {
                platformCustomerId = await EnsureSeedCustomerAsync(
                    db,
                    seed.Auth0Sub,
                    seed.DisplayName,
                    seed.Phone,
                    cancellationToken);
            }

            var phone = NormalizeSeedPhone(seed.Phone);
            db.TenantCustomers.Add(new TenantCustomer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = platformCustomerId,
                DisplayName = seed.DisplayName,
                FirstName = seed.FirstName,
                LastName = seed.LastName,
                Email = seed.Email,
                NormalizedPhone = string.IsNullOrEmpty(phone) ? null : phone,
                CreatedAt = now.AddDays(-seed.LastSeenDaysAgo - 30),
                UpdatedAt = now.AddDays(-seed.LastSeenDaysAgo),
                LastSeenAt = now.AddDays(-seed.LastSeenDaysAgo),
                LastBookingAt = seed.LastSeenDaysAgo <= 7 ? now.AddDays(-seed.LastSeenDaysAgo) : null,
                LastQueueJoinAt = seed.LastSeenDaysAgo <= 4 ? now.AddDays(-seed.LastSeenDaysAgo) : null,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private const string DevAdminPrivacySeedAuth0Prefix = "auth0|dev-privacy-";

    private const string DevAdminPrivacySeedEmailDomain = "@seed.privacy.local";

    /// <summary>
    /// Platform customers for admin Customer privacy (<c>/customers</c>): search, DSAR export, erasure demo.
    /// Idempotent: skips when any Auth0 sub starts with <see cref="DevAdminPrivacySeedAuth0Prefix"/>.
    /// Restart the API in Development to seed; search e.g. <c>dev-privacy</c> or <c>seed.privacy.local</c>.
    /// </summary>
    private static async Task SeedDevAdminPrivacyCustomersAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        if (await db.Customers
                .IgnoreQueryFilters()
                .AnyAsync(
                    customer => customer.Auth0Sub.StartsWith(DevAdminPrivacySeedAuth0Prefix),
                    cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var seeds = new AdminPrivacyCustomerSeed[]
        {
            new("auth0|dev-privacy-amara-k", "Amara Kouassi", "amara.kouassi", "+16135550102", 45, false),
            new("auth0|dev-privacy-chidi-o", "Chidi Okafor", "chidi.okafor", "+2348015550201", 120, false),
            new("auth0|dev-privacy-sarah-m", "Sarah Mensah", "sarah.mensah", "+2348015550202", 30, false),
            new("auth0|dev-privacy-jordan-t", "Jordan Tremblay", "jordan.tremblay", "+16135550188", 14, false),
            new("auth0|dev-privacy-ada-export", "Ada Okonkwo", "ada.export", "+2348015550203", 60, false),
            new("auth0|dev-privacy-erased", "[erased]", "erased.demo", "+2348015550299", 200, true),
        };

        foreach (var seed in seeds)
        {
            var createdAt = now.AddDays(-seed.CreatedDaysAgo);
            if (seed.Erased)
            {
                db.Customers.Add(new Customer
                {
                    Id = Guid.NewGuid(),
                    Auth0Sub = seed.Auth0Sub,
                    Name = seed.Name,
                    Email = null,
                    Phone = null,
                    CreatedAt = createdAt,
                    ErasureRequestedAt = now.AddDays(-7),
                });
                continue;
            }

            db.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Auth0Sub = seed.Auth0Sub,
                Name = seed.Name,
                Email = $"{seed.EmailLocalPart}{DevAdminPrivacySeedEmailDomain}",
                Phone = seed.Phone,
                CreatedAt = createdAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var exportCustomer = await db.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                customer => customer.Auth0Sub == "auth0|dev-privacy-ada-export",
                cancellationToken);
        if (exportCustomer is null)
        {
            return;
        }

        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);
        if (location is null)
        {
            return;
        }

        if (await db.Bookings
                .IgnoreQueryFilters()
                .AnyAsync(
                    booking =>
                        booking.CustomerId == exportCustomer.Id
                        && booking.TenantId == location.TenantId,
                    cancellationToken))
        {
            return;
        }

        var service = await db.ServiceOfferings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entry => entry.TenantId == location.TenantId && entry.IsActive)
            .OrderBy(entry => entry.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (service is null)
        {
            return;
        }

        var startAt = now.AddDays(-10);
        var endAt = startAt.AddMinutes(service.DurationMinutes);
        db.Bookings.Add(new BookingRecord
        {
            Id = Guid.NewGuid(),
            TenantId = location.TenantId,
            ServiceOfferingId = service.Id,
            CustomerId = exportCustomer.Id,
            StartAt = startAt,
            EndAt = endAt,
            Status = BookingStatus.Completed,
            CustomerNotes = "Dev seed — admin privacy export demo",
            CreatedAt = startAt,
            UpdatedAt = endAt,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record AdminPrivacyCustomerSeed(
        string Auth0Sub,
        string Name,
        string EmailLocalPart,
        string Phone,
        int CreatedDaysAgo,
        bool Erased);

    private static string NormalizeSeedPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 7 ? digits : string.Empty;
    }

    private sealed record TenantCustomerSeed(
        string DisplayName,
        string FirstName,
        string LastName,
        string Phone,
        string Email,
        string? Auth0Sub,
        int LastSeenDaysAgo);

    /// <summary>
    /// Sample conversations for Lekki Cuts so the portal Messages inbox is non-empty in local dev.
    /// Idempotent: skips when the tenant already has any conversation.
    /// </summary>
    private static async Task SeedDevMessagingAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);
        if (location is null)
        {
            return;
        }

        if (await db.Conversations
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == location.TenantId, cancellationToken))
        {
            return;
        }

        var owner = await db.BusinessUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == location.TenantId && x.Auth0Sub == DevBusinessAuth0Sub,
                cancellationToken);
        if (owner is null)
        {
            return;
        }

        var localCustomer = await db.Customers
            .FirstOrDefaultAsync(entry => entry.Auth0Sub == DevCustomerAuth0Sub, cancellationToken);
        if (localCustomer is null)
        {
            localCustomer = new Customer
            {
                Id = Guid.NewGuid(),
                Auth0Sub = DevCustomerAuth0Sub,
                Name = "Local Customer",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Customers.Add(localCustomer);
            await db.SaveChangesAsync(cancellationToken);
        }

        var walkInCustomer = await EnsureSeedCustomerAsync(
            db,
            "auth0|dev-messaging-walkin",
            "Amaka Okoro",
            "2348012399001",
            cancellationToken);
        var bookingCustomer = await EnsureSeedCustomerAsync(
            db,
            "auth0|dev-messaging-booking",
            "Tunde Bakare",
            "2348012399002",
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // 1) Needs attention — customer asked about hours; FAQ bot replied; no business read yet.
        SeedConversationExchange(
            db,
            location.TenantId,
            walkInCustomer,
            owner,
            ConversationContextType.Profile,
            contextId: null,
            now.AddHours(-5),
            includeBusinessReply: false,
            includeFaqBot: true,
            businessHasRead: false,
            customerBody: "Hi — what time do you open on Saturday?",
            botBody: "Thanks for asking! Check our live availability and book online on Adeni — we confirm slots in real time.",
            businessBody: null);

        // 2) Active chat — local customer + owner already replied (caught up for team badge).
        SeedConversationExchange(
            db,
            location.TenantId,
            localCustomer.Id,
            owner,
            ConversationContextType.Profile,
            contextId: null,
            now.AddHours(-2),
            includeBusinessReply: true,
            includeFaqBot: false,
            businessHasRead: true,
            customerBody: "Can I book a fade for tomorrow afternoon?",
            botBody: null,
            businessBody: "Yes — we have openings after 2pm. Book on Adeni or tell me a time that works.");

        // 3) Needs attention again — newer customer question after last business read.
        var booking = await db.Bookings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == location.TenantId && x.CustomerId == bookingCustomer)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        SeedConversationExchange(
            db,
            location.TenantId,
            bookingCustomer,
            owner,
            booking is null ? ConversationContextType.Profile : ConversationContextType.Booking,
            booking?.Id,
            now.AddMinutes(-40),
            includeBusinessReply: false,
            includeFaqBot: false,
            businessHasRead: false,
            customerBody: "Running 15 minutes late — can you still take me?",
            botBody: null,
            businessBody: null);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void SeedConversationExchange(
        AdeniDbContext db,
        Guid tenantId,
        Guid customerId,
        BusinessUser owner,
        ConversationContextType contextType,
        Guid? contextId,
        DateTimeOffset startedAt,
        bool includeBusinessReply,
        bool includeFaqBot,
        bool businessHasRead,
        string customerBody,
        string? botBody,
        string? businessBody)
    {
        var conversationId = Guid.NewGuid();
        var customerParticipantId = Guid.NewGuid();
        var businessParticipantId = Guid.NewGuid();
        var botParticipantId = Guid.NewGuid();
        var customerMessageId = Guid.NewGuid();
        var botMessageId = Guid.NewGuid();
        var businessMessageId = Guid.NewGuid();

        var lastMessageAt = startedAt;
        var conversation = new Conversation
        {
            Id = conversationId,
            TenantId = tenantId,
            CustomerId = customerId,
            ContextType = contextType,
            ContextId = contextId,
            Status = ConversationStatus.Open,
            CreatedAt = startedAt,
            LastMessageAt = startedAt,
            CreatedByParticipantId = customerParticipantId,
        };

        var customerParticipant = new ConversationParticipant
        {
            Id = customerParticipantId,
            ConversationId = conversationId,
            TenantId = tenantId,
            ParticipantType = ConversationParticipantType.Customer,
            CustomerId = customerId,
            Auth0Sub = null,
            JoinedAt = startedAt,
            LastReadMessageId = customerMessageId,
            LastReadAt = startedAt,
        };

        db.Conversations.Add(conversation);
        db.ConversationParticipants.Add(customerParticipant);
        db.ConversationMessages.Add(new ConversationMessage
        {
            Id = customerMessageId,
            ConversationId = conversationId,
            TenantId = tenantId,
            SenderParticipantId = customerParticipantId,
            Type = ConversationMessageType.Text,
            Body = customerBody,
            ClientMessageId = $"seed-customer-{conversationId:N}",
            CreatedAt = startedAt,
            ModerationStatus = MessageModerationStatus.None,
        });

        if (includeFaqBot && !string.IsNullOrWhiteSpace(botBody))
        {
            var botAt = startedAt.AddMinutes(1);
            lastMessageAt = botAt;
            db.ConversationParticipants.Add(new ConversationParticipant
            {
                Id = botParticipantId,
                ConversationId = conversationId,
                TenantId = tenantId,
                ParticipantType = ConversationParticipantType.Bot,
                BotKey = MessagingBotKeys.Faq,
                JoinedAt = botAt,
            });
            db.ConversationMessages.Add(new ConversationMessage
            {
                Id = botMessageId,
                ConversationId = conversationId,
                TenantId = tenantId,
                SenderParticipantId = botParticipantId,
                Type = ConversationMessageType.Text,
                Body = botBody,
                ClientMessageId = $"seed-bot-{conversationId:N}",
                CreatedAt = botAt,
                ModerationStatus = MessageModerationStatus.None,
            });
        }

        if (includeBusinessReply && !string.IsNullOrWhiteSpace(businessBody))
        {
            var businessAt = startedAt.AddMinutes(12);
            lastMessageAt = businessAt;
            var businessParticipant = new ConversationParticipant
            {
                Id = businessParticipantId,
                ConversationId = conversationId,
                TenantId = tenantId,
                ParticipantType = ConversationParticipantType.BusinessUser,
                BusinessUserId = owner.Id,
                Auth0Sub = owner.Auth0Sub,
                JoinedAt = businessAt,
                LastReadMessageId = businessHasRead ? businessMessageId : null,
                LastReadAt = businessHasRead ? businessAt : null,
            };
            db.ConversationParticipants.Add(businessParticipant);
            db.ConversationMessages.Add(new ConversationMessage
            {
                Id = businessMessageId,
                ConversationId = conversationId,
                TenantId = tenantId,
                SenderParticipantId = businessParticipantId,
                Type = ConversationMessageType.Text,
                Body = businessBody,
                ClientMessageId = $"seed-business-{conversationId:N}",
                CreatedAt = businessAt,
                ModerationStatus = MessageModerationStatus.None,
            });
        }
        else if (businessHasRead)
        {
            // Owner opened but did not reply — still a participant with a read cursor.
            db.ConversationParticipants.Add(new ConversationParticipant
            {
                Id = businessParticipantId,
                ConversationId = conversationId,
                TenantId = tenantId,
                ParticipantType = ConversationParticipantType.BusinessUser,
                BusinessUserId = owner.Id,
                Auth0Sub = owner.Auth0Sub,
                JoinedAt = startedAt.AddMinutes(5),
                LastReadMessageId = includeFaqBot ? botMessageId : customerMessageId,
                LastReadAt = startedAt.AddMinutes(5),
            });
        }

        conversation.LastMessageAt = lastMessageAt;
        conversation.CreatedByParticipantId = customerParticipantId;
    }

    private static async Task EnsureServiceMenusAsync(
        AdeniDbContext db,
        SeedHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var catalog = WellnessCategoryCatalogJson.ReadFromFile(environment);
        var businesses = await db.BusinessProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(profile => new { profile.TenantId, profile.CategorySlug })
            .ToListAsync(cancellationToken);

        var markets = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(location => location.IsPrimary)
            .Select(location => new { location.TenantId, location.MarketId })
            .ToListAsync(cancellationToken);
        var marketByTenant = markets.ToDictionary(x => x.TenantId, x => x.MarketId);

        var existing = await db.ServiceOfferings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(service => service.CatalogServiceId != null)
            .Select(service => new { service.TenantId, service.CatalogServiceId })
            .ToListAsync(cancellationToken);
        var owned = existing
            .Select(service => (service.TenantId, service.CatalogServiceId!))
            .ToHashSet();

        var now = DateTimeOffset.UtcNow;
        var touched = 0;
        foreach (var business in businesses)
        {
            if (!marketByTenant.TryGetValue(business.TenantId, out var marketId)
                || !MarketDefaults.TryGetValue(marketId, out var marketDefaults))
            {
                continue;
            }

            var added = AddServiceMenu(
                db,
                business.TenantId,
                business.CategorySlug,
                marketDefaults.Currency,
                sample: null,
                catalog.GetServiceTemplates(business.CategorySlug),
                owned,
                now,
                business.TenantId.GetHashCode());
            if (added == 0)
            {
                continue;
            }

            touched++;
            if (touched % 50 == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (touched % 50 != 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureGalleryPhotosAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var profiles = await db.BusinessProfiles
            .IgnoreQueryFilters()
            .Where(profile =>
                profile.GalleryImageKeysJson == null
                || profile.GalleryImageKeysJson == ""
                || profile.GalleryImageKeysJson.Contains("images.unsplash.com"))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var touched = 0;
        foreach (var profile in profiles)
        {
            if (!GalleryPhotos.TryGetValue(profile.CategorySlug, out var photos) || photos.Length < 5)
            {
                continue;
            }

            var picked = PickPhotos(photos, profile.TenantId, 5);
            if (string.IsNullOrWhiteSpace(profile.CoverImageKey)
                || profile.CoverImageKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                profile.CoverImageKey = picked[0];
            }

            profile.GalleryImageKeysJson = GalleryImageKeys.Serialize(picked.Skip(1).Take(4).ToArray());
            profile.UpdatedAt = now;
            touched++;

            if (touched % 100 == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (touched % 100 != 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureDevBusinessLogoAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entry => entry.Slug == SeedMarkerSlug)
            .Select(entry => entry.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantId == Guid.Empty)
        {
            return;
        }

        var profile = await db.BusinessProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entry => entry.TenantId == tenantId, cancellationToken);

        if (profile is null || !string.IsNullOrWhiteSpace(profile.LogoImageKey))
        {
            return;
        }

        profile.LogoImageKey = DevLekkiLogoImageKey;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string[] PickPhotos(string[] pool, Guid tenantId, int count)
    {
        var order = pool.ToArray();
        var bytes = tenantId.ToByteArray();
        var state = BitConverter.ToUInt32(bytes, 0)
            ^ BitConverter.ToUInt32(bytes, 4)
            ^ BitConverter.ToUInt32(bytes, 8);
        for (var i = order.Length - 1; i > 0; i--)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            var swap = (int)(state % (uint)(i + 1));
            (order[i], order[swap]) = (order[swap], order[i]);
        }

        return order.Take(count).ToArray();
    }

    private static string Photo(string id) =>
        $"https://images.unsplash.com/{id}?w=1200&auto=format&fit=crop&q=80";

    private static readonly Dictionary<string, string[]> GalleryPhotos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hair-grooming"] =
        [
            Photo("photo-1621605815971-fbc98d665033"),
            Photo("photo-1560066984-138dadb4c035"),
            Photo("photo-1503951914875-452162b0f3f1"),
            Photo("photo-1595476108010-b4d1f102b1b1"),
            Photo("photo-1562322140-8baeececf3df"),
            Photo("photo-1521590832167-7bcbfaa6381f"),
            Photo("photo-1605497788044-5a32c7078486"),
            Photo("photo-1492106087820-71f1a00d2b11"),
            Photo("photo-1519699047748-de8e457a634e"),
            Photo("photo-1580618672591-eb180b1a973f"),
            Photo("photo-1470259078422-826894b933aa"),
            Photo("photo-1622286342621-4bd786c2447c"),
            Photo("photo-1599351431202-1e0f0137899a"),
            Photo("photo-1516975080664-ed2fc6a32937"),
            Photo("photo-1500840216050-6ffa99d75160"),
            Photo("photo-1622287162716-f311baa1a2b8"),
            Photo("photo-1595475884562-073c30d45670"),
            Photo("photo-1600948836101-f9ffda59d250"),
            Photo("photo-1620331311520-246422fd82f9"),
            Photo("photo-1633681926022-84c23e8cb2d6"),
        ],
        ["massage-bodywork"] =
        [
            Photo("photo-1544161515-4ab6ce6db874"),
            Photo("photo-1600334129128-685c5582fd35"),
            Photo("photo-1519824145371-296894a0daa9"),
            Photo("photo-1591343395082-e120087004b4"),
            Photo("photo-1519823551278-64ac92734fb1"),
            Photo("photo-1600334089648-b0d9d3028eb2"),
            Photo("photo-1545205597-3d9d02c29597"),
            Photo("photo-1552196563-55cd4e45efb3"),
            Photo("photo-1506126613408-eca07ce68773"),
            Photo("photo-1540555700478-4be289fbecef"),
            Photo("photo-1515377905703-c4788e51af15"),
            Photo("photo-1571902943202-507ec2618e8f"),
            Photo("photo-1599447421416-3414500d18a5"),
            Photo("photo-1545389336-cf090694435e"),
            Photo("photo-1506126279646-a697353d3166"),
            Photo("photo-1575052814086-f385e2e2ad1b"),
        ],
        ["spa-relaxation"] =
        [
            Photo("photo-1540555700478-4be289fbecef"),
            Photo("photo-1515377905703-c4788e51af15"),
            Photo("photo-1571902943202-507ec2618e8f"),
            Photo("photo-1600334089648-b0d9d3028eb2"),
            Photo("photo-1552693673-1bf958298935"),
            Photo("photo-1544161515-4ab6ce6db874"),
            Photo("photo-1600334129128-685c5582fd35"),
            Photo("photo-1519824145371-296894a0daa9"),
            Photo("photo-1591343395082-e120087004b4"),
            Photo("photo-1519823551278-64ac92734fb1"),
            Photo("photo-1570172619644-dfd03ed5d881"),
            Photo("photo-1616394584738-fc6e612e71b9"),
            Photo("photo-1556228720-195a672e8a03"),
            Photo("photo-1598440947619-2c35fc9aa908"),
            Photo("photo-1612817288484-6f916006741a"),
            Photo("photo-1545205597-3d9d02c29597"),
        ],
        ["skincare-aesthetics"] =
        [
            Photo("photo-1570172619644-dfd03ed5d881"),
            Photo("photo-1616394584738-fc6e612e71b9"),
            Photo("photo-1512290923902-8a9f81dc236c"),
            Photo("photo-1487412947147-5cebf100ffc2"),
            Photo("photo-1522335789203-aabd1fc54bc9"),
            Photo("photo-1556228720-195a672e8a03"),
            Photo("photo-1598440947619-2c35fc9aa908"),
            Photo("photo-1612817288484-6f916006741a"),
            Photo("photo-1556228578-0d85b1a4d571"),
            Photo("photo-1596755389378-c31d21fd1273"),
            Photo("photo-1611930022073-b7a4ba5fcccd"),
            Photo("photo-1620916566398-39f1143ab7be"),
            Photo("photo-1571781926291-c477ebfd024b"),
            Photo("photo-1556228578-8c89e6adf883"),
            Photo("photo-1512496015851-a90fb38ba796"),
            Photo("photo-1596462502278-27bfdc403348"),
            Photo("photo-1526045478516-99145907023c"),
            Photo("photo-1515688594390-b649af70d282"),
            Photo("photo-1487412720507-e7ab37603c6f"),
            Photo("photo-1571290274554-6a2eaa771e5f"),
        ],
        ["nails"] =
        [
            Photo("photo-1604654894610-df63bc536371"),
            Photo("photo-1519014816548-bf5fe059798b"),
            Photo("photo-1632345031435-8727f6897d53"),
            Photo("photo-1610992015732-2449b76344bc"),
            Photo("photo-1457972729786-0411a3b2b626"),
            Photo("photo-1522337094846-8a818192de1f"),
            Photo("photo-1519415943484-9fa1873496d4"),
            Photo("photo-1522337660859-02fbefca4702"),
            Photo("photo-1583001931096-959e9a1a6223"),
            Photo("photo-1571290274554-6a2eaa771e5f"),
            Photo("photo-1596462502278-27bfdc403348"),
            Photo("photo-1526045478516-99145907023c"),
            Photo("photo-1512496015851-a90fb38ba796"),
            Photo("photo-1582095133179-bfd08e2fc6b3"),
            Photo("photo-1515688594390-b649af70d282"),
            Photo("photo-1487412720507-e7ab37603c6f"),
        ],
        ["fitness"] =
        [
            Photo("photo-1517836357463-d25dfeac3438"),
            Photo("photo-1534438327276-14e5300c3a48"),
            Photo("photo-1571019614242-c5c5dee9f50b"),
            Photo("photo-1518611012118-696072aa579a"),
            Photo("photo-1574680096145-d05b474e2155"),
            Photo("photo-1517963879433-6ad2b056d712"),
            Photo("photo-1534258936925-c58bed479fcb"),
            Photo("photo-1434682881908-b43d0467b798"),
            Photo("photo-1517838277536-f5f99be501cd"),
            Photo("photo-1581009146145-b5ef050c2e1e"),
            Photo("photo-1540497077202-7c8a3999166f"),
            Photo("photo-1576678927484-cc907957088c"),
            Photo("photo-1550345332-09e3ac987658"),
            Photo("photo-1571019613454-1cb2f99b2d8b"),
            Photo("photo-1518310383802-640c2de311b2"),
            Photo("photo-1476480862126-209bfaa8edc8"),
            Photo("photo-1599058917212-d750089bc07e"),
            Photo("photo-1434596922112-19c563067271"),
        ],
        ["yoga"] =
        [
            Photo("photo-1544367567-0f2fcb009e0b"),
            Photo("photo-1506126613408-eca07ce68773"),
            Photo("photo-1545389336-cf090694435e"),
            Photo("photo-1599901860904-17e6ed7083a0"),
            Photo("photo-1575052814086-f385e2e2ad1b"),
            Photo("photo-1545205597-3d9d02c29597"),
            Photo("photo-1506126279646-a697353d3166"),
            Photo("photo-1552196563-55cd4e45efb3"),
        ],
        ["pilates"] =
        [
            Photo("photo-1518611012118-696072aa579a"),
            Photo("photo-1599447421416-3414500d18a5"),
            Photo("photo-1571019613454-1cb2f99b2d8b"),
            Photo("photo-1518310383802-640c2de311b2"),
            Photo("photo-1476480862126-209bfaa8edc8"),
            Photo("photo-1550345332-09e3ac987658"),
            Photo("photo-1434596922112-19c563067271"),
            Photo("photo-1540497077202-7c8a3999166f"),
        ],
        ["yoga-pilates"] =
        [
            Photo("photo-1544367567-0f2fcb009e0b"),
            Photo("photo-1518611012118-696072aa579a"),
        ],
    };

    private static int AddServiceMenu(
        AdeniDbContext db,
        Guid tenantId,
        string categorySlug,
        string currency,
        DevelopmentSeedCatalog.SampleBusiness? sample,
        IReadOnlyList<ServiceTemplateResponse> templates,
        HashSet<(Guid TenantId, string CatalogServiceId)> owned,
        DateTimeOffset now,
        int salt)
    {
        var added = 0;
        if (templates.Count == 0)
        {
            if (sample is null)
            {
                return 0;
            }

            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = sample.ServiceName,
                Description = sample.ServiceDescription,
                PriceAmount = sample.PriceAmount,
                Currency = currency,
                DurationMinutes = sample.DurationMinutes,
                CategorySlug = categorySlug,
                CatalogServiceId = sample.CatalogServiceId,
                BookingDeliveryType = sample.BookingDeliveryType,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            return 1;
        }

        for (var index = 0; index < templates.Count; index++)
        {
            var template = templates[index];
            if (!owned.Add((tenantId, template.Id)))
            {
                continue;
            }

            var isAnchor = sample?.CatalogServiceId == template.Id;
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = isAnchor ? sample!.ServiceName : template.Name,
                Description = isAnchor
                    ? sample!.ServiceDescription
                    : $"{template.Name}. Book a time online.",
                PriceAmount = isAnchor ? sample!.PriceAmount : MenuPrice(currency, index, salt),
                Currency = currency,
                DurationMinutes = isAnchor ? sample!.DurationMinutes : template.DefaultDurationMinutes,
                CategorySlug = categorySlug,
                CatalogServiceId = template.Id,
                BookingDeliveryType = isAnchor
                    ? sample!.BookingDeliveryType
                    : ParseDelivery(template.BookingDeliveryType),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            added++;
        }

        return added;
    }

    private static decimal MenuPrice(string currency, int index, int salt)
    {
        var (min, span) = currency switch
        {
            "NGN" => (6000m, 4500m),
            "CAD" => (30m, 16m),
            _ => (30m, 16m),
        };

        var step = (index + (salt & 7)) % 8;
        return decimal.Round(min + span * step, 0);
    }

    private static BookingDeliveryType ParseDelivery(string value) =>
        value.Replace("_", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant() switch
        {
            "class" => BookingDeliveryType.Class,
            "session" => BookingDeliveryType.Session,
            "experience" => BookingDeliveryType.Experience,
            "mobileappointment" => BookingDeliveryType.MobileAppointment,
            _ => BookingDeliveryType.Appointment,
        };

    private sealed class SeedHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Adeni";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static async Task SeedDevBusinessOwnerAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        if (await db.BusinessUsers
                .IgnoreQueryFilters()
                .AnyAsync(user => user.Auth0Sub == DevBusinessAuth0Sub, cancellationToken))
        {
            return;
        }

        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entry => entry.Slug == SeedMarkerSlug,
                cancellationToken);

        if (location is null)
        {
            return;
        }

        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = location.TenantId,
            Auth0Sub = DevBusinessAuth0Sub,
            Role = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Local portal owner (auth0|local-business) should see Professional trial entitlements, not legacy Free.
    /// </summary>
    private static async Task EnsureDevPortalTenantBillingAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var owner = await db.BusinessUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Auth0Sub == DevBusinessAuth0Sub, cancellationToken);

        if (owner is null)
        {
            return;
        }

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entry => entry.Id == owner.TenantId, cancellationToken);

        if (tenant is null)
        {
            return;
        }

        if (TenantBillingPolicy.ResolveEffectiveSku(tenant) == SubscriptionSku.Professional)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        tenant.SubscriptionSku = SubscriptionSku.Professional;
        TenantBillingPolicy.GrantTrial(
            tenant,
            TenantBillingDefaults.DefaultTrialDays,
            SubscriptionSku.Professional,
            now);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Sample Adeni SaaS charges for portal <c>/plan</c> (Lekki Cuts / auth0|local-business).
    /// Idempotent via <c>devseed-portal-billing-*</c> keys. Restart API in Development to apply.
    /// Admin Finance seed: <c>db/scripts/admin-transactions-dev-seed.sql</c>.
    /// </summary>
    private static async Task SeedDevPortalSubscriptionChargesAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        const string idempotencyPrefix = "devseed-portal-billing-";

        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);

        if (location is null)
        {
            return;
        }

        var tenantId = location.TenantId;
        if (await db.TenantSubscriptionCharges
                .IgnoreQueryFilters()
                .AnyAsync(
                    x => x.TenantId == tenantId && x.IdempotencyKey.StartsWith(idempotencyPrefix),
                    cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        (int DayOffset, string EventType, string Status, long AmountMinor)[] seeds =
        [
            (88, "subscription.create", "success", 55_000),
            (59, "subscription.charge.success", "success", 55_000),
            (31, "subscription.charge.success", "success", 55_000),
            (14, "invoice.payment_failed", "failed", 55_000),
            (2, "subscription.charge.success", "success", 55_000),
        ];

        var index = 1;
        foreach (var seed in seeds)
        {
            var occurred = now.AddDays(-seed.DayOffset);
            db.TenantSubscriptionCharges.Add(new TenantSubscriptionCharge
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IdempotencyKey = $"{idempotencyPrefix}{index}",
                ProviderReference = $"devseed_portal_billing_{index:D3}",
                EventType = seed.EventType,
                SubscriptionSku = SubscriptionSku.Professional,
                AmountMinor = seed.AmountMinor,
                Currency = "NGN",
                Status = seed.Status,
                OccurredAt = occurred,
                CreatedAt = occurred,
            });
            index++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Lekki Cuts second branch (Phase 2) for portal location switch. Idempotent by slug.
    /// </summary>
    private static async Task EnsureDevLekkiMultiBranchAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var primary = await db.BusinessLocations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);

        if (primary is null)
        {
            return;
        }

        var activeBranchCount = await db.BusinessLocations
            .IgnoreQueryFilters()
            .CountAsync(
                entry => entry.TenantId == primary.TenantId && entry.IsActive,
                cancellationToken);

        if (activeBranchCount >= 2
            || await db.BusinessLocations
                .IgnoreQueryFilters()
                .AnyAsync(entry => entry.Slug == DevLekkiPhase2Slug, cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var phase2Id = Guid.NewGuid();
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = phase2Id,
            TenantId = primary.TenantId,
            Slug = DevLekkiPhase2Slug,
            Name = "Lekki Phase 2",
            MarketId = primary.MarketId,
            AddressLine = "18 Admiralty Way, Lekki Phase 2",
            Area = "Lekki Phase 2",
            Latitude = 6.4490,
            Longitude = 3.4725,
            TimeZoneId = primary.TimeZoneId,
            IsPrimary = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });

        var primaryHours = await db.WeeklyAvailabilities
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == primary.TenantId && x.BusinessLocationId == primary.Id)
            .ToListAsync(cancellationToken);

        foreach (var row in primaryHours)
        {
            db.WeeklyAvailabilities.Add(new WeeklyAvailability
            {
                Id = Guid.NewGuid(),
                TenantId = primary.TenantId,
                BusinessLocationId = phase2Id,
                DayOfWeek = row.DayOfWeek,
                OpenTime = row.OpenTime,
                CloseTime = row.CloseTime,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Demo practitioners for Lekki Cuts with branch assignment (portal staff list + queue).
    /// Backfills legacy rows with null <see cref="StaffMember.BusinessLocationId"/> across active branches.
    /// </summary>
    private static async Task EnsureDevLekkiStaffRosterAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var primaryLocation = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);

        if (primaryLocation is null)
        {
            return;
        }

        var tenantId = primaryLocation.TenantId;
        var branches = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && entry.IsActive)
            .OrderByDescending(entry => entry.Slug == SeedMarkerSlug)
            .ThenByDescending(entry => entry.IsPrimary)
            .ThenBy(entry => entry.CreatedAt)
            .ToListAsync(cancellationToken);

        if (branches.Count == 0)
        {
            return;
        }

        var phase1Id = branches.FirstOrDefault(entry => entry.Slug == SeedMarkerSlug)?.Id
            ?? branches[0].Id;
        var phase2Id = branches.FirstOrDefault(entry => entry.Slug == DevLekkiPhase2Slug)?.Id
            ?? branches.FirstOrDefault(entry => entry.Id != phase1Id)?.Id;

        var unassigned = await db.StaffMembers
            .IgnoreQueryFilters()
            .Where(member => member.TenantId == tenantId && member.BusinessLocationId == null)
            .OrderBy(member => member.SortOrder)
            .ThenBy(member => member.DisplayName)
            .ToListAsync(cancellationToken);

        if (unassigned.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            for (var index = 0; index < unassigned.Count; index++)
            {
                unassigned[index].BusinessLocationId = branches[index % branches.Count].Id;
                unassigned[index].UpdatedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var service = await db.ServiceOfferings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && entry.IsActive)
            .OrderBy(entry => entry.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return;
        }

        var roster = new (string First, string Last, string Display, string Role, Guid LocationId, int Sort)[]
        {
            ("Fela", "Okonkwo", "Baba Fela", StaffRoleKeys.Barber, phase1Id, 0),
            ("Chi", "Adeyemi", "Chi Ade", StaffRoleKeys.Stylist, phase1Id, 1),
            ("Tunde", "Balogun", "Tunde B.", StaffRoleKeys.Barber, phase1Id, 2),
        };

        if (phase2Id is Guid secondBranch)
        {
            roster =
            [
                ..roster,
                ("Amaka", "Ojo", "Amaka O.", StaffRoleKeys.Stylist, secondBranch, 0),
            ];
        }

        var existingNames = await db.StaffMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId)
            .Select(member => member.DisplayName)
            .ToListAsync(cancellationToken);

        var nameSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var createdAny = false;
        var nowCreate = DateTimeOffset.UtcNow;

        foreach (var seed in roster)
        {
            if (nameSet.Contains(seed.Display))
            {
                continue;
            }

            var memberId = Guid.NewGuid();
            db.StaffMembers.Add(new StaffMember
            {
                Id = memberId,
                TenantId = tenantId,
                FirstName = seed.First,
                LastName = seed.Last,
                DisplayName = seed.Display,
                RoleKey = seed.Role,
                Title = seed.Role == StaffRoleKeys.Barber ? "Barber" : "Stylist",
                Bio = DevStaffRosterSeedMarker,
                IsActive = true,
                SortOrder = seed.Sort,
                BusinessLocationId = seed.LocationId,
                CreatedAt = nowCreate,
                UpdatedAt = nowCreate,
            });

            db.StaffServiceLinks.Add(new StaffServiceLink
            {
                StaffMemberId = memberId,
                ServiceOfferingId = service.Id,
                TenantId = tenantId,
            });

            nameSet.Add(seed.Display);
            createdAny = true;
        }

        if (createdAny)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedDevStaffInviteAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);

        if (location is null)
        {
            return;
        }

        var tokenHash = StaffAccessService.HashToken(DevStaffInviteToken);
        if (await db.StaffPortalInvites
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TokenHash == tokenHash, cancellationToken))
        {
            return;
        }

        if (await db.BusinessUsers
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Auth0Sub == DevStaffAuth0Sub, cancellationToken))
        {
            return;
        }

        var staff = await db.StaffMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s =>
                s.TenantId == location.TenantId
                && s.IsActive
                && s.DisplayName == "Baba Fela")
            .FirstOrDefaultAsync(cancellationToken)
            ?? await db.StaffMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.TenantId == location.TenantId && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .FirstOrDefaultAsync(cancellationToken);

        db.StaffPortalInvites.Add(new StaffPortalInvite
        {
            Id = Guid.NewGuid(),
            TenantId = location.TenantId,
            Email = "fela@lekki.cuts",
            PermissionRole = BusinessPermissionRoles.Practitioner,
            StaffMemberId = staff?.Id,
            TokenHash = tokenHash,
            Status = StaffPortalInviteStatuses.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
            InvitedByAuth0Sub = DevBusinessAuth0Sub,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDevReviewFixtureAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);

        if (location is null)
        {
            return;
        }

        var customer = await db.Customers
            .FirstOrDefaultAsync(entry => entry.Auth0Sub == DevCustomerAuth0Sub, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Auth0Sub = DevCustomerAuth0Sub,
                Name = "Local Customer",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync(cancellationToken);
        }

        var completedBookingIds = await db.Bookings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(booking =>
                booking.CustomerId == customer.Id
                && booking.TenantId == location.TenantId
                && booking.Status == BookingStatus.Confirmed
                && booking.EndAt <= DateTimeOffset.UtcNow)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

        if (completedBookingIds.Count > 0)
        {
            var reviewedBookingIds = await db.Reviews
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(review => completedBookingIds.Contains(review.BookingId))
                .Select(review => review.BookingId)
                .ToListAsync(cancellationToken);

            if (completedBookingIds.Except(reviewedBookingIds).Any())
            {
                return;
            }
        }

        var service = await db.ServiceOfferings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entry => entry.TenantId == location.TenantId && entry.IsActive)
            .OrderBy(entry => entry.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return;
        }

        var startAt = DateTimeOffset.UtcNow.AddDays(-3);
        var endAt = startAt.AddMinutes(service.DurationMinutes);

        db.Bookings.Add(new BookingRecord
        {
            Id = Guid.NewGuid(),
            TenantId = location.TenantId,
            ServiceOfferingId = service.Id,
            CustomerId = customer.Id,
            StartAt = startAt,
            EndAt = endAt,
            Status = BookingStatus.Confirmed,
            CustomerNotes = "Dev seed — ready for review E2E",
            CreatedAt = startAt,
            UpdatedAt = startAt,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDevQueueFloorAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);

        if (location is null)
        {
            return;
        }

        var tenantId = location.TenantId;
        if (await db.QueueTickets
                .IgnoreQueryFilters()
                .AnyAsync(
                    ticket => ticket.TenantId == tenantId && ticket.Notes == DevQueueFloorSeedNote,
                    cancellationToken))
        {
            return;
        }

        var profile = await db.BusinessProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entry => entry.TenantId == tenantId, cancellationToken);

        if (profile is null)
        {
            return;
        }

        var service = await db.ServiceOfferings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && entry.IsActive)
            .OrderBy(entry => entry.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return;
        }

        var staff = await db.StaffMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.IsActive)
            .OrderBy(member => member.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        profile.WalkInsEnabled = true;
        profile.WalkInAcceptingUntil = now.AddHours(8);
        profile.MaxWalkInWaiting = 15;
        profile.WalkInCalendarPolicy = WalkInCalendarPolicy.Relaxed;
        profile.UpdatedAt = now;

        var walkInCustomerA = await EnsureSeedCustomerAsync(
            db,
            "dev-queue-walkin-a",
            "Dev Walk-in Ada",
            "+2348012300001",
            cancellationToken);
        var walkInCustomerB = await EnsureSeedCustomerAsync(
            db,
            "dev-queue-walkin-b",
            "Dev Walk-in Chi",
            "+2348012300002",
            cancellationToken);
        var expectedCustomer = await EnsureSeedCustomerAsync(
            db,
            "dev-queue-expected",
            "Jordan Expected",
            "+2348012300003",
            cancellationToken);
        var checkedInCustomer = await EnsureSeedCustomerAsync(
            db,
            "dev-queue-checked-in",
            "Sam Checked-in",
            "+2348012300004",
            cancellationToken);

        db.QueueTickets.Add(new QueueTicket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceOfferingId = service.Id,
            StaffMemberId = staff?.Id,
            CustomerId = walkInCustomerA,
            GuestName = "Dev Walk-in Ada",
            GuestPhone = "2348012300001",
            Status = QueueTicketStatus.Waiting,
            PartySize = 1,
            Notes = DevQueueFloorSeedNote,
            PublicToken = CreateQueuePublicToken(),
            Source = QueueTicketSource.WalkIn,
            JoinedAt = now.AddMinutes(-12),
            CreatedAt = now.AddMinutes(-12),
            UpdatedAt = now.AddMinutes(-12),
        });

        db.QueueTickets.Add(new QueueTicket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceOfferingId = service.Id,
            StaffMemberId = staff?.Id,
            CustomerId = walkInCustomerB,
            GuestName = "Dev Walk-in Chi",
            GuestPhone = "2348012300002",
            Status = QueueTicketStatus.Called,
            PartySize = 1,
            Notes = DevQueueFloorSeedNote,
            PublicToken = CreateQueuePublicToken(),
            Source = QueueTicketSource.WalkIn,
            JoinedAt = now.AddMinutes(-25),
            CalledAt = now.AddMinutes(-3),
            CreatedAt = now.AddMinutes(-25),
            UpdatedAt = now.AddMinutes(-3),
        });

        var expectedStart = now.AddHours(2);
        db.Bookings.Add(new BookingRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceOfferingId = service.Id,
            CustomerId = expectedCustomer,
            StaffMemberId = staff?.Id,
            StartAt = expectedStart,
            EndAt = expectedStart.AddMinutes(service.DurationMinutes),
            Status = BookingStatus.Confirmed,
            CreatedAt = now,
            UpdatedAt = now,
        });

        var checkedInStart = now.AddMinutes(-10);
        var checkedInBookingId = Guid.NewGuid();
        db.Bookings.Add(new BookingRecord
        {
            Id = checkedInBookingId,
            TenantId = tenantId,
            ServiceOfferingId = service.Id,
            CustomerId = checkedInCustomer,
            StaffMemberId = staff?.Id,
            StartAt = checkedInStart,
            EndAt = checkedInStart.AddMinutes(service.DurationMinutes),
            Status = BookingStatus.CheckedIn,
            CreatedAt = checkedInStart,
            UpdatedAt = now,
        });

        db.QueueTickets.Add(new QueueTicket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceOfferingId = service.Id,
            StaffMemberId = staff?.Id,
            CustomerId = checkedInCustomer,
            GuestName = "Sam Checked-in",
            GuestPhone = "2348012300004",
            Status = QueueTicketStatus.Waiting,
            PartySize = 1,
            Notes = $"{DevQueueFloorSeedNote} — appointment check-in",
            PublicToken = CreateQueuePublicToken(),
            BookingId = checkedInBookingId,
            Source = QueueTicketSource.Appointment,
            JoinedAt = now.AddMinutes(-5),
            CreatedAt = now.AddMinutes(-5),
            UpdatedAt = now.AddMinutes(-5),
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Guid> EnsureSeedCustomerAsync(
        AdeniDbContext db,
        string auth0Sub,
        string name,
        string phone,
        CancellationToken cancellationToken)
    {
        var existing = await db.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(customer => customer.Auth0Sub == auth0Sub, cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Auth0Sub = auth0Sub,
            Name = name,
            Phone = phone,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return customer.Id;
    }

    private static string CreateQueuePublicToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static async Task SeedDevDepositSettingsAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);

        if (location is null)
        {
            return;
        }

        var profile = await db.BusinessProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entry => entry.TenantId == location.TenantId, cancellationToken);

        if (profile is null || profile.DepositPercent > 0)
        {
            return;
        }

        profile.DepositPercent = 30;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
