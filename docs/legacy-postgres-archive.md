# Legacy PostgreSQL archive

Adeni now uses **SQL Server** (`db/Adeni.Database` + SqlPackage). PostgreSQL EF migrations and related local helpers were extracted for reference.

| Archive | Location |
|---------|----------|
| **GitHub** | https://github.com/acethsol/adeni-legacy-db |
| **Local clone (optional)** | `git clone https://github.com/acethsol/adeni-legacy-db.git` |

## Why remove from main repo?

- Avoids agents reintroducing Npgsql / `MigrateAsync` / Adminer
- Keeps the monorepo focused on the Database Project + SqlPackage workflow
- Preserves migration history for archaeology during the cutover

## Canonical setup

See [database-setup.md](./database-setup.md) and [db/README.md](../db/README.md).
