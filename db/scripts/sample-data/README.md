# Sample data SQL (Development / Staging only)

Idempotent T-SQL run by the API when `SampleData:Enabled` is true. Not used in Production.

Invoked from `DevelopmentDataSeeder` via `SampleDataSqlSeedRunner` after the C# fixture seed. Files are copied to the API publish output (`db/scripts/sample-data/`).

**Cleanup:** set `SampleData:CleanupVolumeDemo` to `true` for one API restart.
