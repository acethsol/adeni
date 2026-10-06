import { enMessages } from "./messages/en";
import { frMessages } from "./messages/fr";
import { defaultLocale, isLocaleId, translate, type LocaleId, type Messages } from "./types";

const catalog: Record<LocaleId, Messages> = {
  en: enMessages,
  fr: frMessages,
};

const PROMPT_KEYS = [
  "search.prompts.barberNearMe",
  "search.prompts.hairSalonBraids",
  "search.prompts.nailsWeekend",
  "search.prompts.massageNearMe",
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
