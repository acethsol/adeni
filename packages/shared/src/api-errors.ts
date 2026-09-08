import { t } from "./i18n";

/** RFC 7807 ProblemDetails body returned by Adeni APIs (`code` + `params` for client i18n). */
export type ApiErrorResponse = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  params?: Record<string, string | number | boolean | null>;
  correlationId?: string;
};

function normalizeParams(
  params?: Record<string, string | number | boolean | null>,
): Record<string, string | number> | undefined {
  if (!params) {
    return undefined;
  }

  const normalized: Record<string, string | number> = {};
  for (const [key, value] of Object.entries(params)) {
    if (value === null || value === undefined) {
      continue;
    }

    normalized[key] = typeof value === "boolean" ? (value ? "true" : "false") : value;
  }

  return Object.keys(normalized).length > 0 ? normalized : undefined;
}

export function isApiErrorResponse(value: unknown): value is ApiErrorResponse {
  return typeof value === "object" && value !== null;
}

/** Resolve a localized user-facing message from an API error response body. */
export function localizeErrorResponse(locale: string, response: ApiErrorResponse | unknown): string {
  if (!isApiErrorResponse(response)) {
    return "Something went wrong.";
  }

  if (response.code) {
    const key = `errors.${response.code}`;
    const translated = t(locale, key, normalizeParams(response.params));
    if (translated !== key) {
      return translated;
    }

    const category = response.code.split(".")[0];
    const categoryKey = `errors.${category}`;
    const categoryMessage = t(locale, categoryKey);
    if (categoryMessage !== categoryKey) {
      return categoryMessage;
    }
  }

  if (typeof response.title === "string" && response.title.length > 0) {
    return response.title;
  }

  if (typeof response.detail === "string" && response.detail.length > 0) {
    return response.detail;
  }

  if (typeof response.code === "string" && response.code.length > 0) {
    return response.code;
  }

  return "Something went wrong.";
}

export async function localizeErrorFromResponse(
  locale: string,
  response: Response,
  fallback?: string,
): Promise<string> {
  const payload = await response.json().catch(() => ({}));
  const message = localizeErrorResponse(locale, payload);
  if (message === "Something went wrong." && fallback) {
    return fallback;
  }

  return message;
}
