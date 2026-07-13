# Adeni markets catalog

Bootstrap seed for launch cities, currencies, coordinates, and supported languages.

## Runtime source of truth

Markets are stored in Postgres (`catalog.markets`) and served by:

- Public API: `GET /api/v1/markets`
- Admin API: `GET/POST/PUT/PATCH /api/v1/admin/markets` (admin + MFA)
- Web/mobile fetch markets at runtime (JSON below is fallback only)

## Bootstrap seed

On first API startup with an empty `catalog.markets` table, rows are seeded from `markets.json`.

Edit this file when adding markets that should appear on fresh databases — then restart the API so the seeder runs (or create via admin UI after first deploy).

## Add a market (seed file)

```json
{
  "id": "montreal",
  "name": "Montreal",
  "countryCode": "CA",
  "currency": "CAD",
  "timeZoneId": "America/Toronto",
  "defaultLocation": { "lat": 45.5017, "lng": -73.5673 },
  "languages": ["en", "fr"],
  "isLive": false
}
```

Set `isLive: true` when the city is ready for GTM. In production, prefer toggling live status from the admin portal (`/admin`) so changes apply without redeploy.
