CREATE TABLE [booking].[booking_lines] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [BookingId]         UNIQUEIDENTIFIER NOT NULL,
    [TenantId]          UNIQUEIDENTIFIER NOT NULL,
    [ServiceOfferingId] UNIQUEIDENTIFIER NOT NULL,
    [SortOrder]         INT              NOT NULL,
    [PriceAmount]       DECIMAL (12, 2)  NOT NULL,
    [DurationMinutes]   INT              NOT NULL,
    [ServiceName]       NVARCHAR (200)   NOT NULL,
    [IsAddOn]           BIT              NOT NULL,
    CONSTRAINT [PK_booking_lines] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_booking_lines_bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [booking].[bookings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_booking_lines_service_offerings_ServiceOfferingId] FOREIGN KEY ([ServiceOfferingId]) REFERENCES [booking].[service_offerings] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_booking_lines_BookingId_SortOrder]
    ON [booking].[booking_lines]([BookingId] ASC, [SortOrder] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_booking_lines_ServiceOfferingId]
    ON [booking].[booking_lines]([ServiceOfferingId] ASC);


GO

