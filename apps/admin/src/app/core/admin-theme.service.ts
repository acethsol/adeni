import { DestroyRef, Injectable, inject, signal } from "@angular/core";

const THEME_STORAGE_KEY = "adeni.admin.theme";

export type AdminTheme = "light" | "dark";
export type ThemePreference = AdminTheme | "system";

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

function resolveTheme(preference: ThemePreference): AdminTheme {
  if (preference === "light" || preference === "dark") {
    return preference;
  }
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

@Injectable({ providedIn: "root" })
export class AdminThemeService {
  private readonly destroyRef = inject(DestroyRef);

  readonly preference = signal<ThemePreference>(readThemePreference());
  readonly theme = signal<AdminTheme>(resolveTheme(this.preference()));

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

  private applyResolved(theme: AdminTheme): void {
    this.theme.set(theme);
    document.documentElement.dataset["theme"] = theme;
    document.documentElement.dataset["staffApp"] = "admin";
  }
}
