CREATE TABLE [booking].[service_offerings] (
    [Id]                  UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]            UNIQUEIDENTIFIER   NOT NULL,
    [Name]                NVARCHAR (200)     NOT NULL,
    [Description]         NVARCHAR (2000)    NULL,
    [PriceAmount]         DECIMAL (12, 2)    NOT NULL,
    [Currency]            NVARCHAR (3)       NOT NULL,
    [PricingType]         INT                NOT NULL,
    [DurationMinutes]     INT                NOT NULL,
    [CategorySlug]        NVARCHAR (64)      NULL,
    [CatalogServiceId]    NVARCHAR (64)      NULL,
    [BookingDeliveryType] INT                NOT NULL,
    [MenuGroupId]         UNIQUEIDENTIFIER   NULL,
    [SortOrder]           INT                DEFAULT ((0)) NOT NULL,
    [IsAddOn]             BIT                DEFAULT (CONVERT([bit],(0))) NOT NULL,
    [IsActive]            BIT                NOT NULL,
    [CreatedAt]           DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]           DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_service_offerings] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_service_offerings_service_menu_groups_MenuGroupId] FOREIGN KEY ([MenuGroupId]) REFERENCES [booking].[service_menu_groups] ([Id]) ON DELETE SET NULL
);


GO

CREATE NONCLUSTERED INDEX [IX_service_offerings_TenantId_IsActive]
    ON [booking].[service_offerings]([TenantId] ASC, [IsActive] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_service_offerings_TenantId_MenuGroupId_SortOrder]
    ON [booking].[service_offerings]([TenantId] ASC, [MenuGroupId] ASC, [SortOrder] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_service_offerings_MenuGroupId]
    ON [booking].[service_offerings]([MenuGroupId] ASC);


GO

