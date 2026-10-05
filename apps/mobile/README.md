# Adeni Business (Flutter)

Supply-side mobile skeleton: **owner**, **employee**, and **front desk** modes. Spec: [docs/specs/flutter-business-mobile-skeleton.md](../../docs/specs/flutter-business-mobile-skeleton.md).

Consumer discover/booking Flutter is a separate track (ADR-012). Legacy Expo app: [adeni-legacy-clients](https://github.com/acethsol/adeni-legacy-clients).

## Prerequisites

- Flutter 3.22+ (stable)
- Adeni API running locally (`http://localhost:5169`)

## Run

```powershell
cd C:\DEV\Aceth\adeni\apps\mobile
flutter pub get
flutter run
```

### API base URL

| Target | Command |
|--------|---------|
| Windows / iOS simulator | default `http://localhost:5169` |
| Android emulator | `flutter run --dart-define=ADENI_API_URL=http://10.0.2.2:5169` |
| Custom host | `flutter run --dart-define=ADENI_API_URL=http://192.168.x.x:5169` |

Tap the API banner to refresh `GET /health`.

## Project layout

```
lib/
  core/config/     AppConfig (dart-define)
  core/api/        AdeniApiClient (health; bookings when Auth0 lands)
  features/
    home/          Mode launcher (dev)
    owner/         Owner shell
    employee/      Schedule shell
    front_desk/    Check-in / checkout shell (tablet-oriented)
```

## Next steps (spec)

- Auth0 Native (PKCE) + business JWT + `X-Tenant-Id`
- OpenAPI → Dart client; wire `GET /api/v1/business/bookings`
- Phase 2: `POST .../check-in` / `check-out` on API
