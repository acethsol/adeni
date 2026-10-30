/*
  Pre-deployment — runs before SqlPackage applies the dacpac model.

  During V1 development, prefer editing object scripts under Adeni.Database/
  and publishing to a fresh local database (drop DB + publish-db.ps1 -CreateNewDatabase)
  instead of adding one-off ALTER/DROP scripts here.

  Reserve this file for production upgrades later (data backfills, drops that block
  dacpac compare, renames SqlPackage cannot infer).
*/
PRINT N'Adeni.Database pre-deployment (no-op).';
GO
