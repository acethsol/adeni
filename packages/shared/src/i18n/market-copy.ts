import { enMessages } from "./messages/en";
import { frMessages } from "./messages/fr";
import { defaultLocale, isLocaleId, translate, type LocaleId, type Messages } from "./types";

const catalog: Record<LocaleId, Messages> = {
  en: enMessages,
  fr: frMessages,
};

function resolveLocale(locale: LocaleId | string): LocaleId {
  return isLocaleId(locale) ? locale : defaultLocale;
}

function localized(locale: LocaleId | string, key: string): string {
  return translate(catalog[resolveLocale(locale)], key);
}

export function getMarketTagline(locale: LocaleId | string): string {
  return localized(locale, "market.tagline");
}

export function getMarketDescription(locale: LocaleId | string): string {
  return localized(locale, "market.description");
}
