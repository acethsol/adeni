-- Sprint 33: Adeni SaaS subscription charge snapshots (idempotent webhook sync)

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'subscriptions')
BEGIN
    EXEC(N'CREATE SCHEMA [subscriptions]');
END
GO

IF OBJECT_ID(N'[subscriptions].[tenant_subscription_charges]', N'U') IS NULL
BEGIN
    CREATE TABLE [subscriptions].[tenant_subscription_charges] (
        [Id]                 UNIQUEIDENTIFIER   NOT NULL,
        [TenantId]           UNIQUEIDENTIFIER   NOT NULL,
        [IdempotencyKey]     NVARCHAR (256)     NOT NULL,
        [ProviderReference]  NVARCHAR (128)     NOT NULL,
        [EventType]          NVARCHAR (64)      NOT NULL,
        [SubscriptionSku]    INT                NULL,
        [AmountMinor]        BIGINT             NULL,
        [Currency]           NVARCHAR (3)         NULL,
        [Status]             NVARCHAR (32)      NOT NULL,
        [OccurredAt]         DATETIMEOFFSET (7) NOT NULL,
        [CreatedAt]          DATETIMEOFFSET (7) NOT NULL,
        CONSTRAINT [PK_tenant_subscription_charges] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_tenant_subscription_charges_IdempotencyKey'
      AND object_id = OBJECT_ID(N'[subscriptions].[tenant_subscription_charges]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_tenant_subscription_charges_IdempotencyKey]
        ON [subscriptions].[tenant_subscription_charges]([IdempotencyKey] ASC);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_tenant_subscription_charges_TenantId_OccurredAt'
      AND object_id = OBJECT_ID(N'[subscriptions].[tenant_subscription_charges]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_tenant_subscription_charges_TenantId_OccurredAt]
        ON [subscriptions].[tenant_subscription_charges]([TenantId] ASC, [OccurredAt] DESC);
END
GO
