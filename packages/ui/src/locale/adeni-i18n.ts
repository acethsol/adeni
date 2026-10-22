import { inject } from "@angular/core";
import { resolveApiErrorMessage, t } from "@adeni/shared";
import { AdeniLocaleService } from "./adeni-locale.service";

export type AdeniTranslate = (key: string, vars?: Record<string, string | number>) => string;

export type AdeniApiErrorMessage = (
  error: unknown,
  fallbackKey?: string,
  vars?: Record<string, string | number>,
) => string;

/**
 * Field initializer helper: `protected readonly tr = injectAdeniTranslate();`
 * Reads the active locale signal on every call, so templates re-render when the language changes.
 */
export function injectAdeniTranslate(): AdeniTranslate {
  const locale = inject(AdeniLocaleService).locale;
  return (key, vars) => t(locale(), key, vars);
}

/** Field initializer helper: `protected readonly apiErr = injectAdeniApiError();` for catch blocks. */
export function injectAdeniApiError(): AdeniApiErrorMessage {
  const locale = inject(AdeniLocaleService).locale;
  return (error, fallbackKey, vars) => resolveApiErrorMessage(locale(), error, fallbackKey, vars);
}
