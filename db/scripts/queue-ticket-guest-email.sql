-- Idempotent: guest email on walk-in queue tickets
IF COL_LENGTH('booking.queue_tickets', 'GuestEmail') IS NULL
BEGIN
    ALTER TABLE [booking].[queue_tickets]
    ADD [GuestEmail] NVARCHAR (320) NULL;
END
GO
