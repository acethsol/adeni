# Sample data SQL (Development / Staging only)

Idempotent T-SQL run by the API when `SampleData:Enabled` is true. Not used in Production.

Invoked from `DevelopmentDataSeeder` via `SampleDataSqlSeedRunner` after the C# fixture seed. Files are copied to the API publish output (`db/scripts/sample-data/`).

**Cleanup:** set `SampleData:CleanupVolumeDemo` to `true` for one API restart.

| Script | Purpose |
|--------|---------|
| `seed-lekki-cuts-demo.sql` | Staff, customers, bookings, messages, queue volume |
| `seed-lekki-cuts-retention.sql` | Packages, memberships, loyalty + referral catalog and sold/enrolled volume for Lekki Cuts |
| `seed-lekki-cuts-intake-consultation.sql` | Intake / consultation fixtures |

**Customer app retention:** sign in as `auth0|local-customer` after seed (packages, memberships, loyalty at Lekki Cuts). **Business portal:** `auth0|local-business` → Account → Retention.
