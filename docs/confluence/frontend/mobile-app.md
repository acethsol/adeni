# Frontend — Flutter Mobile (`apps/mobile`)

> **Parent:** [Frontend Overview](../frontend-overview.md)

---

## Purpose

**Business operations on iOS/Android** — owner, employee, and front-desk modes. Consumer discover/booking Flutter is **deferred** until web GA (ADR-012).

---

## Stack

| Item | Value |
|------|-------|
| Framework | Flutter 3.22+ |
| API | `AdeniApiClient` — health today; full OpenAPI client planned |
| Config | `--dart-define=ADENI_API_URL=...` |

---

## Directory layout

```
apps/mobile/lib/
  core/config/       AppConfig
  core/api/          AdeniApiClient
  features/
    home/            Mode launcher (dev)
    owner/           Owner shell
    employee/        Schedule shell
    front_desk/      Tablet-oriented check-in/checkout
```

---

## Current status

| Capability | Status |
|------------|--------|
| Health check banner | ✅ |
| Auth0 PKCE + tenant header | Planned |
| Bookings list / check-in | Planned (spec) |
| Queue / kiosk parity with portal | Future phases |

Spec: `docs/specs/flutter-business-mobile-skeleton.md`.

---

## API contract

Same **`/api/v1/`** modular monolith as web clients. Flutter will **not** consume npm packages — mirror DTOs via OpenAPI codegen or hand-maintained models aligned with `@adeni/shared`.

---

## Run locally

```bash
cd apps/mobile
flutter pub get
flutter run
# Android emulator:
flutter run --dart-define=ADENI_API_URL=http://10.0.2.2:5169
```

Requires API at `http://localhost:5169`.

---

## Related backend modules

[Booking](../backend/booking.md) (queue, kiosk, staff calendar), [Identity](../backend/identity-and-auth.md).

---

## Related docs

- `apps/mobile/README.md`
- `docs/adr/ADR-012-angular-web-deferred-flutter.md`
