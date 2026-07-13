import type { LocaleId } from "./types";
import { enMessages } from "./messages/en";
import { frMessages } from "./messages/fr";
import { esMessages } from "./messages/es";
import { ptMessages } from "./messages/pt";
import { defaultLocale, isLocaleId, translate, type Messages } from "./types";

const catalog: Record<LocaleId, Messages> = {
  en: enMessages,
  fr: frMessages,
  es: esMessages,
  pt: ptMessages,
};

import { getCurrencySymbol } from "./currency";

export function getMessages(locale: string): Messages {
  const normalized = isLocaleId(locale) ? locale : defaultLocale;
  return catalog[normalized];
}

export function t(
  locale: string,
  key: string,
  vars?: Record<string, string | number>,
): string {
  return translate(getMessages(locale), key, vars);
}

export * from "./types";
export { getCurrencySymbol } from "./currency";
export * from "./locale-regions";
export { getCategoryLabel, getCategoryGroupLabel } from "./categories";
export { getReviewCountLabel } from "./reviews";
export { getMarketTagline, getMarketDescription } from "./market-copy";
export { formatBookingStatusLabel } from "./bookings";
export { getAskAdeniPrompts } from "./search-prompts";
export {
  TRANSLATION_COOKIE_NAME,
  DEFAULT_CONTENT_SOURCE_LOCALE,
  resolveTranslationPreference,
  shouldTranslateContent,
} from "./content-translation";
export { enMessages, frMessages, esMessages, ptMessages };
