import { Injectable, signal } from "@angular/core";
import { defaultLocale, isLocaleId, LOCALE_COOKIE_NAME, type LocaleId } from "@adeni/shared";
import { readCookie, writeCookie } from "../cookie.util";

const LOCALE_COOKIE_MAX_AGE = 60 * 60 * 24 * 365;

@Injectable({ providedIn: "root" })
export class DiscoverLocaleService {
  readonly locale = signal<LocaleId>(this.readInitial());

  setLocale(locale: string): void {
    const next = isLocaleId(locale) ? locale : defaultLocale;
    writeCookie(LOCALE_COOKIE_NAME, next, LOCALE_COOKIE_MAX_AGE);
    this.locale.set(next);
  }

  private readInitial(): LocaleId {
    const cookie = readCookie(LOCALE_COOKIE_NAME);
    return cookie && isLocaleId(cookie) ? cookie : defaultLocale;
  }
}
