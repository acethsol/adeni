import { defaultLocale, type LocaleId } from "./types";

export const TRANSLATION_COOKIE_NAME = "adeni_translate_content";

/** Seed and business-supplied copy is authored in English for now. */
export const DEFAULT_CONTENT_SOURCE_LOCALE: LocaleId = "en";

export function resolveTranslationPreference(
  locale: LocaleId,
  cookieValue?: string | null,
): boolean {
  if (cookieValue === "0" || cookieValue === "false") {
    return false;
  }

  if (cookieValue === "1" || cookieValue === "true") {
    return true;
  }

  return locale !== defaultLocale;
}

export function shouldTranslateContent(
  locale: LocaleId,
  enabled: boolean,
  text?: string | null,
): boolean {
  if (!enabled || locale === DEFAULT_CONTENT_SOURCE_LOCALE) {
    return false;
  }

  return Boolean(text?.trim());
}
