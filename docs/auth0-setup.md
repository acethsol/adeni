# Auth0 setup (M0+)

## Tenant applications

| App type | Client | Used by |
|----------|--------|---------|
| **Regular Web** | Next.js (`@auth0/nextjs-auth0`) | Legacy web until strangler completes |
| **SPA (PKCE)** | `@auth0/auth0-angular` | `apps/portal`, `apps/discover`, `apps/admin` (Angular) |
| **Native** | Expo AuthSession | iOS + Android mobile (until Flutter) |

Create Auth0 **API** with identifier `https://api.adeni.io` (matches `Auth0:Audience`).

Enable **RS256** signing.

## Login Action — inject Adeni claims

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://adeni.io/';
  const roles = event.authorization?.roles ?? ['customer'];
  api.idToken.setCustomClaim(`${namespace}roles`, roles);
  api.accessToken.setCustomClaim(`${namespace}roles`, roles);

  if (event.user.app_metadata?.tenant_id) {
    api.accessToken.setCustomClaim(`${namespace}tenant_id`, event.user.app_metadata.tenant_id);
  }
  if (event.user.app_metadata?.platform_user_id) {
    api.accessToken.setCustomClaim(`${namespace}platform_user_id`, event.user.app_metadata.platform_user_id);
  }
};
```

## MFA for admin (SOC2-06)

1. Auth0 Dashboard → Security → Multi-factor Auth → enable OTP/WebAuthn.
2. Create **Action** `Require MFA for Admin` on Login flow.
3. Set `Auth0:RequireMfaForAdmin=true` in API config.
4. API policy `AdminMfaPolicy` rejects admin JWTs without `amr: mfa`.

## Angular portal (`apps/portal`)

1. Create Auth0 **Single Page Application** (not Regular Web).
2. Callback / logout / web origins: `http://localhost:5173` (local).
3. Set `auth0.domain`, `auth0.clientId`, and `auth0.audience` in `apps/portal/src/environments/environment.development.ts` (see `apps/portal/.env.example.md`).
4. Local dev without Auth0: keep `devBusinessAuth0Sub` (same as Next `DEV_BUSINESS_AUTH0_SUB`) with API `Auth0:Enabled: false`.

## Angular admin (`apps/admin`)

1. Auth0 **SPA** (when staging/Azure is ready) — separate client; callback `http://localhost:5180`.
2. Local dev without Auth0: `devAdminAuth0Sub: "auth0|local-admin"` in `apps/admin/src/environments/environment.development.ts` (`DevAdminAuthMiddleware` on the API).

## Angular discover (`apps/discover`)

1. Auth0 **SPA** for **customers** — separate client from portal/admin; callback `http://localhost:5190`.
2. Local dev without Auth0: `devCustomerAuth0Sub: "auth0|local-customer"` (same as Next `DEV_CUSTOMER_AUTH0_SUB` / API seeder).
3. See `apps/discover/.env.example.md` for environment fields (`publicAppUrl`, optional `envMarketId`).
4. **You can defer Auth0 registration** until staging: leave `auth0.domain` / `clientId` empty and use dev customer sub with `Auth0:Enabled: false` on the API.

## Angular web (discover, portal, admin)

Create three Auth0 **Single Page Application** clients (or one app with multiple callback URLs during early dev):

| App | Local origin | Env docs |
|-----|--------------|----------|
| Discover | `http://localhost:5190` | `apps/discover/.env.example.md` |
| Portal | `http://localhost:5173` | `apps/portal/.env.example.md` |
| Admin | `http://localhost:5180` | `apps/admin/.env.example.md` |

Use Auth0 SPA SDK in each app; callbacks and logout URLs must match deployed origins.

## Legacy Next.js / Expo

Archived — see [legacy-clients-archive.md](./legacy-clients-archive.md) for Auth0 Regular Web / Native setup used historically.

## Local API development

Set in `appsettings.Development.json`:

```json
"Auth0": { "Enabled": false }
```

JWT validation skipped locally; enable when testing Auth0 end-to-end.

## CORS origins

Development (`appsettings.Development.json`):

```json
"Cors": {
  "AllowedOrigins": [
    "http://localhost:5173",
    "http://localhost:5180",
    "http://localhost:5190"
  ]
}
```

## Staging

Use `appsettings.Staging.json` or environment variables — see previous docs.

## Archived Flutter client

The Flutter prototype was retired in the July 2026 pivot (ADR-010). Do not use for new work.

See [docs/frontend.md](frontend.md) and [Frontend Architecture v1 (Confluence)](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065).
