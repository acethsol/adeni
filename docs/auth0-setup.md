# Auth0 setup (M0)

## Tenant application

1. Create Auth0 Application (SPA) for Flutter clients.
2. Create Auth0 API with identifier `https://api.adeni.io` (matches `Auth0:Audience`).
3. Enable **RS256** signing.

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
2. Create **Action** `Require MFA for Admin` on Login flow:

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const roles = event.authorization?.roles ?? [];
  if (roles.includes('admin') && !event.authentication?.methods?.some(m => m.name === 'mfa')) {
    api.multifactor.enable('any', { allowRememberBrowser: false });
  }
};
```

3. Set `Auth0:RequireMfaForAdmin=true` in API config (default).
4. API policy `AdminMfaPolicy` rejects admin JWTs without `amr: mfa`.

## Local development

Set in `appsettings.Development.json`:

```json
"Auth0": { "Enabled": false }
```

JWT validation is skipped locally; enable when testing Auth0 integration.
