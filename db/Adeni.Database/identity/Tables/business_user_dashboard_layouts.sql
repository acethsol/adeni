CREATE TABLE [identity].[business_user_dashboard_layouts] (
    [BusinessUserId] UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]       UNIQUEIDENTIFIER   NOT NULL,
    [LayoutJson]     NVARCHAR (MAX)     NOT NULL,
    [UpdatedAt]      DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_business_user_dashboard_layouts] PRIMARY KEY CLUSTERED ([BusinessUserId] ASC),
    CONSTRAINT [FK_business_user_dashboard_layouts_business_users_BusinessUserId] FOREIGN KEY ([BusinessUserId]) REFERENCES [identity].[business_users] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_business_user_dashboard_layouts_tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_business_user_dashboard_layouts_TenantId]
    ON [identity].[business_user_dashboard_layouts]([TenantId] ASC);


GO
