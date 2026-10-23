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
UPDATE s
SET s.BusinessLocationId = p.Id
FROM [booking].[staff_members] s
INNER JOIN primary_loc p ON p.TenantId = s.TenantId AND p.rn = 1
WHERE s.BusinessLocationId IS NULL;
GO

PRINT N'staff-location-scope.sql complete.';
GO
