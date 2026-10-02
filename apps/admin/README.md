# Adeni admin portal (Angular)

Angular strangler target for Next.js `/admin/*`. See [ADR-012](../../docs/adr/ADR-012-angular-web-deferred-flutter.md).

## Dev

```bash
nvm use
npm install
dotnet run --project src/Adeni.Api --launch-profile http
npm run dev:admin
```

Open http://localhost:5180 — local dev uses `auth0|local-admin` when API `Auth0:Enabled` is `false` (see `DevAdminAuthMiddleware`).

Configure Auth0 SPA in `src/environments/environment.development.ts` when ready (deferred until Azure).

Production build uses `baseHref: /admin/`.
