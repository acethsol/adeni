# Auth0 setup (M0+)

## Tenant applications

| App type | Client | Used by |
|----------|--------|---------|
| **Regular Web** | Next.js (`@auth0/nextjs-auth0`) | Public, business, admin web |
| **Native** | Expo AuthSession | iOS + Android mobile |

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

## Next.js web

1. Create Auth0 **Regular Web Application** for Next.js.
2. Callback URLs: `http://localhost:3000/auth/callback`, staging/prod URLs.
3. Allowed logout URLs: same origins.
4. Install `@auth0/nextjs-auth0` in `apps/web` (Sprint 3b — done).
5. Copy `apps/web/.env.local.example` → `.env.local` and set `APP_BASE_URL`, `AUTH0_*` vars.
6. Callback URL: `http://localhost:3000/auth/callback` (v4 SDK auto-mounts `/auth/*` routes).

## Expo mobile

1. Create Auth0 **Native** application for `apps/mobile`.
2. Callback URL pattern per Expo / platform (see Auth0 Expo docs).
3. Enable **Refresh Token Rotation** for native clients.

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
    "http://localhost:3000",
    "http://localhost:5173"
  ]
}
```

## Staging

Use `appsettings.Staging.json` or environment variables — see previous docs.

## Archived Flutter client

The Flutter prototype lives in `mobile/_archive/adeni_app_flutter` (July 2026 pivot). Do not use for new work.

See [docs/frontend.md](frontend.md) and [Frontend Architecture v1 (Confluence)](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065).
