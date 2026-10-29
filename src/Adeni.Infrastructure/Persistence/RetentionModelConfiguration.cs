namespace Adeni.Infrastructure.Persistence;

using Adeni.Domain.Retention;
using Adeni.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

public sealed partial class AdeniDbContext
{
    // Mirrors db/Adeni.Database/retention/Tables (DACPAC is schema authority � no EF migrations).
    private void ConfigureRetention(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PackageDefinition>(e =>
        {
            e.ToTable("package_definitions", "retention", t =>
            {
                t.HasCheckConstraint("CK_package_definitions_ListPrice", "[ListPrice] >= 0");
                t.HasCheckConstraint("CK_package_definitions_RedemptionScope", "[RedemptionScope] IN (0,1,2)");
                t.HasCheckConstraint("CK_package_definitions_SellScope", "[SellScope] IN (0,1)");
            });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.ListPrice).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.RedemptionLocationIdsJson).HasMaxLength(4000);
            e.Property(x => x.SellLocationIdsJson).HasMaxLength(4000);
            e.Ignore(x => x.EffectiveExpiryMonths);
            e.HasIndex(x => new { x.TenantId, x.IsActive, x.CreatedAt });
        });

        modelBuilder.Entity<PackageBenefit>(e =>
        {
            e.ToTable("package_benefits", "retention", t => t.HasCheckConstraint("CK_package_benefits_Quantity", "[QuantityGranted] BETWEEN 1 AND 1000"));
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ServiceName).HasMaxLength(200);
            e.HasIndex(x => new { x.TenantId, x.PackageDefinitionId, x.ServiceOfferingId }).IsUnique();
            e.HasOne<PackageDefinition>().WithMany(x => x.Benefits)
                .HasForeignKey(x => new { x.TenantId, x.PackageDefinitionId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MembershipPlan>(e =>
        {
            e.ToTable("membership_plans", "retention", t =>
            {
                t.HasCheckConstraint("CK_membership_plans_Price", "[Price] > 0");
                t.HasCheckConstraint("CK_membership_plans_BillingInterval", "[BillingInterval] IN (0,1,2)");
                t.HasCheckConstraint("CK_membership_plans_RolloverPolicy", "[RolloverPolicy] IN (0,1)");
                t.HasCheckConstraint("CK_membership_plans_CancellationPolicy", "[CancellationPolicy] IN (0,1)");
                t.HasCheckConstraint("CK_membership_plans_PastDueGraceDays", "[PastDueGraceDays] BETWEEN 0 AND 30");
            });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasIndex(x => new { x.TenantId, x.IsActive, x.CreatedAt });
        });

        modelBuilder.Entity<MembershipPlanBenefit>(e =>
        {
            e.ToTable("membership_plan_benefits", "retention", t => t.HasCheckConstraint("CK_membership_plan_benefits_Quantity", "[QuantityPerCycle] BETWEEN 1 AND 1000"));
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ServiceName).HasMaxLength(200);
            e.HasIndex(x => new { x.TenantId, x.MembershipPlanId, x.ServiceOfferingId }).IsUnique();
            e.HasOne<MembershipPlan>().WithMany(x => x.Benefits)
                .HasForeignKey(x => new { x.TenantId, x.MembershipPlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerPackage>(e =>
        {
            e.ToTable("customer_packages", "retention", t =>
            {
                t.HasCheckConstraint("CK_customer_packages_Status", "[Status] IN (0,1,2,3)");
                t.HasCheckConstraint("CK_customer_packages_SoldChannel", "[SoldChannel] IN (0,1,2)");
                t.HasCheckConstraint("CK_customer_packages_RedemptionScope", "[RedemptionScopeSnapshot] IN (0,1,2)");
            });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.NameSnapshot).HasMaxLength(200);
            e.Property(x => x.ListPriceSnapshot).HasPrecision(12, 2);
            e.Property(x => x.CurrencySnapshot).HasMaxLength(3);
            e.Property(x => x.RedemptionLocationIdsJson).HasMaxLength(4000);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.SoldBy).HasMaxLength(128);
            e.Property(x => x.VoidedBy).HasMaxLength(128);
            e.Property(x => x.VoidReason).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.TenantId, x.TenantCustomerId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAt });
            e.HasIndex(x => x.PaymentIntentId).IsUnique().HasFilter("[PaymentIntentId] IS NOT NULL");
            e.HasOne<PackageDefinition>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.PackageDefinitionId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerMembership>(e =>
        {
            e.ToTable("customer_memberships", "retention", t =>
            {
                t.HasCheckConstraint("CK_customer_memberships_Status", "[Status] IN (0,1,2,3,4,5,6)");
                t.HasCheckConstraint("CK_customer_memberships_EnrolChannel", "[EnrolChannel] IN (0,1,2)");
                t.HasCheckConstraint("CK_customer_memberships_BillingInterval", "[BillingIntervalSnapshot] IN (0,1,2)");
                t.HasCheckConstraint("CK_customer_memberships_RolloverPolicy", "[RolloverPolicySnapshot] IN (0,1)");
                t.HasCheckConstraint("CK_customer_memberships_CancellationPolicy", "[CancellationPolicySnapshot] IN (0,1)");
            });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.NameSnapshot).HasMaxLength(200);
            e.Property(x => x.PriceSnapshot).HasPrecision(12, 2);
            e.Property(x => x.CurrencySnapshot).HasMaxLength(3);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.EnrolledBy).HasMaxLength(128);
            e.Property(x => x.CancelledBy).HasMaxLength(128);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.Property(x => x.ProviderName).HasMaxLength(32);
            e.Property(x => x.ProviderMandateRef).HasMaxLength(128);
            e.Property(x => x.ProviderSubscriptionRef).HasMaxLength(128);
            e.Ignore(x => x.IsOnline);
            e.Ignore(x => x.GracePeriod);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.TenantId, x.TenantCustomerId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.Status, x.CurrentCycleEnd });
            // One live (non-terminal) membership per customer and plan: Pending, Active, PastDue, Frozen, CancelPending.
            e.HasIndex(x => new { x.TenantId, x.TenantCustomerId, x.MembershipPlanId })
                .IsUnique()
                .HasFilter("[Status] IN (0,1,2,3,4)");
            e.HasOne<MembershipPlan>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.MembershipPlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MembershipPayment>(e =>
        {
            e.ToTable("membership_payments", "retention", t =>
            {
                t.HasCheckConstraint("CK_membership_payments_Kind", "[Kind] IN (0,1)");
                t.HasCheckConstraint("CK_membership_payments_Status", "[Status] IN (0,1,2,3)");
                t.HasCheckConstraint("CK_membership_payments_CycleNumber", "[CycleNumber] >= 1");
            });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.ProviderReference).HasMaxLength(128);
            e.Property(x => x.FailureReason).HasMaxLength(200);
            e.Property(x => x.RowVersion).IsRowVersion();
            // One provider intent maps to exactly one cycle charge (replay-safe).
            e.HasIndex(x => x.PaymentIntentId).IsUnique();
            // At most one open-or-paid charge per membership cycle; failed/unapplied attempts may be superseded.
            e.HasIndex(x => new { x.TenantId, x.CustomerMembershipId, x.CycleNumber })
                .IsUnique()
                .HasFilter("[Status] IN (0,1)");
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
            e.HasOne<CustomerMembership>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.CustomerMembershipId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerMembershipBenefit>(e =>
        {
            e.ToTable("customer_membership_benefits", "retention", t => t.HasCheckConstraint("CK_customer_membership_benefits_Quantity", "[QuantityPerCycle] BETWEEN 1 AND 1000"));
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ServiceName).HasMaxLength(200);
            e.HasIndex(x => new { x.TenantId, x.CustomerMembershipId, x.ServiceOfferingId }).IsUnique();
            e.HasOne<CustomerMembership>().WithMany(x => x.Benefits)
                .HasForeignKey(x => new { x.TenantId, x.CustomerMembershipId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BenefitBalance>(e =>
        {
            e.ToTable("benefit_balances", "retention", t =>
            {
                t.HasCheckConstraint(
                    "CK_benefit_balances_Invariant",
                    "[Granted] > 0 AND [Used] >= 0 AND [Reserved] >= 0 AND [Forfeited] >= 0 AND ([Used] + [Reserved] + [Forfeited]) <= [Granted]");
                t.HasCheckConstraint(
                    "CK_benefit_balances_Owner",
                    "([CustomerPackageId] IS NOT NULL AND [CustomerMembershipId] IS NULL AND [CycleNumber] IS NULL) OR ([CustomerPackageId] IS NULL AND [CustomerMembershipId] IS NOT NULL AND [CycleNumber] >= 1)");
            });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ServiceName).HasMaxLength(200);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.Remaining);
            e.Ignore(x => x.CarryableToNextCycle);
            e.HasIndex(x => new { x.TenantId, x.CustomerPackageId, x.ServiceOfferingId })
                .IsUnique()
                .HasFilter("[CustomerPackageId] IS NOT NULL");
            e.HasIndex(x => new { x.TenantId, x.CustomerMembershipId, x.CycleNumber, x.ServiceOfferingId })
                .IsUnique()
                .HasFilter("[CustomerMembershipId] IS NOT NULL");
            e.HasOne<CustomerPackage>().WithMany(x => x.Balances)
                .HasForeignKey(x => new { x.TenantId, x.CustomerPackageId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CustomerMembership>().WithMany(x => x.Balances)
                .HasForeignKey(x => new { x.TenantId, x.CustomerMembershipId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BenefitTransaction>(e =>
        {
            e.ToTable("benefit_transactions", "retention", t =>
            {
                t.HasCheckConstraint("CK_benefit_transactions_Kind", "[Kind] IN (0,1,2,3,4,5,6)");
                t.HasCheckConstraint("CK_benefit_transactions_Quantity", "[Quantity] > 0");
            });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Actor).HasMaxLength(128);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.BenefitBalanceId, x.Sequence }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.BenefitBalanceId, x.BookingId, x.Kind })
                .IsUnique()
                .HasFilter("[BookingId] IS NOT NULL");
            e.HasOne<BenefitBalance>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.BenefitBalanceId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BenefitOperation>(e =>
        {
            e.ToTable("benefit_operations", "retention", t => { });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.KeyHash).HasMaxLength(64);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Actor).HasMaxLength(128);
            e.HasIndex(x => new { x.TenantId, x.KeyHash }).IsUnique();
            e.HasOne<CustomerPackage>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ResultCustomerPackageId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CustomerMembership>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ResultCustomerMembershipId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}