/*
  Pre-deployment — runs before SqlPackage applies the dacpac model.
  Use for one-off cleanup that must happen before schema sync
  (e.g. drop objects that block renames). Keep empty unless needed.
*/
PRINT N'Adeni.Database pre-deployment starting...';
GO

/* payments → finance schema rename runs in scripts/publish-db.ps1 before SqlPackage analyze. */

/* Sprint 36: drop Sprint 18 messaging tables (no data migration). */
IF OBJECT_ID(N'[messaging].[messages]', N'U') IS NOT NULL
BEGIN
    PRINT N'Dropping Sprint 18 [messaging].[messages]...';
    DROP TABLE [messaging].[messages];
END
GO

IF OBJECT_ID(N'[messaging].[message_threads]', N'U') IS NOT NULL
BEGIN
    PRINT N'Dropping Sprint 18 [messaging].[message_threads]...';
    DROP TABLE [messaging].[message_threads];
END
GO

/* Monetization: drop legacy SubscriptionTier (values matched SubscriptionSku 0/1/2). */
IF COL_LENGTH(N'tenancy.tenants', N'SubscriptionTier') IS NOT NULL
BEGIN
    PRINT N'Aligning tenancy.tenants.SubscriptionSku from legacy SubscriptionTier...';
    EXEC(N'
        UPDATE [tenancy].[tenants]
        SET [SubscriptionSku] = [SubscriptionTier]
        WHERE [SubscriptionSku] <> [SubscriptionTier];
    ');

    PRINT N'Dropping tenancy.tenants.SubscriptionTier...';
    ALTER TABLE [tenancy].[tenants] DROP COLUMN [SubscriptionTier];
END
GO
