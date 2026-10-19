/*
  Post-deployment — runs after SqlPackage applies the dacpac model.
  Use for grants, reference data, or environment wiring that is not
  represented as model objects. Dev business seed stays in the API
  (DevelopmentDataSeeder); do not put bulk sample tenants here.
*/
:r ..\..\scripts\pricing-catalog-seed-2026-launch-01.sql

PRINT N'Adeni.Database post-deployment complete.';
GO
