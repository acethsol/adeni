-- Dev-only: sample commerce + SaaS rows for admin Finance → Transactions (browse + search "devseed").
-- Portal /plan billing for Lekki Cuts: seeded on API start via DevelopmentDataSeeder (devseed-portal-billing-*).
-- Idempotent on ProviderReference / IdempotencyKey prefixes.

SET NOCOUNT ON;

DECLARE @now DATETIMEOFFSET(7) = SYSDATETOFFSET();
DECLARE @tenant TABLE (RowNum INT NOT NULL, TenantId UNIQUEIDENTIFIER NOT NULL, TenantName NVARCHAR(256) NOT NULL);

INSERT INTO @tenant (RowNum, TenantId, TenantName)
SELECT TOP (5)
    ROW_NUMBER() OVER (ORDER BY t.[Name]),
    t.[Id],
    t.[Name]
FROM [tenancy].[tenants] t
ORDER BY t.[Name];

IF NOT EXISTS (SELECT 1 FROM @tenant)
BEGIN
    RAISERROR('No tenants found — create businesses before seeding transactions.', 16, 1);
    RETURN;
END

DECLARE @tenantCount INT = (SELECT COUNT(*) FROM @tenant);
DECLARE @tid UNIQUEIDENTIFIER;
DECLARE @i INT = 1;
WHILE @i <= 22
BEGIN
    SELECT @tid = TenantId FROM @tenant WHERE RowNum = ((@i - 1) % @tenantCount) + 1;
    DECLARE @ref NVARCHAR(128) = CONCAT(N'devseed_commerce_', RIGHT(CONCAT(N'000', @i), 3));
    DECLARE @idem NVARCHAR(128) = CONCAT(N'devseed-commerce-', @i);
    DECLARE @amount DECIMAL(12, 2) = 3500 + (@i * 125);
    DECLARE @fee DECIMAL(12, 2) = ROUND(@amount * 0.05, 2);
    DECLARE @when DATETIMEOFFSET(7) = DATEADD(DAY, -@i, @now);

    IF NOT EXISTS (SELECT 1 FROM [payments].[payment_intents] WHERE [ProviderReference] = @ref)
    BEGIN
        INSERT INTO [payments].[payment_intents] (
            [Id], [TenantId], [BookingId], [Type], [Amount], [PlatformFeeAmount], [Currency],
            [Status], [ProviderReference], [Description], [IdempotencyKey], [CreatedAt], [UpdatedAt]
        )
        VALUES (
            NEWID(),
            @tid,
            NULL,
            1,
            @amount,
            @fee,
            N'NGN',
            1,
            @ref,
            N'Dev seed commerce checkout',
            @idem,
            @when,
            @when
        );
    END

    SET @i += 1;
END

SET @i = 1;
WHILE @i <= 14
BEGIN
    SELECT @tid = TenantId FROM @tenant WHERE RowNum = ((@i - 1) % @tenantCount) + 1;
    DECLARE @saasRef NVARCHAR(128) = CONCAT(N'devseed_saas_', RIGHT(CONCAT(N'000', @i), 3));
    DECLARE @saasIdem NVARCHAR(256) = CONCAT(N'devseed-saas-', @i);
    SET @when = DATEADD(DAY, -@i, @now);
    DECLARE @minor BIGINT = 1500000 + (@i * 50000);

    IF NOT EXISTS (SELECT 1 FROM [subscriptions].[tenant_subscription_charges] WHERE [ProviderReference] = @saasRef)
    BEGIN
        INSERT INTO [subscriptions].[tenant_subscription_charges] (
            [Id], [TenantId], [IdempotencyKey], [ProviderReference], [EventType], [SubscriptionSku],
            [AmountMinor], [Currency], [Status], [OccurredAt], [CreatedAt]
        )
        VALUES (
            NEWID(),
            @tid,
            @saasIdem,
            @saasRef,
            N'subscription.charge.success',
            CASE WHEN @i % 3 = 0 THEN 2 WHEN @i % 2 = 0 THEN 1 ELSE 0 END,
            @minor,
            N'NGN',
            N'success',
            @when,
            @when
        );
    END

    SET @i += 1;
END

PRINT CONCAT(N'devseed commerce + SaaS rows ensured (search admin Transactions for "devseed").');
