/*
  DEV ONLY - removes retention demo rows created by seed-lekki-cuts-retention.sql.
  Does not remove base DevelopmentDataSeeder or seed-lekki-cuts-demo.sql rows.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @Marker NVARCHAR(64) = N'seed-lekki-retention-v1';

DECLARE @TenantId UNIQUEIDENTIFIER =
    (SELECT TOP (1) TenantId FROM tenancy.business_locations WHERE Slug = N'lekki-cuts');
IF @TenantId IS NULL
BEGIN
    PRINT 'Tenant lekki-cuts not found — nothing to clean.';
    RETURN;
END;

BEGIN TRY
BEGIN TRANSACTION;

DECLARE @PkgIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @PkgIds SELECT Id FROM retention.package_definitions WHERE TenantId = @TenantId AND Description = @Marker;

DECLARE @PlanIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @PlanIds SELECT Id FROM retention.membership_plans WHERE TenantId = @TenantId AND Description = @Marker;

DECLARE @CustPkg TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @CustPkg SELECT Id FROM retention.customer_packages WHERE TenantId = @TenantId AND Note = @Marker;

DECLARE @CustMem TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @CustMem SELECT Id FROM retention.customer_memberships WHERE TenantId = @TenantId AND Note = @Marker;

DECLARE @Bal TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @Bal
SELECT Id FROM retention.benefit_balances
WHERE TenantId = @TenantId
  AND (CustomerPackageId IN (SELECT Id FROM @CustPkg)
       OR CustomerMembershipId IN (SELECT Id FROM @CustMem));

DELETE FROM retention.benefit_transactions WHERE TenantId = @TenantId AND BenefitBalanceId IN (SELECT Id FROM @Bal);
DELETE FROM retention.benefit_balances WHERE Id IN (SELECT Id FROM @Bal);

DELETE FROM retention.membership_payments WHERE TenantId = @TenantId AND CustomerMembershipId IN (SELECT Id FROM @CustMem);
DELETE FROM retention.customer_membership_benefits WHERE TenantId = @TenantId AND CustomerMembershipId IN (SELECT Id FROM @CustMem);
DELETE FROM retention.customer_memberships WHERE Id IN (SELECT Id FROM @CustMem);

DELETE FROM retention.customer_packages WHERE Id IN (SELECT Id FROM @CustPkg);

DELETE FROM retention.loyalty_reward_redemptions WHERE TenantId = @TenantId
  AND LoyaltyAccountId IN (
      SELECT la.Id FROM retention.loyalty_accounts la
      WHERE la.TenantId = @TenantId
        AND EXISTS (
            SELECT 1 FROM retention.loyalty_transactions lt
            WHERE lt.TenantId = @TenantId AND lt.LoyaltyAccountId = la.Id AND lt.Actor = @Marker));

DELETE FROM retention.loyalty_transactions WHERE TenantId = @TenantId AND Actor = @Marker;

DELETE la FROM retention.loyalty_accounts la
WHERE la.TenantId = @TenantId
  AND NOT EXISTS (SELECT 1 FROM retention.loyalty_transactions lt WHERE lt.TenantId = @TenantId AND lt.LoyaltyAccountId = la.Id);

DELETE FROM retention.referral_attributions WHERE TenantId = @TenantId
  AND ReferralCodeId IN (SELECT Id FROM retention.referral_codes WHERE TenantId = @TenantId AND Code LIKE N'LKSEED%');
DELETE FROM retention.referral_codes WHERE TenantId = @TenantId AND Code LIKE N'LKSEED%';
DELETE FROM retention.referral_programs WHERE TenantId = @TenantId AND UpdatedBy = @Marker;

DELETE FROM retention.loyalty_rewards WHERE TenantId = @TenantId AND (Name LIKE N'[SEED]%' OR Name IN (
    N'Complimentary beard trim', N'Complimentary haircut', N'Deep conditioning add-on', N'Welcome gift (retired)'));
DELETE FROM retention.loyalty_tiers WHERE TenantId = @TenantId AND (Name LIKE N'[SEED]%' OR Name IN (N'Bronze', N'Silver', N'Gold'));
DELETE FROM retention.loyalty_programs WHERE TenantId = @TenantId AND UpdatedBy = @Marker;

DELETE FROM retention.package_benefits WHERE TenantId = @TenantId AND PackageDefinitionId IN (SELECT Id FROM @PkgIds);
DELETE FROM retention.package_definitions WHERE Id IN (SELECT Id FROM @PkgIds);

DELETE FROM retention.membership_plan_benefits WHERE TenantId = @TenantId AND MembershipPlanId IN (SELECT Id FROM @PlanIds);
DELETE FROM retention.membership_plans WHERE Id IN (SELECT Id FROM @PlanIds);

COMMIT TRANSACTION;
PRINT 'Lekki Cuts retention demo seed removed.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

GO
