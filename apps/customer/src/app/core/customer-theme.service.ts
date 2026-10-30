import { DestroyRef, Injectable, inject, signal } from "@angular/core";
import { injectAdeniTranslate } from "@adeni/ui";

const THEME_STORAGE_KEY = "adeni.customerApp.theme";
const LEGACY_THEME_STORAGE_KEY = "adeni.portal.theme";

export type CustomerTheme = "light" | "dark";
export type CustomerThemePreference = CustomerTheme | "system";

function readThemePreference(): CustomerThemePreference {
  try {
    let stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (!stored) {
      stored = localStorage.getItem(LEGACY_THEME_STORAGE_KEY);
      if (stored === "light" || stored === "dark" || stored === "system") {
        localStorage.setItem(THEME_STORAGE_KEY, stored);
      }
    }
    if (stored === "light" || stored === "dark" || stored === "system") {
      return stored;
    }
  } catch {
    // optional
  }
  return "system";
}

function resolveTheme(preference: CustomerThemePreference): CustomerTheme {
  if (preference === "light" || preference === "dark") {
    return preference;
  }
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

@Injectable({ providedIn: "root" })
export class CustomerThemeService {
  protected readonly tr = injectAdeniTranslate();
  private readonly destroyRef = inject(DestroyRef);

  readonly preference = signal<CustomerThemePreference>(readThemePreference());
  readonly theme = signal<CustomerTheme>(resolveTheme(this.preference()));

  constructor() {
    if (typeof document === "undefined") {
      return;
    }
    this.applyResolved(this.theme());
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onSystemTheme = (): void => {
      if (this.preference() === "system") {
        this.applyResolved(resolveTheme("system"));
      }
    };
    media.addEventListener("change", onSystemTheme);
    this.destroyRef.onDestroy(() => media.removeEventListener("change", onSystemTheme));
  }

  toggle(): void {
    this.setPreference(this.theme() === "dark" ? "light" : "dark");
  }

  setPreference(preference: CustomerThemePreference): void {
    this.preference.set(preference);
    this.applyResolved(resolveTheme(preference));
    try {
      localStorage.setItem(THEME_STORAGE_KEY, preference);
    } catch {
      // optional
    }
  }

  themeToggleLabel(): string {
    return this.theme() === "dark" ? this.tr("common.switchLightTheme") : this.tr("common.switchDarkTheme");
  }

  private applyResolved(theme: CustomerTheme): void {
    this.theme.set(theme);
    document.documentElement.dataset["theme"] = theme;
  }
}
