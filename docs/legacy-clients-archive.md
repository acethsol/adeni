# Legacy Next.js & Expo archive

ADR-012 made **Angular** (`apps/discover`, `apps/portal`, `apps/admin`) the only supported web clients in the main monorepo. **Next.js** and **Expo** were extracted for reference.

| Archive | Location |
|---------|----------|
| **GitHub** | https://github.com/acethsol/adeni-legacy-clients |
| **Local clone (optional)** | `git clone https://github.com/acethsol/adeni-legacy-clients.git` |

Snapshot commit when removed: see archive repo `README.md` (from `adeni` @ `d3b18d9`).

## Why remove from main repo?

- Avoids agents and contributors defaulting to `apps/web` / `apps/mobile`
- Keeps `npm install` and CI focused on Angular + .NET
- Preserves BFF and UI patterns for parity checks during migration

## Parity during migration

Use [angular-migration-checklist.md](./angular-migration-checklist.md) against the archive repo or git history — not live paths under `apps/web`.
