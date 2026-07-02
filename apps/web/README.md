# @adeni/web

Next.js app — public discovery (SEO), business portal, admin portal.

## Dev

```powershell
# From repo root
npm run dev:web
```

Create `apps/web/.env.local`:

```
ADENI_API_URL=http://localhost:5169
```

## Routes (v1 scaffold)

| Route | Purpose |
|-------|---------|
| `/` | Public landing + categories |
| `/discover` | Geo search (placeholder) |
| `/businesses/[slug]` | SSR business profile (placeholder) |
| `/business` | Business portal (placeholder) |
| `/admin` | Admin portal (placeholder) |
