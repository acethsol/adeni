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


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_tenant_customers_TenantId_CustomerId]
    ON [tenancy].[tenant_customers]([TenantId] ASC, [CustomerId] ASC)
    WHERE [CustomerId] IS NOT NULL;


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_tenant_customers_TenantId_NormalizedPhone]
    ON [tenancy].[tenant_customers]([TenantId] ASC, [NormalizedPhone] ASC)
    WHERE [NormalizedPhone] IS NOT NULL;


GO

CREATE NONCLUSTERED INDEX [IX_tenant_customers_TenantId_LastSeenAt]
    ON [tenancy].[tenant_customers]([TenantId] ASC, [LastSeenAt] DESC);


GO
