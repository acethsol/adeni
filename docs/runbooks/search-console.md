# Google Search Console — Adeni Discover

Ops checklist after Discover is on a stable public host (`https://adeni.com`).

## 1. Verify ownership (HTML tag)

1. In [Google Search Console](https://search.google.com/search-console), add property **URL prefix** `https://adeni.com`.
2. Choose **HTML tag** verification. Copy the `content` value from:
   ```html
   <meta name="google-site-verification" content="TOKEN_HERE" />
   ```
3. Set Discover env / file replacement:
   - `environment.production.ts` → `googleSiteVerification: "TOKEN_HERE"`
   - or CI replace the same field at build time
4. Redeploy Discover SSR. Confirm in page source:
   ```html
   <meta name="google-site-verification" content="TOKEN_HERE">
   ```
5. Click **Verify** in Search Console.

## 2. Submit sitemap

1. Confirm live:
   - `https://adeni.com/robots.txt` includes `Sitemap: https://adeni.com/sitemap.xml`
   - `https://adeni.com/sitemap.xml` returns `200` with `<urlset>`
2. In Search Console → **Sitemaps** → add `sitemap.xml` (or full URL).
3. Spot-check indexed URLs later: `/lagos`, `/ottawa`, `/lagos/lekki`, `/lagos/hair-grooming`, `/businesses/{slug}`.

## 3. SSR note

Sitemap and page HTML must be served by the Node SSR entry (`server.ts` / `serve:ssr`), not static-only hosting. Ensure deploy uses `outputMode: server` and `allowedHosts` includes `adeni.com`.

## 4. Do not commit secrets

The verification token is not a high-value secret but treat production env replacements like other deploy config — prefer CI injection over committing tokens to a public fork.
