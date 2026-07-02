# @adeni/mobile

Expo app — single iOS/Android client for **customers and businesses** (role-based UX).

## Dev

```powershell
# From repo root
npm run dev:mobile
```

## API URL

Configure in a future `app.config.ts` / env — for now use Expo dev against local API:

- Android emulator: `http://10.0.2.2:5169`
- iOS simulator / device: your machine LAN IP

Uses `@adeni/api-client` from the monorepo (see `metro.config.js`).
