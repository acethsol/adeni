import {
  defaultLocale,
  isLocaleId,
  LOCALE_COOKIE_NAME,
  type LocaleId,
} from "@adeni/shared";
import { secureGetItem, secureSetItem } from "@/lib/secure-storage";

const LOCALE_STORAGE_KEY = LOCALE_COOKIE_NAME;
const MARKET_STORAGE_KEY = "adeni_market";

export async function readStoredLocale(): Promise<LocaleId> {
  const value = await secureGetItem(LOCALE_STORAGE_KEY);

  if (value && isLocaleId(value)) {
    return value;
  }

  return defaultLocale;
}

export async function writeStoredLocale(locale: LocaleId): Promise<void> {
  await secureSetItem(LOCALE_STORAGE_KEY, locale);
}

export async function readStoredMarketId(): Promise<string | null> {
  return secureGetItem(MARKET_STORAGE_KEY);
}

export async function writeStoredMarketId(marketId: string): Promise<void> {
  await secureSetItem(MARKET_STORAGE_KEY, marketId);
}
