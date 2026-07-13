import { enMessages } from "./messages/en";
import { esMessages } from "./messages/es";
import { frMessages } from "./messages/fr";
import { ptMessages } from "./messages/pt";
import { defaultLocale, isLocaleId, translate, type LocaleId, type Messages } from "./types";

const catalog: Record<LocaleId, Messages> = {
  en: enMessages,
  fr: frMessages,
  es: esMessages,
  pt: ptMessages,
};

const BOOKING_STATUS_KEYS: Record<number, string> = {
  0: "bookings.status.pending",
  1: "bookings.status.confirmed",
  2: "bookings.status.rejected",
  3: "bookings.status.cancelled",
};

function resolveLocale(locale: LocaleId | string): LocaleId {
  return isLocaleId(locale) ? locale : defaultLocale;
}

function localized(locale: LocaleId | string, key: string): string {
  return translate(catalog[resolveLocale(locale)], key);
}

export function formatBookingStatusLabel(locale: LocaleId | string, status: number): string {
  const key = BOOKING_STATUS_KEYS[status];
  return key ? localized(locale, key) : localized(locale, "bookings.status.unknown");
}
