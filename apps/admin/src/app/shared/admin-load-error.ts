import { AdeniApiError } from "@adeni/api-client";
import { ZodError } from "zod";

export function adminLoadErrorMessage(fallback: string, error: unknown): string {
  if (error instanceof AdeniApiError) {
    if (error.statusCode === 401 || error.statusCode === 403) {
      return `${fallback} Admin access was denied — sign in with an admin account (MFA when required) or set devAdminAuth0Sub locally.`;
    }
    if (error.statusCode === 404) {
      return `${fallback} The API route was not found — restart Adeni.Api after pulling latest admin paging changes.`;
    }
    return error.message.trim() || fallback;
  }
  if (error instanceof ZodError) {
    return `${fallback} The API response shape did not match — restart Adeni.Api so list endpoints return page, pageSize, and totalCount.`;
  }
  return fallback;
}
