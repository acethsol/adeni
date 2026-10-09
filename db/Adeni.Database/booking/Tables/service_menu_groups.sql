CREATE TABLE [booking].[service_menu_groups] (
    [Id]        UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]  UNIQUEIDENTIFIER   NOT NULL,
    [Name]      NVARCHAR (120)     NOT NULL,
    [SortOrder] INT                NOT NULL,
    [CreatedAt] DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_service_menu_groups] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE NONCLUSTERED INDEX [IX_service_menu_groups_TenantId_SortOrder]
    ON [booking].[service_menu_groups]([TenantId] ASC, [SortOrder] ASC);


GO

