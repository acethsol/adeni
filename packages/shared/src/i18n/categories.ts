import { formatCategoryLabel } from "../category-visuals";
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

function categoryKey(slug: string) {
  return `categories.${slug.trim().toLowerCase()}`;
}

function groupKey(slug: string) {
  return `categories.groups.${slug.trim().toLowerCase()}`;
}

function localized(locale: LocaleId | string, key: string): string | null {
  const messages = catalog[resolveLocale(locale)];
  const translated = translate(messages, key);
  return translated === key ? null : translated;
}

export function getCategoryLabel(
  locale: LocaleId | string,
  slug: string,
  fallback?: string,
): string {
  const normalized = slug.trim().toLowerCase();
  return localized(locale, categoryKey(normalized)) ?? fallback ?? formatCategoryLabel(normalized);
}

export function getCategoryGroupLabel(
  locale: LocaleId | string,
  slug: string,
  fallback?: string,
): string {
  const normalized = slug.trim().toLowerCase();
  return localized(locale, groupKey(normalized)) ?? fallback ?? formatCategoryLabel(normalized);
}
