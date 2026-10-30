namespace Adeni.Infrastructure.Persistence;
using Adeni.Domain.Finance;
using Microsoft.EntityFrameworkCore;
public sealed partial class AdeniDbContext
{
    private void ConfigureCommerce(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommercePaymentRequest>(e =>
        {
            e.ToTable("commerce_requests", "finance", t => { t.HasCheckConstraint("CK_commerce_requests_Amount", "[Amount] > 0"); t.HasCheckConstraint("CK_commerce_requests_Purpose", "[Purpose] IN (0,1)"); t.HasCheckConstraint("CK_commerce_requests_GiftTender", "[Purpose] = 0 OR [AllowsGiftCard] = 0"); });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Adeni.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Label).HasMaxLength(200);
            e.Property(x => x.CreatedBy).HasMaxLength(128);
            e.Property(x => x.Nonce).HasMaxLength(64);
            e.Property(x => x.KeyVersion).HasMaxLength(32);
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.TokenHash }).IsUnique();
        });
        modelBuilder.Entity<CommercePaymentAttempt>(e =>
        {
            e.ToTable("commerce_attempts", "finance", t => { });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Adeni.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Provider).HasMaxLength(32);
            e.Property(x => x.MerchantReference).HasMaxLength(128);
            e.Property(x => x.CheckoutUrl).HasMaxLength(2048);
            e.Property(x => x.State).HasMaxLength(32);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.TenantId, x.RequestId }).IsUnique();
            e.HasIndex(x => new { x.PaymentIntentId }).IsUnique();
            e.HasOne<CommercePaymentRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PaymentIntentRecord>().WithMany().HasForeignKey(x => new { x.TenantId, x.PaymentIntentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BusinessGiftCard>(e =>
        {
            e.ToTable("gift_cards", "finance", t => { t.HasCheckConstraint("CK_gift_cards_Balance", "[RemainingAmount] >= 0 AND [RemainingAmount] <= [OriginalAmount] AND [OriginalAmount] > 0"); });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Adeni.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.OriginalAmount).HasPrecision(12, 2);
            e.Property(x => x.RemainingAmount).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Nonce).HasMaxLength(64);
            e.Property(x => x.KeyVersion).HasMaxLength(32);
            e.Property(x => x.CodeHash).HasMaxLength(64);
            e.Property(x => x.CodeSuffix).HasMaxLength(8);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.TenantId, x.PurchaseRequestId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CodeHash }).IsUnique();
            e.HasOne<CommercePaymentRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.PurchaseRequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<GiftCardTransaction>(e =>
        {
            e.ToTable("gift_transactions", "finance", t => { t.HasCheckConstraint("CK_gift_transactions_Balance", "[BalanceAfter] >= 0"); t.HasCheckConstraint("CK_gift_transactions_Delta", "([Kind] IN (0,2) AND [Delta] > 0) OR ([Kind] IN (1,3) AND [Delta] < 0)"); });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Adeni.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Delta).HasPrecision(12, 2);
            e.Property(x => x.BalanceAfter).HasPrecision(12, 2);
            e.Property(x => x.Actor).HasMaxLength(128);
            e.HasIndex(x => new { x.TenantId, x.GiftCardId, x.Sequence }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.GiftCardId }).IsUnique().HasFilter("[Kind] = 0");
            e.HasIndex(x => new { x.TenantId, x.ReversesTransactionId }).IsUnique().HasFilter("[ReversesTransactionId] IS NOT NULL");
            e.HasOne<BusinessGiftCard>().WithMany().HasForeignKey(x => new { x.TenantId, x.GiftCardId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CommercePaymentRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GiftCardTransaction>().WithMany().HasForeignKey(x => new { x.TenantId, x.ReversesTransactionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CommerceSettlement>(e =>
        {
            e.ToTable("commerce_settlements", "finance", t => { t.HasCheckConstraint("CK_commerce_settlements_Amount", "[Amount] > 0"); t.HasCheckConstraint("CK_commerce_settlements_Tender", "([PaymentIntentId] IS NULL AND [GiftTransactionId] IS NOT NULL) OR ([PaymentIntentId] IS NOT NULL AND [GiftTransactionId] IS NULL)"); });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Adeni.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.TenantId, x.RequestId }).IsUnique();
            e.HasIndex(x => new { x.PaymentIntentId }).IsUnique().HasFilter("[PaymentIntentId] IS NOT NULL");
            e.HasIndex(x => new { x.GiftTransactionId }).IsUnique().HasFilter("[GiftTransactionId] IS NOT NULL");
            e.HasOne<CommercePaymentRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GiftCardTransaction>().WithMany().HasForeignKey(x => new { x.TenantId, x.GiftTransactionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PaymentIntentRecord>().WithMany().HasForeignKey(x => new { x.TenantId, x.PaymentIntentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CommerceOperation>(e =>
        {
            e.ToTable("commerce_operations", "finance", t => { });
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<Adeni.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.KeyHash).HasMaxLength(64);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Actor).HasMaxLength(128);
            e.HasIndex(x => new { x.TenantId, x.KeyHash }).IsUnique();
            e.HasOne<CommercePaymentRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
