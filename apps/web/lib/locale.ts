import { cookies } from "next/headers";
import {
  defaultLocale,
  isLocaleId,
  LOCALE_COOKIE_NAME,
  type LocaleId,
} from "@adeni/shared";

export { LOCALE_COOKIE_NAME };

export async function getLocale(): Promise<LocaleId> {
  const cookieStore = await cookies();
  const value = cookieStore.get(LOCALE_COOKIE_NAME)?.value;

  if (value && isLocaleId(value)) {
    return value;
  }

  return defaultLocale;
}
