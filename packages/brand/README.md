# @adeni/brand

**Option 3 — Premium & Distinctive** corrected developer pack (approved-reference PNGs).  
Rules: [CURSOR_BRAND_INSTRUCTIONS.md](./CURSOR_BRAND_INSTRUCTIONS.md) · Visual reference: [reference/Adeni-Option3-Approved-Reference.png](./reference/Adeni-Option3-Approved-Reference.png)

## Assets

| Path | Served as | Use |
|------|-----------|-----|
| `assets/brand/` | `/brand/*` | Logos, marks, wordmarks, tagline lockups |
| `assets/favicon/` | site root | Browser icons (transparent — `npm run generate:brand-favicons` from `adeni-mark.png`) |
| `assets/pwa/` | `/pwa/*` | PWA icons (same generator) |
| `brand-tokens.css` | optional `@import` | Brand color CSS variables |

## Angular

`npm install` → `scripts/sync-brand-assets.mjs` copies into each app `public/`.

```html
<adeni-brand-lockup [height]="40" surface="light" />
<adeni-staff-sidebar-brand caption="Business" [logoHeight]="44" />
<adeni-brand-logo variant="mark" [size]="32" />
```

## Flutter

Mirror `assets/brand/` from `flutter/assets/brand/` in the zip into `apps/mobile/assets/brand/`.

## Updates

Replace files from `Adeni_Branding_Assets.zip` under `assets/brand/`, then `npm run generate:brand-favicons` and `npm run sync:brand`.
