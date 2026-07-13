import {
  DEFAULT_CONTENT_SOURCE_LOCALE,
  resolveTranslationPreference,
  TRANSLATION_COOKIE_NAME,
  type LocaleId,
} from "@adeni/shared";
import { getLocale } from "@/lib/locale";
import { translateTextMap } from "@/lib/translation-service";
import { cookies } from "next/headers";

export async function getTranslationPreference(): Promise<boolean> {
  const [locale, cookieStore] = await Promise.all([getLocale(), cookies()]);
  const cookieValue = cookieStore.get(TRANSLATION_COOKIE_NAME)?.value;
  return resolveTranslationPreference(locale, cookieValue);
}

export async function translateMany(
  items: Array<string | null | undefined>,
  locale: LocaleId,
  enabled: boolean,
): Promise<string[]> {
  if (!enabled || locale === DEFAULT_CONTENT_SOURCE_LOCALE) {
    return items.map((item) => item ?? "");
  }

  const normalized = items.map((item) => item?.trim() ?? "");
  const unique = [...new Set(normalized.filter(Boolean))];

  if (unique.length === 0) {
    return normalized;
  }

  const translated = await translateTextMap(
    unique,
    DEFAULT_CONTENT_SOURCE_LOCALE,
    locale,
  );

  return normalized.map((text) => (text ? (translated.get(text) ?? text) : ""));
}
