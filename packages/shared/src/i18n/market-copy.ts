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
