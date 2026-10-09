/*
  Pre-deployment — runs before SqlPackage applies the dacpac model.
  Use for one-off cleanup that must happen before schema sync
  (e.g. drop objects that block renames). Keep empty unless needed.
*/
PRINT N'Adeni.Database pre-deployment starting...';
GO
