CREATE TABLE [tenancy].[public_booking_links] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [LocationId] UNIQUEIDENTIFIER NOT NULL,
    [Slug] NVARCHAR(64) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_public_booking_links] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_public_booking_links_Location] FOREIGN KEY ([TenantId],[LocationId]) REFERENCES [tenancy].[business_locations] ([TenantId],[Id])
);
GO
CREATE UNIQUE INDEX [IX_public_booking_links_Slug] ON [tenancy].[public_booking_links] ([Slug]);
GO
CREATE UNIQUE INDEX [IX_public_booking_links_TenantId_LocationId] ON [tenancy].[public_booking_links] ([TenantId],[LocationId]);
