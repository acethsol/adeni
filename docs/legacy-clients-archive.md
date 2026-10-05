# Legacy Next.js & Expo archive

ADR-012 made **Angular** (`apps/discover`, `apps/portal`, `apps/admin`) the only supported web clients in the main monorepo. **Next.js** and **Expo** were extracted for reference.

| Archive | Location |
|---------|----------|
| **GitHub (recommended)** | Create `acethsol/adeni-legacy-clients` and push from local folder (see below) |
| **Local path (dev machine)** | `C:\DEV\Aceth\adeni-legacy-clients` (git init done; commit `3a00af1`) |

### Push archive to GitHub (one time)

```powershell
cd C:\DEV\Aceth\adeni-legacy-clients
git branch -M main
git remote add origin https://github.com/acethsol/adeni-legacy-clients.git
git push -u origin main
```

Snapshot commit when removed: see archive `README.md`.

## Why remove from main repo?

- Avoids agents and contributors defaulting to `apps/web` / `apps/mobile`
- Keeps `npm install` and CI focused on Angular + .NET
- Preserves BFF and UI patterns for parity checks during migration

## Parity during migration

Use [angular-migration-checklist.md](./angular-migration-checklist.md) against the archive repo or git history — not live paths under `apps/web`.
