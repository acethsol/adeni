# Addendum: Sprint 38c — Bilingual email + portal discoverability

| Field | Value |
|-------|-------|
| **Parent** | [sprint-38c-notification-templates-i18n.md](./sprint-38c-notification-templates-i18n.md) |
| **Status** | Done (2026-10-22) |

## Bilingual customer email

- Tenant `notificationLanguageMode`: `single` (default) \| `bilingual`
- `defaultNotificationLocale`: primary locale for **single** mode and for **SMS** when bilingual (`en` when bilingual EN+FR)
- **Email + bilingual:** one message — render catalog `en` + `fr`, merge with `--- English ---` / `--- Français ---` section headings; subject `en / fr` when they differ
- **SMS + bilingual:** primary locale only (ignores customer `PreferredLocale` for SMS body)
- **Email + single:** unchanged locale resolution (customer → tenant default → market → `en`)

## Portal discoverability

- Sidebar **Settings** → **Notifications** tab (requires `portal.profile`)
- Header gear menu: **Business settings** + **Notification preferences** (not only appearance/language)
- Search indexes settings tabs (e.g. “Notifications” → `/settings?tab=notifications`)
- Shortcut route: `/settings/notifications` → notifications tab

## API

`notificationPreferencesSchema` adds `notificationLanguageMode`.
