/*
  One-time: rename SQL schema [payments] → [finance] before dacpac sync.
  Idempotent when [finance] already exists (greenfield or re-run).
*/
IF SCHEMA_ID(N'finance') IS NULL AND SCHEMA_ID(N'payments') IS NOT NULL
BEGIN
    PRINT N'Creating [finance] and transferring objects from [payments]...';
    EXEC(N'CREATE SCHEMA [finance] AUTHORIZATION [dbo];');

    DECLARE @sql nvarchar(max) = N'';
    SELECT @sql = @sql
        + N'ALTER SCHEMA [finance] TRANSFER [payments].' + QUOTENAME(o.name) + N';'
        + CHAR(13)
    FROM sys.objects o
    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
    WHERE s.name = N'payments'
      AND o.type IN (N'U', N'V', N'P', N'FN', N'IF', N'TF');

    IF LEN(@sql) > 0
        EXEC sp_executesql @sql;

    IF NOT EXISTS (
        SELECT 1 FROM sys.objects o
        INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
        WHERE s.name = N'payments')
        DROP SCHEMA [payments];

    PRINT N'[payments] → [finance] transfer complete.';
END
GO
