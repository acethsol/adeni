# Adeni.Database (SQL Server Database Project)

Schema is owned by **`Adeni.Database/`** — a [Microsoft.Build.Sql](https://learn.microsoft.com/sql/tools/sql-database-projects/sql-database-projects) project. EF Core maps entities only; do **not** add EF migrations.

| Path | Role |
|------|------|
| `Adeni.Database/**/*.sql` (tables, schemas) | Declarative model → `.dacpac` |
| `Adeni.Database/PreDeployment/` | Empty in V1 dev — edit table scripts instead of one-off ALTER scripts |
| `Adeni.Database/PostDeployment/` | Reference data after schema sync (pricing catalog) |
| `../scripts/publish-db.ps1` | Build + SqlPackage publish |
| `scripts/pricing-catalog-seed-2026-launch-01.sql` | SaaS pricing rows (post-deploy, all environments) |
| `scripts/sample-data/*.sql` | Dev/staging grid + admin browse fixtures (API seed only) |

## Local publish

```powershell
docker compose up -d
./scripts/publish-db.ps1 -CreateNewDatabase
dotnet run --project src/Adeni.Api --launch-profile http   # sample data (Development)
```

SqlPackage is installed as a local .NET tool (`dotnet-tools.json`).

## Changing schema (development)

1. Edit object scripts under `Adeni.Database/`
2. Align EF configuration if the contract changed
3. Publish incrementally, or drop `adeni` and `./scripts/publish-db.ps1 -CreateNewDatabase` for a clean slate

Do **not** add incremental patch scripts under `db/scripts/` — put the final shape in the dacpac.

## Visual Studio: “Target framework not installed”

`Adeni.Database` is an **SDK-style** SQL project (`Microsoft.Build.Sql`). Classic SSDT treats `.sqlproj` as .NET Framework and shows that dialog.

**Do not** choose “Update the target to .NET Framework 4.8” — that breaks CLI/`publish-db.ps1` builds.

| Option | What to do |
|--------|------------|
| **Quick** | Choose **Do not load this project**. Edit `.sql` in the repo; build/publish with `./scripts/publish-db.ps1` or `dotnet build db/Adeni.Database`. |
| **Full VS support** | VS 2022 **17.12+**, install workload component **SQL Server Data Tools SDK-style (Preview)** *and* **.NET SDK**. |
| **Editor** | [SQL Database Projects](https://marketplace.visualstudio.com/items?itemName=ms-mssql.sql-database-projects-vscode) in VS Code / Cursor. |

## Seeding

| Layer | When | Environments |
|-------|------|--------------|
| **PostDeployment** | Every publish | All — pricing catalog SQL |
| **`DevelopmentDataSeeder`** | API startup when `SampleData:Enabled` | Development + Staging (default on) |
| **`scripts/sample-data/*.sql`** | End of `DevelopmentDataSeeder` | Same — Lekki grid volume, admin transactions/audit browse fixtures |

**Reset demo volume without dropping the DB:** set `"SampleData": { "CleanupVolumeDemo": true }` in appsettings, restart API once (runs cleanup SQL then re-seeds), then set it back to `false`.

**Production:** `SampleData:Enabled` false (default when environment is not Development/Staging).

See [database-setup.md](../docs/database-setup.md) for Auth0 test users and Lekki markers.
