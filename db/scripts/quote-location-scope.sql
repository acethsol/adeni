/*
  Sprint: portal active location scope — quotes
  Adds BusinessLocationId to quote_requests and backfills primary branch.
  Run on staging/prod after dacpac deploy when column is not yet in the model.
*/

IF COL_LENGTH('booking.quote_requests', 'BusinessLocationId') IS NULL
BEGIN
    ALTER TABLE [booking].[quote_requests]
        ADD [BusinessLocationId] UNIQUEIDENTIFIER NULL;

    ALTER TABLE [booking].[quote_requests]
        WITH CHECK ADD CONSTRAINT [FK_quote_requests_business_locations_BusinessLocationId]
        FOREIGN KEY ([BusinessLocationId]) REFERENCES [tenancy].[business_locations] ([Id]);

    CREATE NONCLUSTERED INDEX [IX_quote_requests_TenantId_BusinessLocationId_CreatedAt]
        ON [booking].[quote_requests]([TenantId] ASC, [BusinessLocationId] ASC, [CreatedAt] ASC);
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
UPDATE q
SET q.BusinessLocationId = p.Id
FROM [booking].[quote_requests] q
INNER JOIN primary_loc p ON p.TenantId = q.TenantId AND p.rn = 1
WHERE q.BusinessLocationId IS NULL;
GO

PRINT N'quote-location-scope.sql complete.';
GO
