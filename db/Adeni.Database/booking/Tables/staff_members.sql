CREATE TABLE [booking].[staff_members] (
    [Id]             UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]       UNIQUEIDENTIFIER   NOT NULL,
    [FirstName]      NVARCHAR (80)      NOT NULL,
    [LastName]       NVARCHAR (80)      NOT NULL,
    [DisplayName]    NVARCHAR (120)     NOT NULL,
    [RoleKey]        NVARCHAR (40)      DEFAULT (N'other') NOT NULL,
    [Title]          NVARCHAR (120)     NULL,
    [Bio]            NVARCHAR (500)     NULL,
    [IsActive]       BIT                NOT NULL,
    [SortOrder]      INT                NOT NULL,
    [AvatarImageKey] NVARCHAR (512)     NULL,
    [CreatedAt]      DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]      DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_staff_members] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE NONCLUSTERED INDEX [IX_staff_members_TenantId_IsActive_SortOrder]
    ON [booking].[staff_members]([TenantId] ASC, [IsActive] ASC, [SortOrder] ASC);


GO

