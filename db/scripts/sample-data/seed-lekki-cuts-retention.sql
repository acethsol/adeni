/*
  DEV ONLY - retention catalog + volume for Lekki Cuts (packages, memberships, loyalty).
  Run after DevelopmentDataSeeder + seed-lekki-cuts-demo.sql (needs tenant_customers + services).

  Marker: seed-lekki-retention-v1 (catalog Description / instance Note / loyalty Actor)
  Undo:   seed-lekki-cuts-retention-cleanup.sql

  Customer app: sign in as auth0|local-customer — packages, memberships, loyalty at Lekki Cuts.
  Business portal: Retention tabs under Account (auth0|local-business).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @TenantSlug  NVARCHAR(64) = N'lekki-cuts';
DECLARE @Marker      NVARCHAR(64) = N'seed-lekki-retention-v1';
DECLARE @OwnerSub    NVARCHAR(128) = N'auth0|local-business';
DECLARE @Now         DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();

DECLARE @TenantId UNIQUEIDENTIFIER =
    (SELECT TOP (1) TenantId FROM tenancy.business_locations WHERE Slug = @TenantSlug);
IF @TenantId IS NULL
    THROW 50001, 'Tenant lekki-cuts not found. Start the API in Development once.', 1;

IF EXISTS (SELECT 1 FROM retention.package_definitions WHERE TenantId = @TenantId AND Description = @Marker)
BEGIN
    PRINT 'Lekki retention seed already applied — skipping.';
    RETURN;
END;

DECLARE @PrimaryLoc UNIQUEIDENTIFIER, @Phase2Loc UNIQUEIDENTIFIER;
SELECT TOP (1) @PrimaryLoc = Id FROM tenancy.business_locations WHERE TenantId = @TenantId AND Slug = @TenantSlug;
SELECT TOP (1) @Phase2Loc = Id FROM tenancy.business_locations WHERE TenantId = @TenantId AND Slug = N'lekki-cuts-phase2';

DECLARE @SvcHaircut UNIQUEIDENTIFIER, @SvcBarber UNIQUEIDENTIFIER, @SvcBeard UNIQUEIDENTIFIER,
        @SvcBraids UNIQUEIDENTIFIER, @SvcStyling UNIQUEIDENTIFIER;
SELECT @SvcHaircut = Id FROM booking.service_offerings WHERE TenantId = @TenantId AND Name = N'Haircut';
SELECT @SvcBarber = Id FROM booking.service_offerings WHERE TenantId = @TenantId AND Name = N'Barbering';
SELECT @SvcBeard = Id FROM booking.service_offerings WHERE TenantId = @TenantId AND Name = N'Beard Grooming';
SELECT @SvcBraids = Id FROM booking.service_offerings WHERE TenantId = @TenantId AND Name = N'Braids';
SELECT @SvcStyling = Id FROM booking.service_offerings WHERE TenantId = @TenantId AND Name = N'Hair Styling';

IF @SvcHaircut IS NULL OR @SvcBarber IS NULL
    THROW 50002, 'Expected Lekki service offerings (Haircut, Barbering). Run DevelopmentDataSeeder.', 1;

DECLARE @Phase2Json NVARCHAR(4000) = CASE WHEN @Phase2Loc IS NOT NULL
    THEN N'["' + CONVERT(NVARCHAR(36), @Phase2Loc) + N'"]' ELSE NULL END;

------------------------------------------------------------------------------
-- Catalog: packages
------------------------------------------------------------------------------
DECLARE @Pkg5Cut UNIQUEIDENTIFIER = NEWID();
DECLARE @PkgGroom UNIQUEIDENTIFIER = NEWID();
DECLARE @PkgBraids UNIQUEIDENTIFIER = NEWID();
DECLARE @PkgPhase2 UNIQUEIDENTIFIER = NEWID();
DECLARE @PkgLegacy UNIQUEIDENTIFIER = NEWID();
DECLARE @PkgStarter UNIQUEIDENTIFIER = NEWID();

INSERT retention.package_definitions
    (Id, TenantId, Name, Description, ListPrice, Currency, IsActive, DefaultExpiryMonths,
     RedemptionScope, SellScope, RedemptionLocationIdsJson, SellLocationIdsJson, CreatedAt, UpdatedAt, DeactivatedAt)
VALUES
    (@Pkg5Cut, @TenantId, N'5-Visit Cut Club', @Marker, 45000, N'NGN', 1, 6, 0, 0, NULL, NULL, @Now, @Now, NULL),
    (@PkgGroom, @TenantId, N'Fade & Beard Bundle', @Marker, 38000, N'NGN', 1, 4, 0, 0, NULL, NULL, @Now, @Now, NULL),
    (@PkgBraids, @TenantId, N'Braids Duo Pack', @Marker, 72000, N'NGN', 1, 3, 0, 0, NULL, NULL, @Now, @Now, NULL),
    (@PkgPhase2, @TenantId, N'Phase 2 Branch Pack', @Marker, 28000, N'NGN', 1, 6, 2, 0, @Phase2Json, NULL, @Now, @Now, NULL),
    (@PkgLegacy, @TenantId, N'2024 Legacy Pack (retired)', @Marker, 15000, N'NGN', 0, 12, 0, 0, NULL, NULL, DATEADD(YEAR, -1, @Now), @Now, @Now),
    (@PkgStarter, @TenantId, N'Starter Trio', @Marker, 24000, N'NGN', 1, 12, 1, 0, NULL, NULL, @Now, @Now, NULL);

INSERT retention.package_benefits (Id, TenantId, PackageDefinitionId, ServiceOfferingId, ServiceName, QuantityGranted, SortOrder)
VALUES
    (NEWID(), @TenantId, @Pkg5Cut, @SvcHaircut, N'Haircut', 5, 0),
    (NEWID(), @TenantId, @PkgGroom, @SvcHaircut, N'Haircut', 3, 0),
    (NEWID(), @TenantId, @PkgGroom, @SvcBeard, N'Beard Grooming', 3, 1),
    (NEWID(), @TenantId, @PkgBraids, @SvcBraids, N'Braids', 2, 0),
    (NEWID(), @TenantId, @PkgPhase2, @SvcBarber, N'Barbering', 4, 0),
    (NEWID(), @TenantId, @PkgLegacy, @SvcHaircut, N'Haircut', 3, 0),
    (NEWID(), @TenantId, @PkgStarter, @SvcHaircut, N'Haircut', 3, 0);

------------------------------------------------------------------------------
-- Catalog: membership plans
------------------------------------------------------------------------------
DECLARE @PlanFade UNIQUEIDENTIFIER = NEWID();
DECLARE @PlanGroom UNIQUEIDENTIFIER = NEWID();
DECLARE @PlanBraidsQ UNIQUEIDENTIFIER = NEWID();
DECLARE @PlanVip UNIQUEIDENTIFIER = NEWID();
DECLARE @PlanRetired UNIQUEIDENTIFIER = NEWID();

INSERT retention.membership_plans
    (Id, TenantId, Name, Description, Price, Currency, BillingInterval, RolloverPolicy, CancellationPolicy,
     AllowFreeze, PastDueGraceDays, IsActive, CreatedAt, UpdatedAt, DeactivatedAt)
VALUES
    (@PlanFade, @TenantId, N'Fade Club', @Marker, 25000, N'NGN', 0, 1, 0, 1, 7, 1, @Now, @Now, NULL),
    (@PlanGroom, @TenantId, N'Groom & Go Monthly', @Marker, 32000, N'NGN', 0, 0, 0, 1, 5, 1, @Now, @Now, NULL),
    (@PlanBraidsQ, @TenantId, N'Braids Quarterly', @Marker, 85000, N'NGN', 1, 0, 0, 0, 7, 1, @Now, @Now, NULL),
    (@PlanVip, @TenantId, N'VIP Annual', @Marker, 240000, N'NGN', 2, 1, 0, 1, 14, 1, @Now, @Now, NULL),
    (@PlanRetired, @TenantId, N'Legacy Members Plan', @Marker, 18000, N'NGN', 0, 0, 0, 0, 3, 0, DATEADD(YEAR, -1, @Now), @Now, @Now);

INSERT retention.membership_plan_benefits (Id, TenantId, MembershipPlanId, ServiceOfferingId, ServiceName, QuantityPerCycle, SortOrder)
VALUES
    (NEWID(), @TenantId, @PlanFade, @SvcHaircut, N'Haircut', 2, 0),
    (NEWID(), @TenantId, @PlanGroom, @SvcHaircut, N'Haircut', 1, 0),
    (NEWID(), @TenantId, @PlanGroom, @SvcBeard, N'Beard Grooming', 2, 1),
    (NEWID(), @TenantId, @PlanBraidsQ, @SvcBraids, N'Braids', 2, 0),
    (NEWID(), @TenantId, @PlanVip, @SvcHaircut, N'Haircut', 2, 0),
    (NEWID(), @TenantId, @PlanVip, @SvcStyling, N'Hair Styling', 1, 1),
    (NEWID(), @TenantId, @PlanRetired, @SvcHaircut, N'Haircut', 1, 0);

------------------------------------------------------------------------------
-- Loyalty program, tiers, rewards, referrals
------------------------------------------------------------------------------
DECLARE @LoyaltyProgramId UNIQUEIDENTIFIER = NEWID();
INSERT retention.loyalty_programs
    (Id, TenantId, IsEnabled, Currency, EarnPoints, EarnPerAmount, RedeemPoints, RedeemValue, MinRedeemPoints,
     PointsExpiryMonths, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (@LoyaltyProgramId, @TenantId, 1, N'NGN', 1, 100, 100, 500, 100, 12, @Marker, @Now, @Now);

DECLARE @TierBronze UNIQUEIDENTIFIER = NEWID();
DECLARE @TierSilver UNIQUEIDENTIFIER = NEWID();
DECLARE @TierGold UNIQUEIDENTIFIER = NEWID();
INSERT retention.loyalty_tiers (Id, TenantId, Name, MinLifetimeEarned, SortOrder, CreatedAt, UpdatedAt)
VALUES
    (@TierBronze, @TenantId, N'Bronze', 0, 0, @Now, @Now),
    (@TierSilver, @TenantId, N'Silver', 500, 1, @Now, @Now),
    (@TierGold, @TenantId, N'Gold', 2000, 2, @Now, @Now);

INSERT retention.loyalty_rewards (Id, TenantId, Name, Kind, PointsCost, IsActive, SortOrder, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @TenantId, N'Complimentary beard trim', 0, 150, 1, 0, @Now, @Now),
    (NEWID(), @TenantId, N'Complimentary haircut', 0, 300, 1, 1, @Now, @Now),
    (NEWID(), @TenantId, N'Deep conditioning add-on', 0, 220, 1, 2, @Now, @Now),
    (NEWID(), @TenantId, N'Welcome gift (retired)', 0, 50, 0, 3, @Now, @Now);

INSERT retention.referral_programs
    (Id, TenantId, IsEnabled, ReferrerRewardPoints, RefereeRewardPoints, MaxRewardsPerReferrer, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @TenantId, 1, 200, 100, NULL, @Marker, @Now, @Now);

------------------------------------------------------------------------------
-- Customer roster (tenant_customers)
------------------------------------------------------------------------------
CREATE TABLE #Cust (
    rn INT NOT NULL PRIMARY KEY,
    TenantCustomerId UNIQUEIDENTIFIER NOT NULL,
    DisplayName NVARCHAR(200) NOT NULL
);
INSERT #Cust (rn, TenantCustomerId, DisplayName)
SELECT ROW_NUMBER() OVER (ORDER BY tc.LastSeenAt DESC, tc.CreatedAt),
       tc.Id,
       tc.DisplayName
FROM tenancy.tenant_customers tc
WHERE tc.TenantId = @TenantId;

DECLARE @CustCount INT = (SELECT COUNT(*) FROM #Cust);
IF @CustCount < 10
    THROW 50003, 'Need tenant_customers (run seed-lekki-cuts-demo.sql or restart API with SampleData).', 1;

DECLARE @LocalTc UNIQUEIDENTIFIER = (
    SELECT TOP (1) tc.Id
    FROM tenancy.tenant_customers tc
    LEFT JOIN [identity].customers c ON c.Id = tc.CustomerId
    WHERE tc.TenantId = @TenantId
      AND (c.Auth0Sub = N'auth0|local-customer' OR tc.Email = N'local@seed.lekki.local')
    ORDER BY CASE WHEN c.Auth0Sub = N'auth0|local-customer' THEN 0 ELSE 1 END
);

CREATE TABLE #PkgDef (
    rn INT NOT NULL PRIMARY KEY,
    PkgId UNIQUEIDENTIFIER NOT NULL,
    Scope INT NOT NULL,
    LocJson NVARCHAR(4000) NULL
);
INSERT #PkgDef (rn, PkgId, Scope, LocJson)
VALUES (1, @Pkg5Cut, 0, NULL), (2, @PkgGroom, 0, NULL), (3, @PkgBraids, 0, NULL),
       (4, @PkgPhase2, 2, @Phase2Json), (5, @PkgStarter, 1, NULL);

CREATE TABLE #PlanDef (rn INT NOT NULL PRIMARY KEY, PlanId UNIQUEIDENTIFIER NOT NULL);
INSERT #PlanDef VALUES (1, @PlanFade), (2, @PlanGroom), (3, @PlanBraidsQ), (4, @PlanVip);

CREATE TABLE #Loc (rn INT NOT NULL PRIMARY KEY, LocId UNIQUEIDENTIFIER NOT NULL);
INSERT #Loc (rn, LocId)
SELECT ROW_NUMBER() OVER (ORDER BY CASE WHEN Slug = @TenantSlug THEN 0 ELSE 1 END, CreatedAt), Id
FROM tenancy.business_locations WHERE TenantId = @TenantId AND IsActive = 1;

------------------------------------------------------------------------------
-- Sold packages (~60)
------------------------------------------------------------------------------
DECLARE @i INT = 1;
WHILE @i <= 60
BEGIN
    DECLARE @tcPkg UNIQUEIDENTIFIER = (SELECT TenantCustomerId FROM #Cust WHERE rn = ((@i - 1) % @CustCount) + 1);
    DECLARE @pd INT = ((@i - 1) % 5) + 1;
    DECLARE @pkgId UNIQUEIDENTIFIER = (SELECT PkgId FROM #PkgDef WHERE rn = @pd);
    DECLARE @scope INT = (SELECT Scope FROM #PkgDef WHERE rn = @pd);
    DECLARE @locJson NVARCHAR(4000) = (SELECT LocJson FROM #PkgDef WHERE rn = @pd);
    DECLARE @soldLoc UNIQUEIDENTIFIER = (SELECT LocId FROM #Loc WHERE rn = ((@i - 1) % (SELECT COUNT(*) FROM #Loc)) + 1);
    DECLARE @cpId UNIQUEIDENTIFIER = NEWID();
    DECLARE @soldAt DATETIMEOFFSET(7) = DATEADD(DAY, -((@i * 3) % 120), @Now);
    DECLARE @expires DATETIMEOFFSET(7) = DATEADD(MONTH, 6, @soldAt);

    INSERT retention.customer_packages
        (Id, TenantId, TenantCustomerId, PackageDefinitionId, SoldAtLocationId, SoldChannel, PaymentIntentId, Status,
         NameSnapshot, ListPriceSnapshot, CurrencySnapshot, RedemptionScopeSnapshot, RedemptionLocationIdsJson,
         Note, SoldBy, CreatedAt, ActivatedAt, ExpiresAt)
    SELECT @cpId, @TenantId, @tcPkg, @pkgId, @soldLoc, @i % 3, NULL, 1,
           pd.Name, pd.ListPrice, pd.Currency, @scope, @locJson,
           @Marker, @OwnerSub, @soldAt, @soldAt, @expires
    FROM retention.package_definitions pd WHERE pd.Id = @pkgId;

    INSERT retention.benefit_balances
        (Id, TenantId, CustomerPackageId, CustomerMembershipId, CycleNumber, ServiceOfferingId, ServiceName,
         Granted, Used, Reserved, Forfeited, Sequence, ExpiresAt, CarriedIn)
    SELECT NEWID(), @TenantId, @cpId, NULL, NULL, pb.ServiceOfferingId, pb.ServiceName,
           pb.QuantityGranted,
           CASE WHEN pb.QuantityGranted > 1 AND @i % 4 = 0 THEN 1 ELSE 0 END,
           CASE WHEN pb.QuantityGranted > 2 AND @i % 11 = 0 THEN 1 ELSE 0 END,
           0, 0, @expires, 0
    FROM retention.package_benefits pb
    WHERE pb.PackageDefinitionId = @pkgId;

    INSERT retention.benefit_transactions
        (Id, TenantId, BenefitBalanceId, Sequence, Kind, Quantity, Delta, ReservedAfter, UsedAfter, RemainingAfter,
         BookingId, BusinessLocationId, Actor, Reason, OccurredAt)
    SELECT NEWID(), @TenantId, bb.Id, 0, 0, bb.Granted, bb.Granted, bb.Reserved, bb.Used, bb.Granted - bb.Used - bb.Reserved - bb.Forfeited,
           NULL, @soldLoc, @Marker, @Marker, @soldAt
    FROM retention.benefit_balances bb
    WHERE bb.CustomerPackageId = @cpId;

    SET @i += 1;
END;

PRINT CONCAT('customer_packages seeded: ', 60);

------------------------------------------------------------------------------
-- Memberships (~35, mixed status for portal testing; unique customer + plan pairs)
------------------------------------------------------------------------------
CREATE TABLE #MemSeed (
    seq INT NOT NULL PRIMARY KEY,
    TenantCustomerId UNIQUEIDENTIFIER NOT NULL,
    PlanId UNIQUEIDENTIFIER NOT NULL
);
INSERT #MemSeed (seq, TenantCustomerId, PlanId)
SELECT ROW_NUMBER() OVER (ORDER BY c.rn, p.rn), c.TenantCustomerId, p.PlanId
FROM #Cust c
CROSS JOIN #PlanDef p;

SET @i = 1;
WHILE @i <= 35
BEGIN
    DECLARE @tcMem UNIQUEIDENTIFIER = (SELECT TenantCustomerId FROM #MemSeed WHERE seq = @i);
    DECLARE @planId UNIQUEIDENTIFIER = (SELECT PlanId FROM #MemSeed WHERE seq = @i);
    DECLARE @enrolLoc UNIQUEIDENTIFIER = (SELECT LocId FROM #Loc WHERE rn = ((@i - 1) % (SELECT COUNT(*) FROM #Loc)) + 1);
    DECLARE @cmId UNIQUEIDENTIFIER = NEWID();
    DECLARE @enrolAt DATETIMEOFFSET(7) = DATEADD(DAY, -((@i * 5) % 200), @Now);
    DECLARE @cycleStart DATETIMEOFFSET(7) = DATEADD(DAY, -((@i * 3) % 25), @Now);
    DECLARE @cycleEnd DATETIMEOFFSET(7) = DATEADD(MONTH, 1, @cycleStart);
    DECLARE @memStatus INT = CASE
        WHEN @i % 17 = 0 THEN 3
        WHEN @i % 13 = 0 THEN 2
        WHEN @i % 11 = 0 THEN 4
        WHEN @i >= 35 THEN 0
        ELSE 1 END;
    DECLARE @cycleNum INT = CASE WHEN @memStatus = 0 THEN 0 ELSE 1 + (@i % 3) END;

    IF @memStatus IN (2, 3, 4)
        SET @cycleEnd = DATEADD(DAY, CASE WHEN @memStatus = 4 THEN 14 ELSE -3 END, @Now);

    INSERT retention.customer_memberships
        (Id, TenantId, TenantCustomerId, MembershipPlanId, EnrolledAtLocationId, EnrolChannel, Status,
         NameSnapshot, PriceSnapshot, CurrencySnapshot, BillingIntervalSnapshot, RolloverPolicySnapshot,
         CancellationPolicySnapshot, AllowFreezeSnapshot, PastDueGraceDaysSnapshot, Note, EnrolledBy,
         CreatedAt, ActivatedAt, CurrentCycleNumber, CurrentCycleStart, CurrentCycleEnd,
         FrozenAt, CancelRequestedAt, PastDueSince, FailedPaymentCount)
    SELECT @cmId, @TenantId, @tcMem, @planId, @enrolLoc, @i % 2, @memStatus,
           mp.Name, mp.Price, mp.Currency, mp.BillingInterval, mp.RolloverPolicy, mp.CancellationPolicy,
           mp.AllowFreeze, mp.PastDueGraceDays, @Marker, @OwnerSub,
           @enrolAt, CASE WHEN @memStatus = 0 THEN NULL ELSE @enrolAt END, @cycleNum,
           CASE WHEN @memStatus = 0 THEN NULL ELSE @cycleStart END,
           CASE WHEN @memStatus = 0 THEN NULL ELSE @cycleEnd END,
           CASE WHEN @memStatus = 3 THEN DATEADD(DAY, -2, @Now) ELSE NULL END,
           CASE WHEN @memStatus = 4 THEN DATEADD(DAY, -1, @Now) ELSE NULL END,
           CASE WHEN @memStatus = 2 THEN DATEADD(DAY, -5, @Now) ELSE NULL END,
           CASE WHEN @memStatus = 2 THEN 1 ELSE 0 END
    FROM retention.membership_plans mp WHERE mp.Id = @planId;

    IF @memStatus <> 0
    BEGIN
        INSERT retention.customer_membership_benefits
            (Id, TenantId, CustomerMembershipId, ServiceOfferingId, ServiceName, QuantityPerCycle, SortOrder)
        SELECT NEWID(), @TenantId, @cmId, mpb.ServiceOfferingId, mpb.ServiceName, mpb.QuantityPerCycle, mpb.SortOrder
        FROM retention.membership_plan_benefits mpb WHERE mpb.MembershipPlanId = @planId;

        INSERT retention.benefit_balances
            (Id, TenantId, CustomerPackageId, CustomerMembershipId, CycleNumber, ServiceOfferingId, ServiceName,
             Granted, Used, Reserved, Forfeited, Sequence, ExpiresAt, CarriedIn)
        SELECT NEWID(), @TenantId, NULL, @cmId, @cycleNum, cmb.ServiceOfferingId, cmb.ServiceName,
               cmb.QuantityPerCycle + CASE WHEN mp.RolloverPolicy = 1 AND @i % 5 = 0 THEN 1 ELSE 0 END,
               CASE WHEN @i % 6 = 0 THEN 1 ELSE 0 END,
               0, 0, 0, @cycleEnd, CASE WHEN mp.RolloverPolicy = 1 AND @i % 5 = 0 THEN 1 ELSE 0 END
        FROM retention.customer_membership_benefits cmb
        JOIN retention.membership_plans mp ON mp.Id = @planId
        WHERE cmb.CustomerMembershipId = @cmId;

        INSERT retention.benefit_transactions
            (Id, TenantId, BenefitBalanceId, Sequence, Kind, Quantity, Delta, ReservedAfter, UsedAfter, RemainingAfter,
             BookingId, BusinessLocationId, Actor, Reason, OccurredAt)
        SELECT NEWID(), @TenantId, bb.Id, 0, 0, bb.Granted, bb.Granted, bb.Reserved, bb.Used,
               bb.Granted - bb.Used - bb.Reserved - bb.Forfeited,
               NULL, @enrolLoc, @Marker, @Marker, @cycleStart
        FROM retention.benefit_balances bb
        WHERE bb.CustomerMembershipId = @cmId AND bb.CycleNumber = @cycleNum;

        IF @memStatus IN (1, 2)
        BEGIN
            INSERT retention.membership_payments
                (Id, TenantId, CustomerMembershipId, CycleNumber, Kind, PaymentIntentId, Amount, Currency, Status,
                 ProviderReference, CreatedAt, ResolvedAt)
            SELECT NEWID(), @TenantId, @cmId, @cycleNum, CASE WHEN @cycleNum = 1 THEN 0 ELSE 1 END,
                   NEWID(), mp.Price, mp.Currency, 1, N'seed-retention-pay', @cycleStart, @cycleStart
            FROM retention.membership_plans mp WHERE mp.Id = @planId;
        END
    END;

    SET @i += 1;
END;

PRINT CONCAT('customer_memberships seeded: ', 35);

------------------------------------------------------------------------------
-- Loyalty accounts + ledger (~55 customers)
------------------------------------------------------------------------------
SET @i = 1;
WHILE @i <= 55
BEGIN
    DECLARE @tcLoy UNIQUEIDENTIFIER = (SELECT TenantCustomerId FROM #Cust WHERE rn = @i);
    IF EXISTS (SELECT 1 FROM retention.loyalty_accounts WHERE TenantId = @TenantId AND TenantCustomerId = @tcLoy)
    BEGIN
        SET @i += 1;
        CONTINUE;
    END;

    DECLARE @laId UNIQUEIDENTIFIER = NEWID();
    DECLARE @earned INT = 120 + (@i * 37) % 2800;
    DECLARE @redeemed INT = (@i * 11) % 400;
    DECLARE @balance INT = @earned - @redeemed;
    IF @balance < 0 SET @balance = 0;
    DECLARE @tierId UNIQUEIDENTIFIER = CASE
        WHEN @earned >= 2000 THEN @TierGold
        WHEN @earned >= 500 THEN @TierSilver
        ELSE @TierBronze END;
    DECLARE @loyAt DATETIMEOFFSET(7) = DATEADD(DAY, -((@i * 2) % 90), @Now);

    INSERT retention.loyalty_accounts
        (Id, TenantId, TenantCustomerId, PointsBalance, LifetimeEarned, LifetimeRedeemed, Sequence, CurrentTierId, CreatedAt, UpdatedAt)
    VALUES (@laId, @TenantId, @tcLoy, @balance, @earned, @redeemed, 2, @tierId, @loyAt, @Now);

    INSERT retention.loyalty_transactions
        (Id, TenantId, LoyaltyAccountId, TenantCustomerId, Sequence, Kind, Points, BalanceAfter, Reason, BookingId,
         DiscountAmount, Actor, KeyHash, Fingerprint, OccurredAt)
    VALUES
        (NEWID(), @TenantId, @laId, @tcLoy, 0, 2, @earned, @earned, @Marker, NULL, NULL, @Marker, NULL, NULL, @loyAt),
        (NEWID(), @TenantId, @laId, @tcLoy, 1, 2, -@redeemed, @balance, @Marker, NULL, NULL, @Marker, NULL, NULL, DATEADD(DAY, 1, @loyAt));

    IF @i <= 40
    BEGIN
        INSERT retention.referral_codes (Id, TenantId, TenantCustomerId, Code, CreatedAt)
        VALUES (NEWID(), @TenantId, @tcLoy,
                CONCAT(N'LKSEED', RIGHT(N'000' + CAST(@i AS NVARCHAR(3)), 3)), @loyAt);
    END;

    SET @i += 1;
END;

PRINT CONCAT('loyalty_accounts seeded (up to): ', 55);

------------------------------------------------------------------------------
-- Ensure dev local-customer has rich retention fixtures
------------------------------------------------------------------------------
IF @LocalTc IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM retention.customer_packages WHERE TenantId = @TenantId AND TenantCustomerId = @LocalTc AND Note = @Marker)
    BEGIN
        DECLARE @localCp UNIQUEIDENTIFIER = NEWID();
        DECLARE @localSold DATETIMEOFFSET(7) = DATEADD(DAY, -14, @Now);
        INSERT retention.customer_packages
            (Id, TenantId, TenantCustomerId, PackageDefinitionId, SoldAtLocationId, SoldChannel, Status,
             NameSnapshot, ListPriceSnapshot, CurrencySnapshot, RedemptionScopeSnapshot, Note, SoldBy,
             CreatedAt, ActivatedAt, ExpiresAt)
        SELECT @localCp, @TenantId, @LocalTc, @Pkg5Cut, @PrimaryLoc, 0, 1,
               pd.Name, pd.ListPrice, pd.Currency, 0, @Marker, @OwnerSub, @localSold, @localSold, DATEADD(MONTH, 6, @localSold)
        FROM retention.package_definitions pd WHERE pd.Id = @Pkg5Cut;

        INSERT retention.benefit_balances
            (Id, TenantId, CustomerPackageId, CycleNumber, ServiceOfferingId, ServiceName, Granted, Used, Reserved, Forfeited, Sequence, ExpiresAt, CarriedIn)
        SELECT NEWID(), @TenantId, @localCp, NULL, pb.ServiceOfferingId, pb.ServiceName, pb.QuantityGranted, 1, 0, 0, 0, DATEADD(MONTH, 6, @localSold), 0
        FROM retention.package_benefits pb WHERE pb.PackageDefinitionId = @Pkg5Cut;

        INSERT retention.benefit_transactions
            (Id, TenantId, BenefitBalanceId, Sequence, Kind, Quantity, Delta, ReservedAfter, UsedAfter, RemainingAfter,
             BusinessLocationId, Actor, Reason, OccurredAt)
        SELECT NEWID(), @TenantId, bb.Id, 0, 0, bb.Granted, bb.Granted, 0, bb.Used, bb.Granted - bb.Used,
               @PrimaryLoc, @Marker, @Marker, @localSold
        FROM retention.benefit_balances bb WHERE bb.CustomerPackageId = @localCp;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM retention.customer_memberships
        WHERE TenantId = @TenantId AND TenantCustomerId = @LocalTc AND Status IN (0, 1, 2, 3, 4))
    BEGIN
        DECLARE @localCm UNIQUEIDENTIFIER = NEWID();
        DECLARE @localCycleStart DATETIMEOFFSET(7) = DATEADD(DAY, -10, @Now);
        DECLARE @localCycleEnd DATETIMEOFFSET(7) = DATEADD(MONTH, 1, @localCycleStart);
        INSERT retention.customer_memberships
            (Id, TenantId, TenantCustomerId, MembershipPlanId, EnrolledAtLocationId, EnrolChannel, Status,
             NameSnapshot, PriceSnapshot, CurrencySnapshot, BillingIntervalSnapshot, RolloverPolicySnapshot,
             CancellationPolicySnapshot, AllowFreezeSnapshot, PastDueGraceDaysSnapshot, Note, EnrolledBy,
             CreatedAt, ActivatedAt, CurrentCycleNumber, CurrentCycleStart, CurrentCycleEnd, FailedPaymentCount)
        SELECT @localCm, @TenantId, @LocalTc, @PlanFade, @PrimaryLoc, 0, 1,
               mp.Name, mp.Price, mp.Currency, mp.BillingInterval, mp.RolloverPolicy, mp.CancellationPolicy,
               mp.AllowFreeze, mp.PastDueGraceDays, @Marker, @OwnerSub,
               @localCycleStart, @localCycleStart, 1, @localCycleStart, @localCycleEnd, 0
        FROM retention.membership_plans mp WHERE mp.Id = @PlanFade;

        INSERT retention.customer_membership_benefits
            (Id, TenantId, CustomerMembershipId, ServiceOfferingId, ServiceName, QuantityPerCycle, SortOrder)
        SELECT NEWID(), @TenantId, @localCm, mpb.ServiceOfferingId, mpb.ServiceName, mpb.QuantityPerCycle, mpb.SortOrder
        FROM retention.membership_plan_benefits mpb WHERE mpb.MembershipPlanId = @PlanFade;

        INSERT retention.benefit_balances
            (Id, TenantId, CustomerMembershipId, CycleNumber, ServiceOfferingId, ServiceName, Granted, Used, Reserved, Forfeited, Sequence, ExpiresAt, CarriedIn)
        SELECT NEWID(), @TenantId, @localCm, 1, cmb.ServiceOfferingId, cmb.ServiceName, cmb.QuantityPerCycle, 0, 0, 0, 0, @localCycleEnd, 0
        FROM retention.customer_membership_benefits cmb WHERE cmb.CustomerMembershipId = @localCm;

        INSERT retention.benefit_transactions
            (Id, TenantId, BenefitBalanceId, Sequence, Kind, Quantity, Delta, ReservedAfter, UsedAfter, RemainingAfter,
             BusinessLocationId, Actor, Reason, OccurredAt)
        SELECT NEWID(), @TenantId, bb.Id, 0, 0, bb.Granted, bb.Granted, 0, 0, bb.Granted,
               @PrimaryLoc, @Marker, @Marker, @localCycleStart
        FROM retention.benefit_balances bb WHERE bb.CustomerMembershipId = @localCm;

        INSERT retention.membership_payments
            (Id, TenantId, CustomerMembershipId, CycleNumber, Kind, PaymentIntentId, Amount, Currency, Status, CreatedAt, ResolvedAt)
        SELECT NEWID(), @TenantId, @localCm, 1, 0, NEWID(), mp.Price, mp.Currency, 1, @localCycleStart, @localCycleStart
        FROM retention.membership_plans mp WHERE mp.Id = @PlanFade;
    END;

    IF NOT EXISTS (SELECT 1 FROM retention.loyalty_accounts WHERE TenantId = @TenantId AND TenantCustomerId = @LocalTc)
    BEGIN
        DECLARE @localLa UNIQUEIDENTIFIER = NEWID();
        INSERT retention.loyalty_accounts
            (Id, TenantId, TenantCustomerId, PointsBalance, LifetimeEarned, LifetimeRedeemed, Sequence, CurrentTierId, CreatedAt, UpdatedAt)
        VALUES (@localLa, @TenantId, @LocalTc, 850, 1200, 350, 2, @TierSilver, DATEADD(DAY, -60, @Now), @Now);

        INSERT retention.loyalty_transactions
            (Id, TenantId, LoyaltyAccountId, TenantCustomerId, Sequence, Kind, Points, BalanceAfter, Reason, Actor, OccurredAt)
        VALUES
            (NEWID(), @TenantId, @localLa, @LocalTc, 0, 2, 1200, 1200, @Marker, @Marker, DATEADD(DAY, -60, @Now)),
            (NEWID(), @TenantId, @localLa, @LocalTc, 1, 2, -350, 850, @Marker, @Marker, DATEADD(DAY, -7, @Now));
    END;

    IF NOT EXISTS (SELECT 1 FROM retention.referral_codes WHERE TenantId = @TenantId AND TenantCustomerId = @LocalTc)
        INSERT retention.referral_codes (Id, TenantId, TenantCustomerId, Code, CreatedAt)
        VALUES (NEWID(), @TenantId, @LocalTc, N'LKSEEDLOCAL', @Now);
END
ELSE
    PRINT 'WARNING: auth0|local-customer tenant link not found — customer app retention tabs may be empty.';

SELECT
    (SELECT COUNT(*) FROM retention.package_definitions WHERE TenantId = @TenantId AND Description = @Marker) AS PackageDefinitions,
    (SELECT COUNT(*) FROM retention.customer_packages WHERE TenantId = @TenantId AND Note = @Marker) AS CustomerPackages,
    (SELECT COUNT(*) FROM retention.membership_plans WHERE TenantId = @TenantId AND Description = @Marker) AS MembershipPlans,
    (SELECT COUNT(*) FROM retention.customer_memberships WHERE TenantId = @TenantId AND Note = @Marker) AS CustomerMemberships,
    (SELECT COUNT(*) FROM retention.loyalty_accounts WHERE TenantId = @TenantId) AS LoyaltyAccounts,
    (SELECT COUNT(*) FROM retention.referral_codes WHERE TenantId = @TenantId AND Code LIKE N'LKSEED%') AS ReferralCodes;

PRINT 'Lekki Cuts retention demo seed complete.';
GO
