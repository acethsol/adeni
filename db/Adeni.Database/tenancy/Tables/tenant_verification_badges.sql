CREATE TABLE [tenancy].[tenant_verification_badges] (
    [Id]               UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]         UNIQUEIDENTIFIER   NOT NULL,
    [BadgeType]        INT                NOT NULL,
    [Status]           INT                NOT NULL,
    [ReferenceNumber]  NVARCHAR (128)     NULL,
    [RequestedAt]      DATETIMEOFFSET (7) NOT NULL,
    [GrantedAt]        DATETIMEOFFSET (7) NULL,
    [GrantedByAdminId] NVARCHAR (128)     NULL,
    CONSTRAINT [PK_tenant_verification_badges] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_tenant_verification_badges_TenantId_BadgeType]
    ON [tenancy].[tenant_verification_badges]([TenantId] ASC, [BadgeType] ASC);


GO

