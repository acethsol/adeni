/*
  Per-branch weekly hours and closed dates (portal active location scope).
  Duplicates legacy tenant-wide rows onto every active branch, then requires BusinessLocationId.
*/

IF COL_LENGTH('booking.weekly_availability', 'BusinessLocationId') IS NULL
BEGIN
    ALTER TABLE [booking].[weekly_availability]
        ADD [BusinessLocationId] UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH('booking.business_closed_dates', 'BusinessLocationId') IS NULL
BEGIN
    ALTER TABLE [booking].[business_closed_dates]
        ADD [BusinessLocationId] UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH('booking.weekly_availability', 'BusinessLocationId') IS NOT NULL
   AND EXISTS (
       SELECT 1
       FROM [booking].[weekly_availability]
       WHERE [BusinessLocationId] IS NULL)
BEGIN
    INSERT INTO [booking].[weekly_availability] (
        [Id],
        [TenantId],
        [BusinessLocationId],
        [DayOfWeek],
        [OpenTime],
        [CloseTime])
    SELECT
        NEWID(),
        wa.[TenantId],
        bl.[Id],
        wa.[DayOfWeek],
        wa.[OpenTime],
        wa.[CloseTime]
    FROM [booking].[weekly_availability] wa
    INNER JOIN [tenancy].[business_locations] bl
        ON bl.[TenantId] = wa.[TenantId] AND bl.[IsActive] = 1
    WHERE wa.[BusinessLocationId] IS NULL;

    DELETE FROM [booking].[weekly_availability]
    WHERE [BusinessLocationId] IS NULL;
END
GO

IF COL_LENGTH('booking.business_closed_dates', 'BusinessLocationId') IS NOT NULL
   AND EXISTS (
       SELECT 1
       FROM [booking].[business_closed_dates]
       WHERE [BusinessLocationId] IS NULL)
BEGIN
    INSERT INTO [booking].[business_closed_dates] (
        [Id],
        [TenantId],
        [BusinessLocationId],
        [StartDate],
        [EndDate],
        [Reason],
        [CreatedAt])
    SELECT
        NEWID(),
        cd.[TenantId],
        bl.[Id],
        cd.[StartDate],
        cd.[EndDate],
        cd.[Reason],
        cd.[CreatedAt]
    FROM [booking].[business_closed_dates] cd
    INNER JOIN [tenancy].[business_locations] bl
        ON bl.[TenantId] = cd.[TenantId] AND bl.[IsActive] = 1
    WHERE cd.[BusinessLocationId] IS NULL;

    DELETE FROM [booking].[business_closed_dates]
    WHERE [BusinessLocationId] IS NULL;
END
GO

IF COL_LENGTH('booking.weekly_availability', 'BusinessLocationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = N'FK_weekly_availability_business_locations_BusinessLocationId')
BEGIN
    ALTER TABLE [booking].[weekly_availability]
        WITH CHECK ADD CONSTRAINT [FK_weekly_availability_business_locations_BusinessLocationId]
        FOREIGN KEY ([BusinessLocationId]) REFERENCES [tenancy].[business_locations] ([Id]);
END
GO

IF COL_LENGTH('booking.business_closed_dates', 'BusinessLocationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = N'FK_business_closed_dates_business_locations_BusinessLocationId')
BEGIN
    ALTER TABLE [booking].[business_closed_dates]
        WITH CHECK ADD CONSTRAINT [FK_business_closed_dates_business_locations_BusinessLocationId]
        FOREIGN KEY ([BusinessLocationId]) REFERENCES [tenancy].[business_locations] ([Id]);
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_weekly_availability_TenantId_DayOfWeek'
      AND object_id = OBJECT_ID(N'booking.weekly_availability'))
BEGIN
    DROP INDEX [IX_weekly_availability_TenantId_DayOfWeek] ON [booking].[weekly_availability];
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_business_closed_dates_TenantId_StartDate_EndDate'
      AND object_id = OBJECT_ID(N'booking.business_closed_dates'))
BEGIN
    DROP INDEX [IX_business_closed_dates_TenantId_StartDate_EndDate] ON [booking].[business_closed_dates];
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_weekly_availability_TenantId_BusinessLocationId_DayOfWeek'
      AND object_id = OBJECT_ID(N'booking.weekly_availability'))
BEGIN
    DROP INDEX [IX_weekly_availability_TenantId_BusinessLocationId_DayOfWeek] ON [booking].[weekly_availability];
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_business_closed_dates_TenantId_BusinessLocationId_StartDate_EndDate'
      AND object_id = OBJECT_ID(N'booking.business_closed_dates'))
BEGIN
    DROP INDEX [IX_business_closed_dates_TenantId_BusinessLocationId_StartDate_EndDate] ON [booking].[business_closed_dates];
END
GO

IF COL_LENGTH('booking.weekly_availability', 'BusinessLocationId') IS NOT NULL
BEGIN
    ALTER TABLE [booking].[weekly_availability]
        ALTER COLUMN [BusinessLocationId] UNIQUEIDENTIFIER NOT NULL;
END
GO

IF COL_LENGTH('booking.business_closed_dates', 'BusinessLocationId') IS NOT NULL
BEGIN
    ALTER TABLE [booking].[business_closed_dates]
        ALTER COLUMN [BusinessLocationId] UNIQUEIDENTIFIER NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_weekly_availability_TenantId_BusinessLocationId_DayOfWeek'
      AND object_id = OBJECT_ID(N'booking.weekly_availability'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_weekly_availability_TenantId_BusinessLocationId_DayOfWeek]
        ON [booking].[weekly_availability]([TenantId] ASC, [BusinessLocationId] ASC, [DayOfWeek] ASC);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_business_closed_dates_TenantId_BusinessLocationId_StartDate_EndDate'
      AND object_id = OBJECT_ID(N'booking.business_closed_dates'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_business_closed_dates_TenantId_BusinessLocationId_StartDate_EndDate]
        ON [booking].[business_closed_dates]([TenantId] ASC, [BusinessLocationId] ASC, [StartDate] ASC, [EndDate] ASC);
END
GO

PRINT N'availability-location-scope.sql complete.';
GO
