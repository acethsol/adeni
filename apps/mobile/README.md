# @adeni/mobile

Expo app — unified customer + business client for iOS and Android.

## Dev

```powershell
# From repo root — API must be running on :5169
docker compose up -d
dotnet run --project src/Adeni.Api --launch-profile http

npm run dev:mobile
```

Scan the QR code with Expo Go, or press `a` / `i` for Android / iOS simulator.

## API URL

| Target | Default base URL |
|--------|------------------|
| Android emulator | `http://10.0.2.2:5169` |
| iOS simulator | `http://localhost:5169` |
| Physical device | Set `EXPO_PUBLIC_ADENI_API_URL` to your machine's LAN IP |

Copy `.env.example` to `.env` and adjust for physical devices.

**Local booking (no Auth0):** With the API in Development (`Auth0:Enabled: false`), set `EXPO_PUBLIC_DEV_CUSTOMER_AUTH0_SUB=auth0|local-customer` so the app sends `X-Dev-Auth0-Sub` on booking requests (same pattern as web `DEV_CUSTOMER_AUTH0_SUB`).

**Local business inbox:** Set `EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB=auth0|local-business` — dev seed links this sub to the `lekki-cuts` tenant. Open **Account → Booking inbox**.

**Auth0 Native:** Set `EXPO_PUBLIC_AUTH0_*` vars and add callback URL `adeni://callback` on your Auth0 Native application. Sign in from the **Account** tab.

Uses `@adeni/api-client` and `@adeni/shared` from the monorepo (`metro.config.js`).

## Sprint 5 progress

| Slice | Status |
|-------|--------|
| Browse (home, discover, business profile) | ✅ |
| Customer booking flow | ✅ |
| Auth0 Native login | ✅ |
| Business booking management | ✅ |
