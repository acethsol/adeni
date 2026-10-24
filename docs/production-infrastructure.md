# Production infrastructure configuration

The API validates Staging/Production settings after loading Key Vault and before registering infrastructure. Validation checks configuration shape, not remote connectivity or credential validity. Secrets belong in Key Vault or deployment settings.

## Reverse proxies

Forwarding is disabled by default. Enable it explicitly for Azure Front Door / App Service / Container Apps using the actual proxy addresses or dedicated ingress subnets observed at the application boundary:

```json
{
  "ReverseProxy": {
    "Enabled": true,
    "ForwardLimit": 1,
    "KnownProxies": ["10.0.0.10"],
    "KnownNetworks": []
  }
}
```

The address above is illustrative; it must be replaced with your deployment's trusted ingress. No Azure ranges are implicitly trusted. Keep CIDRs narrow and dedicated to ingress. `/0` networks, unspecified proxy addresses, missing trust lists, and nonpositive limits are rejected. Use the platform's ingress/access restrictions to prevent bypassing Front Door and to restrict origin access. The trusted proxy must sanitize/append forwarded headers correctly; trusting a subnet also trusts any sender in that subnet.

Only `X-Forwarded-For` and `X-Forwarded-Proto` are consumed, from right to left, up to `ForwardLimit`, checking each sending hop. Set the limit and trust list for the complete actual proxy chain; do not increase the limit simply to accept user-supplied prefixes. Forwarded host/prefix are not enabled. Use this application's forwarding configuration rather than platform auto-forwarding startup middleware (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is rejected to prevent duplicate forwarding).

Forwarding runs before exception handling, request logging, HTTPS redirection, CORS, rate limiting, authentication, tenant/security middleware, and authorization. The existing limiter still partitions on `Connection.RemoteIpAddress`; it never reads raw request headers. Policies and limits are unchanged. If forwarding is disabled or the immediate peer is untrusted, the connection peer remains the limiter key and request scheme is unchanged. With multiple hops, processing stops at the first untrusted intermediary.

## Required deployment settings

- `ConnectionStrings:AdeniDb`: parseable SQL Server connection string with a server. SQL authentication and managed identity/integrated authentication retain their existing connection-string support.
- `Auth0:Enabled=true`, `Auth0:Domain` as a hostname, and a nonblank `Auth0:Audience`.
- `Cors:AllowedOrigins`: explicit public HTTPS origins, without paths, wildcards, or trailing slashes.
- `App:PublicWebBaseUrl` and `Portal:PublicBaseUrl`: public HTTPS URLs for generated customer links and staff invitations; localhost defaults must be overridden.
- Effective `Finance:Provider` / `Payments:Provider`: Paystack, Stripe, or MarketDefault. Selected commerce providers require a secret key, webhook secret, and HTTPS base URL (existing vendor defaults apply). MarketDefault requires both commerce providers.
- `SubscriptionBilling:Provider`: Paystack or MarketDefault. Both require Paystack billing secret/webhook keys; the existing fallback to top-level `Paystack` secrets is honored. Billing base URL validation mirrors its existing default/fallback behavior.
- Resend/Twilio credentials and sender fallbacks remain required as documented in [notifications.md](notifications.md).
- `Storage:Provider=AzureBlob`: connection string supporting HTTPS and shared-key SAS generation, plus a container. `Storage:AzureBlob:PublicBaseUrl` is optional; the adapter falls back to its blob endpoint. If set, it must be public HTTPS. Public blob/container access or CDN access must be provisioned separately for downloads.

Local storage is rejected in Production because it has no shared/durable multi-instance storage guarantee. Staging may use Local with an explicit non-development signing key, HTTPS API/media URLs, and a nonblank root. That staging scenario assumes a single instance with an appropriately persistent volume; it is not a multi-instance storage solution. Development/Testing keep local/stub and in-memory configuration.

CDN overrides, subscription plan codes, public/publishable payment keys, unused provider keys, and observability remain optional. Existing Redis and Translator module startup requirements, and the existing Key Vault requirement, are unchanged. No network probes are performed during validation; deployment smoke tests must verify SQL, Auth0 metadata, blob upload/download, payment webhooks, and email/SMS delivery.
