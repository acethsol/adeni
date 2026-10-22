/*
  Sprint: portal active location scope (P1)
  Adds BusinessLocationId to bookings + queue_tickets and backfills primary branch.
  Run on staging/prod after dacpac deploy when columns are not yet in the model.
*/

IF COL_LENGTH('booking.bookings', 'BusinessLocationId') IS NULL
BEGIN
    ALTER TABLE [booking].[bookings]
        ADD [BusinessLocationId] UNIQUEIDENTIFIER NULL;

    ALTER TABLE [booking].[bookings]
        WITH CHECK ADD CONSTRAINT [FK_bookings_business_locations_BusinessLocationId]
        FOREIGN KEY ([BusinessLocationId]) REFERENCES [tenancy].[business_locations] ([Id]);

    CREATE NONCLUSTERED INDEX [IX_bookings_TenantId_BusinessLocationId_StartAt]
        ON [booking].[bookings]([TenantId] ASC, [BusinessLocationId] ASC, [StartAt] ASC);
END
GO

IF COL_LENGTH('booking.queue_tickets', 'BusinessLocationId') IS NULL
BEGIN
    ALTER TABLE [booking].[queue_tickets]
        ADD [BusinessLocationId] UNIQUEIDENTIFIER NULL;

    ALTER TABLE [booking].[queue_tickets]
        WITH CHECK ADD CONSTRAINT [FK_queue_tickets_business_locations_BusinessLocationId]
        FOREIGN KEY ([BusinessLocationId]) REFERENCES [tenancy].[business_locations] ([Id]);
END
GO

;WITH primary_loc AS (
    SELECT
        bl.TenantId,
        bl.Id,
        ROW_NUMBER() OVER (
            PARTITION BY bl.TenantId
            ORDER BY bl.IsPrimary DESC, bl.CreatedAt ASC) AS rn
    FROM [tenancy].[business_locations] bl
    WHERE bl.IsActive = 1
)
UPDATE b
SET b.BusinessLocationId = p.Id
FROM [booking].[bookings] b
INNER JOIN primary_loc p ON p.TenantId = b.TenantId AND p.rn = 1
WHERE b.BusinessLocationId IS NULL;
GO

;WITH primary_loc AS (
    SELECT
        bl.TenantId,
        bl.Id,
        ROW_NUMBER() OVER (
            PARTITION BY bl.TenantId
            ORDER BY bl.IsPrimary DESC, bl.CreatedAt ASC) AS rn
    FROM [tenancy].[business_locations] bl
    WHERE bl.IsActive = 1
)
UPDATE q
SET q.BusinessLocationId = p.Id
FROM [booking].[queue_tickets] q
INNER JOIN primary_loc p ON p.TenantId = q.TenantId AND p.rn = 1
WHERE q.BusinessLocationId IS NULL;
GO

PRINT N'booking-location-scope.sql complete.';
GO
