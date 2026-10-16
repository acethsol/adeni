-- Sprint 35: tenant customer roster + queue ticket link (run on existing dev/staging DBs)
-- Filtered indexes require QUOTED_IDENTIFIER ON for the session.
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[tenancy].[tenant_guests]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[tenancy].[tenant_customers]', N'U') IS NULL
BEGIN
    EXEC sp_rename N'tenancy.tenant_guests', N'tenant_customers';
END
GO

IF OBJECT_ID(N'[tenancy].[tenant_customers]', N'U') IS NULL
BEGIN
    CREATE TABLE [tenancy].[tenant_customers] (
        [Id]               UNIQUEIDENTIFIER   NOT NULL,
        [TenantId]         UNIQUEIDENTIFIER   NOT NULL,
        [CustomerId]       UNIQUEIDENTIFIER   NULL,
        [NormalizedPhone]  NVARCHAR (40)      NULL,
        [DisplayName]      NVARCHAR (200)     NOT NULL,
        [FirstName]        NVARCHAR (100)     NULL,
        [LastName]         NVARCHAR (100)     NULL,
        [Email]            NVARCHAR (320)     NULL,
        [CreatedAt]        DATETIMEOFFSET (7) NOT NULL,
        [UpdatedAt]        DATETIMEOFFSET (7) NOT NULL,
        [LastSeenAt]       DATETIMEOFFSET (7) NOT NULL,
        [LastBookingAt]    DATETIMEOFFSET (7) NULL,
        [LastQueueJoinAt]  DATETIMEOFFSET (7) NULL,
        CONSTRAINT [PK_tenant_customers] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF COL_LENGTH(N'booking.queue_tickets', N'TenantGuestId') IS NOT NULL
   AND COL_LENGTH(N'booking.queue_tickets', N'TenantCustomerId') IS NULL
BEGIN
    EXEC sp_rename N'booking.queue_tickets.TenantGuestId', N'TenantCustomerId', N'COLUMN';
END
GO

IF COL_LENGTH(N'booking.queue_tickets', N'TenantCustomerId') IS NULL
BEGIN
    ALTER TABLE [booking].[queue_tickets]
        ADD [TenantCustomerId] UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_tenant_customers_TenantId_CustomerId'
      AND object_id = OBJECT_ID(N'[tenancy].[tenant_customers]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_tenant_customers_TenantId_CustomerId]
        ON [tenancy].[tenant_customers]([TenantId] ASC, [CustomerId] ASC)
        WHERE [CustomerId] IS NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_tenant_customers_TenantId_NormalizedPhone'
      AND object_id = OBJECT_ID(N'[tenancy].[tenant_customers]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_tenant_customers_TenantId_NormalizedPhone]
        ON [tenancy].[tenant_customers]([TenantId] ASC, [NormalizedPhone] ASC)
        WHERE [NormalizedPhone] IS NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_tenant_customers_TenantId_LastSeenAt'
      AND object_id = OBJECT_ID(N'[tenancy].[tenant_customers]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_tenant_customers_TenantId_LastSeenAt]
        ON [tenancy].[tenant_customers]([TenantId] ASC, [LastSeenAt] DESC);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_queue_tickets_TenantId_TenantCustomerId'
      AND object_id = OBJECT_ID(N'[booking].[queue_tickets]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_queue_tickets_TenantId_TenantCustomerId]
        ON [booking].[queue_tickets]([TenantId] ASC, [TenantCustomerId] ASC);
END
GO
