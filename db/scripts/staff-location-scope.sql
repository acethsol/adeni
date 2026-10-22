/*
  Adds BusinessLocationId to staff_members (optional branch assignment).
*/
IF COL_LENGTH('booking.staff_members', 'BusinessLocationId') IS NULL
BEGIN
    ALTER TABLE [booking].[staff_members]
        ADD [BusinessLocationId] UNIQUEIDENTIFIER NULL;

    ALTER TABLE [booking].[staff_members]
        WITH CHECK ADD CONSTRAINT [FK_staff_members_business_locations_BusinessLocationId]
        FOREIGN KEY ([BusinessLocationId]) REFERENCES [tenancy].[business_locations] ([Id]);
END
GO
