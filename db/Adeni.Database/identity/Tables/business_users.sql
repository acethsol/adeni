CREATE TABLE [identity].[business_users] (
    [Id]        UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]  UNIQUEIDENTIFIER   NOT NULL,
    [Auth0Sub]  NVARCHAR (128)     NOT NULL,
    [Role]      NVARCHAR (32)      NOT NULL,
    [CreatedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_business_users] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_business_users_tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_business_users_TenantId]
    ON [identity].[business_users]([TenantId] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_business_users_Auth0Sub]
    ON [identity].[business_users]([Auth0Sub] ASC);


GO

