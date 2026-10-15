CREATE TABLE [booking].[business_closed_dates] (
    [Id]        UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]  UNIQUEIDENTIFIER   NOT NULL,
    [StartDate] DATE               NOT NULL,
    [EndDate]   DATE               NOT NULL,
    [Reason]    NVARCHAR (200)     NULL,
    [CreatedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_business_closed_dates] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CK_business_closed_dates_range] CHECK ([EndDate] >= [StartDate]),
    CONSTRAINT [FK_business_closed_dates_tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_business_closed_dates_TenantId_StartDate_EndDate]
    ON [booking].[business_closed_dates]([TenantId] ASC, [StartDate] ASC, [EndDate] ASC);


GO
