CREATE TABLE [intake].[booking_assignments] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [BookingId] UNIQUEIDENTIFIER NOT NULL,
    [TemplateId] UNIQUEIDENTIFIER NOT NULL,
    [FormVersionId] UNIQUEIDENTIFIER NOT NULL,
    [AssignedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_booking_assignments] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_booking_assignments_0] UNIQUE ([TenantId], [Id], [FormVersionId]),
    CONSTRAINT [AK_booking_assignments_1] UNIQUE ([TenantId], [BookingId], [TemplateId]),
    CONSTRAINT [FK_booking_assignments_0] FOREIGN KEY ([TenantId], [TemplateId], [FormVersionId]) REFERENCES [intake].[form_versions] ([TenantId], [TemplateId], [Id]),
    CONSTRAINT [FK_booking_assignments_1] FOREIGN KEY ([TenantId], [BookingId]) REFERENCES [booking].[bookings] ([TenantId], [Id])
);
