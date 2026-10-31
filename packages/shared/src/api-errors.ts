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

const staffPresenceGateCodes = new Set([
  "staff.presence_required",
  "staff.presence_expired",
  "staff.presence_location_mismatch",
]);

/** True when on-premises sensitive-data policy blocked the request. */
export function isStaffPresenceGateError(error: unknown): boolean {
  if (!isApiErrorResponse(error)) {
    if (typeof error === "object" && error !== null && "code" in error) {
      const code = (error as { code?: unknown }).code;
      return typeof code === "string" && staffPresenceGateCodes.has(code);
    }
    return false;
  }
  return typeof error.code === "string" && staffPresenceGateCodes.has(error.code);
}

export function readApiErrorCode(error: unknown): string | undefined {
  if (typeof error === "object" && error !== null && "code" in error) {
    const code = (error as { code?: unknown }).code;
    return typeof code === "string" ? code : undefined;
  }
  if (isApiErrorResponse(error) && typeof error.code === "string") {
    return error.code;
  }
  return undefined;
}

function translateErrorCode(
  locale: string,
  code: string,
  params?: ApiErrorResponse["params"],
): string | null {
  const key = `errors.${code}`;
  const translated = t(locale, key, normalizeParams(params));
  if (translated !== key) {
    return translated;
  }

  const categoryKey = `errors.${code.split(".")[0]}`;
  const categoryMessage = t(locale, categoryKey);
  return categoryMessage !== categoryKey ? categoryMessage : null;
}

/** Resolve a localized user-facing message from an API error response body. */
export function localizeErrorResponse(locale: string, response: ApiErrorResponse | unknown): string {
  if (!isApiErrorResponse(response)) {
    return t(locale, "toast.somethingWrongShort");
  }

  if (response.code) {
    const translated = translateErrorCode(locale, response.code, response.params);
    if (translated) {
      return translated;
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

  return t(locale, "toast.somethingWrongShort");
}

export async function localizeErrorFromResponse(
  locale: string,
  response: Response,
  fallback?: string,
): Promise<string> {
  const payload = await response.json().catch(() => ({}));
  const message = localizeErrorResponse(locale, payload);
  if (message === t(locale, "toast.somethingWrongShort") && fallback) {
    return fallback;
  }

  return message;
}

/** Shape of `AdeniApiError` (api-client) without importing it into `@adeni/shared`. */
export type ApiErrorLike = {
  message?: string;
  statusCode?: number;
  code?: string;
  params?: ApiErrorResponse["params"];
};

/**
 * User-facing message for a caught error in a catch block.
 * 1. API `code` (+ `params`) -> `errors.*` in the active locale
 * 2. anything else (unknown code, network, thrown English `Error`) -> `fallbackKey` in the active locale
 *    (server English text is never shown; add the `errors.*` key instead)
 */
export function resolveApiErrorMessage(
  locale: string,
  error: unknown,
  fallbackKey = "toast.somethingWrong",
  vars?: Record<string, string | number>,
): string {
  if (typeof error === "object" && error !== null) {
    const like = error as ApiErrorLike;
    if (typeof like.code === "string" && like.code.length > 0) {
      const translated = translateErrorCode(locale, like.code, like.params);
      if (translated) {
        return translated;
      }
    }

  }

  return t(locale, fallbackKey, vars);
}