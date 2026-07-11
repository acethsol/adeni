import * as SecureStore from "expo-secure-store";
import {
  defaultLocale,
  isLocaleId,
  LOCALE_COOKIE_NAME,
  type LocaleId,
} from "@adeni/shared";

const LOCALE_STORAGE_KEY = LOCALE_COOKIE_NAME;
const MARKET_STORAGE_KEY = "adeni_market";

export async function readStoredLocale(): Promise<LocaleId> {
  try {
    const value = await SecureStore.getItemAsync(LOCALE_STORAGE_KEY);

    if (value && isLocaleId(value)) {
      return value;
    }
  } catch {
    // SecureStore unavailable on web dev — fall back to default.
  }

  return defaultLocale;
}

export async function writeStoredLocale(locale: LocaleId): Promise<void> {
  try {
    await SecureStore.setItemAsync(LOCALE_STORAGE_KEY, locale);
  } catch {
    // Ignore persistence errors in local dev.
  }
}

export async function readStoredMarketId(): Promise<string | null> {
  try {
    return await SecureStore.getItemAsync(MARKET_STORAGE_KEY);
  } catch {
    return null;
  }
}

export async function writeStoredMarketId(marketId: string): Promise<void> {
  try {
    await SecureStore.setItemAsync(MARKET_STORAGE_KEY, marketId);
  } catch {
    // Ignore persistence errors in local dev.
  }
}
