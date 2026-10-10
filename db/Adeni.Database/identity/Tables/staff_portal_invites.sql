CREATE TABLE [identity].[staff_portal_invites] (
    [Id]                UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]          UNIQUEIDENTIFIER   NOT NULL,
    [Email]             NVARCHAR (320)     NOT NULL,
    [PermissionRole]    NVARCHAR (32)      NOT NULL,
    [StaffMemberId]     UNIQUEIDENTIFIER   NULL,
    [TokenHash]         NVARCHAR (64)      NOT NULL,
    [Status]            NVARCHAR (32)      NOT NULL,
    [ExpiresAt]         DATETIMEOFFSET (7) NOT NULL,
    [CreatedAt]         DATETIMEOFFSET (7) NOT NULL,
    [AcceptedAt]        DATETIMEOFFSET (7) NULL,
    [InvitedByAuth0Sub] NVARCHAR (128)     NULL,
    CONSTRAINT [PK_staff_portal_invites] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_staff_portal_invites_tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]) ON DELETE CASCADE
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_staff_portal_invites_TokenHash]
    ON [identity].[staff_portal_invites]([TokenHash] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_staff_portal_invites_TenantId_Status]
    ON [identity].[staff_portal_invites]([TenantId] ASC, [Status] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_staff_portal_invites_TenantId_Email]
    ON [identity].[staff_portal_invites]([TenantId] ASC, [Email] ASC);

