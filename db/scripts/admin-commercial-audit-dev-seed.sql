-- Dev-only: commercial audit rows for admin Business → Commercial audit tab.
-- Idempotent on CorrelationId prefix devseed-commercial-audit-

SET NOCOUNT ON;

DECLARE @now DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();

DECLARE @tenant TABLE (RowNum INT NOT NULL, TenantId UNIQUEIDENTIFIER NOT NULL);

INSERT INTO @tenant (RowNum, TenantId)
SELECT TOP (8)
    ROW_NUMBER() OVER (ORDER BY t.[Name]),
    t.[Id]
FROM [tenancy].[tenants] t
ORDER BY t.[Name];

IF NOT EXISTS (SELECT 1 FROM @tenant)
BEGIN
    RAISERROR('No tenants found — create businesses before seeding commercial audit.', 16, 1);
    RETURN;
END

DECLARE @actions TABLE (RowNum INT NOT NULL, Action NVARCHAR(128) NOT NULL);
INSERT INTO @actions (RowNum, Action) VALUES
    (1, N'tenant.commercial_terms_updated'),
    (2, N'tenant.commercial_bound'),
    (3, N'subscription.webhook_applied'),
    (4, N'tenant.commercial_terms_updated');

DECLARE @i INT = 1;
WHILE @i <= 32
BEGIN
    DECLARE @tid UNIQUEIDENTIFIER;
    SELECT @tid = TenantId FROM @tenant WHERE RowNum = ((@i - 1) % (SELECT COUNT(*) FROM @tenant)) + 1;

    DECLARE @action NVARCHAR(128);
    SELECT @action = Action FROM @actions WHERE RowNum = ((@i - 1) % 4) + 1;

    DECLARE @corr NVARCHAR(64) = CONCAT(N'devseed-commercial-audit-', RIGHT(CONCAT(N'000', @i), 3));
    DECLARE @when DATETIMEOFFSET(7) = DATEADD(HOUR, -@i * 6, @now);
    DECLARE @entityId NVARCHAR(64) = LOWER(CONVERT(NVARCHAR(36), @tid));

    IF NOT EXISTS (SELECT 1 FROM [admin].[audit_logs] WHERE [CorrelationId] = @corr)
    BEGIN
        INSERT INTO [admin].[audit_logs] (
            [Id],
            [ActorId],
            [Action],
            [EntityType],
            [EntityId],
            [CorrelationId],
            [OccurredAt],
            [MetadataJson]
        )
        VALUES (
            NEWID(),
            CASE WHEN @i % 3 = 0 THEN N'auth0|dev-admin' ELSE N'auth0|dev-admin-seed' END,
            @action,
            N'tenant',
            @entityId,
            @corr,
            @when,
            N'{"source":"devseed","note":"Sprint 33 commercial audit sample"}'
        );
    END

    SET @i += 1;
END

PRINT CONCAT(N'Commercial audit dev seed complete (up to 32 rows, correlation devseed-commercial-audit-*).');
