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

const PROMPT_KEYS = [
  "search.prompts.barberNearMe",
  "search.prompts.hairSalonBraids",
  "search.prompts.nailsWeekend",
  "search.prompts.plumberArea",
] as const;

function resolveLocale(locale: LocaleId | string): LocaleId {
  return isLocaleId(locale) ? locale : defaultLocale;
}

function localized(locale: LocaleId | string, key: string): string {
  return translate(catalog[resolveLocale(locale)], key);
}

export function getAskAdeniPrompts(locale: LocaleId | string): string[] {
  return PROMPT_KEYS.map((key) => localized(locale, key));
}
