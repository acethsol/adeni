import { inject, Injectable, InjectionToken, type Provider, signal } from "@angular/core";
import {
  defaultLocale,
  isLocaleId,
  LOCALE_COOKIE_NAME,
  supportedLocales,
  type LocaleId,
  type LocaleOption,
} from "@adeni/shared";

export type AdeniLocaleStorageMode = "cookie" | "localStorage";

export type AdeniLocaleConfig = {
  /** Discover uses cookie (SSR-friendly); portal/admin use localStorage. */
  storage: AdeniLocaleStorageMode;
  /** Required when `storage` is `localStorage`. */
  storageKey?: string;
  cookieMaxAgeSeconds?: number;
};

export const ADENI_LOCALE_CONFIG = new InjectionToken<AdeniLocaleConfig>("ADENI_LOCALE_CONFIG");

const DEFAULT_COOKIE_MAX_AGE = 60 * 60 * 24 * 365;

@Injectable()
export class AdeniLocaleService {
  private readonly config = inject(ADENI_LOCALE_CONFIG);

  readonly locales: readonly LocaleOption[] = supportedLocales;
  readonly locale = signal<LocaleId>(this.readInitial());

  constructor() {
    this.syncDocumentLang(this.locale());
  }

  setLocale(locale: string): void {
    const next = isLocaleId(locale) ? locale : defaultLocale;
    this.persist(next);
    this.locale.set(next);
    this.syncDocumentLang(next);
  }

  private readInitial(): LocaleId {
    const raw =
      this.config.storage === "cookie"
        ? readCookie(LOCALE_COOKIE_NAME)
        : readLocalStorage(this.requireStorageKey());
    return raw && isLocaleId(raw) ? raw : defaultLocale;
  }

  private persist(locale: LocaleId): void {
    if (this.config.storage === "cookie") {
      writeCookie(
        LOCALE_COOKIE_NAME,
        locale,
        this.config.cookieMaxAgeSeconds ?? DEFAULT_COOKIE_MAX_AGE,
      );
      return;
    }

    writeLocalStorage(this.requireStorageKey(), locale);
  }

  private requireStorageKey(): string {
    const key = this.config.storageKey?.trim();
    if (!key) {
      throw new Error("AdeniLocaleConfig.storageKey is required when storage is localStorage.");
    }
    return key;
  }

  private syncDocumentLang(locale: LocaleId): void {
    if (typeof document === "undefined") {
      return;
    }
    document.documentElement.lang = locale;
  }
}

export function provideAdeniLocale(config: AdeniLocaleConfig): Provider[] {
  if (config.storage === "localStorage" && !config.storageKey?.trim()) {
    throw new Error("provideAdeniLocale: storageKey is required for localStorage.");
  }

  return [
    { provide: ADENI_LOCALE_CONFIG, useValue: config },
    AdeniLocaleService,
  ];
}

function readCookie(name: string): string | null {
  if (typeof document === "undefined") {
    return null;
  }

  const prefix = `${name}=`;
  for (const part of document.cookie.split(";")) {
    const trimmed = part.trim();
    if (trimmed.startsWith(prefix)) {
      return decodeURIComponent(trimmed.slice(prefix.length));
    }
  }

  return null;
}

function writeCookie(name: string, value: string, maxAgeSeconds: number): void {
  if (typeof document === "undefined") {
    return;
  }

  document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAgeSeconds}; samesite=lax`;
}

function readLocalStorage(key: string): string | null {
  if (typeof localStorage === "undefined") {
    return null;
  }

  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeLocalStorage(key: string, value: string): void {
  if (typeof localStorage === "undefined") {
    return;
  }

  try {
    localStorage.setItem(key, value);
  } catch {
    // Preference is optional.
  }
}
