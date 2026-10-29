import { DestroyRef, Injectable, inject, signal } from "@angular/core";
import { injectAdeniTranslate } from "@adeni/ui";

const THEME_STORAGE_KEY = "adeni.portal.theme";

export type businessTheme = "light" | "dark";
export type ThemePreference = businessTheme | "system";

function readThemePreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === "light" || stored === "dark" || stored === "system") {
      return stored;
    }
  } catch {
    // Preference is optional.
  }
  return "system";
}

function resolveTheme(preference: ThemePreference): businessTheme {
  if (preference === "light" || preference === "dark") {
    return preference;
  }
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

@Injectable({ providedIn: "root" })
export class BusinessThemeService {
  protected readonly tr = injectAdeniTranslate();
  private readonly destroyRef = inject(DestroyRef);

  readonly preference = signal<ThemePreference>(readThemePreference());
  readonly theme = signal<businessTheme>(resolveTheme(this.preference()));

  constructor() {
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

  setPreference(preference: ThemePreference): void {
    this.preference.set(preference);
    this.applyResolved(resolveTheme(preference));
    try {
      localStorage.setItem(THEME_STORAGE_KEY, preference);
    } catch {
      // Preference is optional.
    }
  }

  themeToggleLabel(): string {
    return this.theme() === "dark" ? this.tr("common.switchLightTheme") : this.tr("common.switchDarkTheme");
  }

  private applyResolved(theme: businessTheme): void {
    this.theme.set(theme);
    document.documentElement.dataset["theme"] = theme;
  }
}
