/*
  Pre-deployment — runs before SqlPackage applies the dacpac model.
  Use for one-off cleanup that must happen before schema sync
  (e.g. drop objects that block renames). Keep empty unless needed.
*/
PRINT N'Adeni.Database pre-deployment starting...';
GO

/* Sprint 36: drop Sprint 18 messaging tables (no data migration). */
IF OBJECT_ID(N'[messaging].[messages]', N'U') IS NOT NULL
BEGIN
    PRINT N'Dropping Sprint 18 [messaging].[messages]...';
    DROP TABLE [messaging].[messages];
END
GO

IF OBJECT_ID(N'[messaging].[message_threads]', N'U') IS NOT NULL
BEGIN
    PRINT N'Dropping Sprint 18 [messaging].[message_threads]...';
    DROP TABLE [messaging].[message_threads];
END
GO
