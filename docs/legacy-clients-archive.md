# Legacy Next.js & Expo archive

ADR-012 made **Angular** (`apps/customer`, `apps/business`, `apps/admin`) the only supported web clients in the main monorepo. **Next.js** and **Expo** were extracted for reference.

| Archive | Location |
|---------|----------|
| **GitHub** | https://github.com/acethsol/adeni-legacy-clients |
| **Local clone (optional)** | `git clone https://github.com/acethsol/adeni-legacy-clients.git` |

Snapshot commit when removed: see archive repo `README.md` (from `adeni` @ `d3b18d9`).

## Why remove from main repo?

- Prevents new work on the archived `apps/web` (Next.js) and former `apps/mobile` (Expo) implementations. The current `apps/mobile` path has been reused for a native Flutter business skeleton; it is not Expo or an active web client.
- Keeps `npm install` and CI focused on Angular + .NET
- Preserves BFF and UI patterns for parity checks during migration

## Parity during migration

Use [angular-migration-checklist.md](./angular-migration-checklist.md) against the archive repo or git history — not live paths under `apps/web`.
