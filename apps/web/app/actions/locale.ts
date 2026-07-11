"use server";

import { cookies } from "next/headers";
import { revalidatePath } from "next/cache";
import { getMarketById, isLocaleId, LOCALE_COOKIE_NAME, type LocaleId } from "@adeni/shared";
import { MARKET_COOKIE_NAME } from "@/lib/market-constants";

export async function setLocaleCookie(locale: LocaleId) {
  if (!isLocaleId(locale)) {
    return;
  }

  const cookieStore = await cookies();
  cookieStore.set(LOCALE_COOKIE_NAME, locale, {
    path: "/",
    maxAge: 60 * 60 * 24 * 365,
    sameSite: "lax",
  });

  revalidatePath("/", "layout");
}

export async function setMarketCookie(marketId: string) {
  if (!getMarketById(marketId)) {
    return;
  }

  const cookieStore = await cookies();
  cookieStore.set(MARKET_COOKIE_NAME, marketId, {
    path: "/",
    maxAge: 60 * 60 * 24 * 365,
    sameSite: "lax",
  });

  revalidatePath("/", "layout");
}

export async function setLocaleRegion(locale: LocaleId, marketId: string) {
  await Promise.all([setLocaleCookie(locale), setMarketCookie(marketId)]);
}
