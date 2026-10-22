# Addendum: Sprint 38c — Bilingual email + portal discoverability

| Field | Value |
|-------|-------|
| **Parent** | [sprint-38c-notification-templates-i18n.md](./sprint-38c-notification-templates-i18n.md) |
| **Status** | Done (2026-10-22); bilingual locale pair extensibility (2026-10-22) |

## Bilingual customer email

- Tenant `notificationLanguageMode`: `single` (default) \| `bilingual`
- `defaultNotificationLocale`: **primary** locale — single mode default; bilingual email first section + **SMS** body
- `secondaryNotificationLocale`: required when `bilingual`; must differ from primary; options from notification template catalog locales (`NOTIFICATION_LOCALES` in `@adeni/shared`, today `en` + `fr`)
- **Email + bilingual:** one message — render catalog for primary + secondary locales; section headings use native locale labels (e.g. `--- English ---`, `--- Français ---`); subject `primary / secondary` when they differ
- **SMS + bilingual:** primary locale only (ignores customer `PreferredLocale` for SMS body)
- **Email + single:** unchanged locale resolution (customer → tenant default → market → `en`)
- Portal template override editors pre-fill from catalog defaults per selected template locale; only diffs persist as overrides

## Portal discoverability

- Sidebar **Settings** → **Notifications** tab (requires `portal.profile`)
- Header gear menu: **Business settings** + **Notification preferences** (not only appearance/language)
- Search indexes settings tabs (e.g. “Notifications” → `/settings?tab=notifications`)
- Shortcut route: `/settings/notifications` → notifications tab

## API

`notificationPreferencesSchema` adds `notificationLanguageMode`, optional `secondaryNotificationLocale` (required + distinct when bilingual). DB: `notifications.tenant_notification_preferences.SecondaryNotificationLocale` (nullable).
